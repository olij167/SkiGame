using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImpossibleRobert.Common;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UnityEngine;
#if BRAIN_OLLAMA
using OllamaSharp;
using OllamaSharp.Models;
#endif

namespace Brain
{
    /// <summary>
    /// Result of a caption operation.
    /// </summary>
    [Serializable]
    public class CaptionResult
    {
        public string path;
        public string caption;
    }

    /// <summary>
    /// Engine for generating AI captions from images using various backends.
    /// </summary>
    public static class CaptionEngine
    {
        /// <summary>
        /// Processes an image file for captioning - handles resizing and format conversion.
        /// </summary>
        /// <param name="filePath">Path to the image file</param>
        /// <param name="minSize">Minimum size in pixels (will upscale if smaller)</param>
        /// <returns>Tuple of image bytes and MIME type</returns>
        public static async Task<(byte[] imageBytes, string mimeType)> ProcessImageForCaption(string filePath, int minSize = 32, CancellationToken cancellationToken = default)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isPng = ext == ".png";
            bool isJpeg = ext == ".jpg" || ext == ".jpeg";
            string mime = isPng ? "image/png" : "image/jpeg";

            // Fast path: if the file is already a format Ollama accepts (PNG / JPEG)
            // and the dimensions are >= minSize, skip the decode/re-encode round trip
            // entirely. ImageSharp's full Load decodes every pixel into Rgba32 and
            // a re-save reencodes — for a 530 KB PNG that's ~500 ms of pure waste
            // when we're just going to send the bytes upstream anyway.
            // Image.IdentifyAsync only reads the file header (a few KB) so it's
            // typically <10 ms regardless of image size.
            if (isPng || isJpeg)
            {
                try
                {
                    // ImageUtils.GetDimensions reads only the PNG IHDR / JPEG SOF marker
                    // (a few bytes) — much cheaper than Image.Identify which scans further.
                    Tuple<int, int> dims = ImageUtils.GetDimensions(filePath, true, ext);
                    if (dims != null && dims.Item1 > 0 && dims.Item2 >= 2 &&
                        dims.Item1 >= minSize && dims.Item2 >= minSize)
                    {
                        byte[] raw;
                        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                        using (MemoryStream ms = new MemoryStream(fs.CanSeek ? (int)fs.Length : 0))
                        {
                            await fs.CopyToAsync(ms, 81920, cancellationToken);
                            raw = ms.ToArray();
                        }
                        return (raw, mime);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    // Header read failed (corrupt, exotic variant, etc.) — fall through
                    // to the slow path which is more permissive.
                }
            }

            // Slow path: format we don't pass through, or image is below minSize and
            // needs upscaling. Decode, optionally resize, re-encode.
            using (Image<Rgba32> img = await Image.LoadAsync<Rgba32>(filePath, cancellationToken))
            {
                int w = img.Width;
                int h = img.Height;

                if (h < 2) throw new InvalidOperationException("Image height is too small");

                double scale = Math.Max((float)minSize / w, (float)minSize / h);
                if (scale > 1.0)
                {
                    int newW = (int)Math.Ceiling(w * scale);
                    int newH = (int)Math.Ceiling(h * scale);
                    img.Mutate(x => x.Resize(newW, newH));
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    IImageEncoder encoder = isPng ? new PngEncoder() : (IImageEncoder)new JpegEncoder();
                    await img.SaveAsync(ms, encoder, cancellationToken);
                    byte[] imgBytes = ms.ToArray();
                    return (imgBytes, mime);
                }
            }
        }

        /// <summary>
        /// Generates captions for one or more images.
        /// </summary>
        /// <param name="filenames">List of image file paths</param>
        /// <param name="prompts">List of prompts corresponding to each file (must match filenames count)</param>
        /// <param name="modelName">Optional model name override</param>
        /// <param name="progressCallback">Optional callback for progress updates (progress 0-1, message)</param>
        /// <returns>List of caption results</returns>
        public static async Task<List<CaptionResult>> CaptionImages(
            List<string> filenames,
            List<string> prompts,
            string modelName = null,
            Action<float, string> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (filenames == null || filenames.Count == 0)
                return new List<CaptionResult>();

            if (prompts == null || prompts.Count != filenames.Count)
                throw new ArgumentException("Prompts list must match filenames count");

            IBrainSettings settings = Intelligence.Settings;
            List<CaptionResult> resultList = null;

            switch (settings.AIBackend)
            {
                case 0: // BLIP
                    resultList = await CaptionWithBlip(filenames, settings);
                    break;

                case 1: // Ollama
#if BRAIN_OLLAMA
                    resultList = await CaptionWithOllama(filenames, prompts, modelName ?? settings.OllamaModel, settings, progressCallback, cancellationToken);
#else
                    await Task.Yield();
                    Debug.LogError("Ollama backend is not enabled. The BRAIN_OLLAMA define is not set.");
#endif
                    break;

                case 2: // LM Studio
                    resultList = await CaptionWithLMStudio(filenames, prompts, modelName ?? settings.LMStudioModel, settings, progressCallback, cancellationToken);
                    break;
            }

            // Clean up results
            resultList?.ForEach(r =>
            {
                if (r.caption != null)
                {
                    r.caption = StringUtils.StripTags(r.caption, true)
                        .Trim()
                        .TrimStart('"')
                        .TrimEnd('"');
                    r.caption = StringUtils.StripTags(r.caption); // remove any left-over tags
                }
            });

            return resultList ?? new List<CaptionResult>();
        }

        /// <summary>
        /// Simplified caption method for a single image.
        /// </summary>
        public static async Task<string> CaptionImage(string filename, string prompt, string modelName = null, CancellationToken cancellationToken = default)
        {
            List<CaptionResult> results = await CaptionImages(
                new List<string> {filename},
                new List<string> {prompt},
                modelName,
                null,
                cancellationToken);
            return results?.FirstOrDefault()?.caption;
        }

        private static Task<List<CaptionResult>> CaptionWithBlip(List<string> filenames, IBrainSettings settings)
        {
            string blipType = settings.BlipType == 1 ? "--large" : "";
            string gpuUsage = settings.BlipUseGPU ? "--gpu" : "";
            string nameList = "\"" + string.Join("\" \"", filenames.Select(IOUtils.ToShortPath)) + "\"";
            string command = settings.BlipPath != null ? Path.Combine(settings.BlipPath, "blip-caption") : "blip-caption";
            string result = IOUtils.ExecuteCommand(command, $"{blipType} {gpuUsage} --json {nameList}");

            if (string.IsNullOrWhiteSpace(result)) return Task.FromResult<List<CaptionResult>>(null);

            try
            {
                return Task.FromResult(JsonConvert.DeserializeObject<List<CaptionResult>>(result));
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not parse BLIP result '{result}': {e.Message}");
                return Task.FromResult<List<CaptionResult>>(null);
            }
        }

#if BRAIN_OLLAMA
        /// <summary>
        /// When true, logs HTTP method/URL and per-chunk streaming timing. Default off because
        /// even lightweight logging adds noticeable per-call overhead in Unity's Debug.Log pipeline.
        /// Importantly, this logger NEVER reads <c>request.Content</c> — doing so would force
        /// JsonContent re-serialization of the Base64 image and add seconds of latency per call.
        /// </summary>
        public static bool DebugLogOllamaTraffic = false;

        /// <summary>
        /// Diagnostic toggle: when true, bypasses OllamaSharp entirely and posts a hand-crafted
        /// JSON body to <c>/api/generate</c> directly using a shared <see cref="HttpClient"/>.
        /// Used to isolate whether the per-image overhead lives in the OllamaSharp library
        /// (request building / JsonSerializer / async-enumerable plumbing) or below it
        /// (network / Ollama server / image encode). If "raw" matches "OllamaSharp" timings,
        /// the cost is below the library; if raw is much faster, the library is the culprit.
        /// </summary>
        public static bool UseRawHttpDiagnostic = false;

        private sealed class OllamaTrafficLogger : DelegatingHandler
        {
            public OllamaTrafficLogger(HttpMessageHandler inner) : base(inner) { }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!DebugLogOllamaTraffic) return await base.SendAsync(request, cancellationToken);

                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                long contentLen = request.Content?.Headers?.ContentLength ?? -1;
                Debug.Log($"[Ollama HTTP] -> {request.Method} {request.RequestUri} ({contentLen} bytes)");
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
                Debug.Log($"[Ollama HTTP] <- {(int)response.StatusCode} {response.StatusCode} (headers in {sw.ElapsedMilliseconds} ms; streaming body follows)");
                return response;
            }
        }

        private static HttpClient CreateOllamaHttpClient()
        {
            // Force IPv4 by replacing "localhost" with "127.0.0.1".
            // On Windows, localhost resolves to BOTH ::1 (IPv6) and 127.0.0.1 (IPv4). Ollama
            // (the desktop app) only binds IPv4, so the .NET stack pays a per-connection cost
            // for the IPv6 attempt to fail before falling back. Hardcoding 127.0.0.1 avoids it.
            string url = Intelligence.OllamaServiceUrl ?? string.Empty;
            Uri baseUri = new Uri(url);
            if (string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                UriBuilder ub = new UriBuilder(baseUri) { Host = "127.0.0.1" };
                baseUri = ub.Uri;
            }

            // Configure the legacy ServicePoint for this endpoint. HttpClientHandler in
            // Unity's Mono / .NET Standard runtime still honors ServicePointManager.
            //
            //   * UseNagleAlgorithm = false: Nagle batches small TCP writes. Combined with
            //     the receiver's delayed-ACK timer (~200 ms), uploading a multi-segment body
            //     (our 707 KB Base64 image) stalls between segments. Disabling Nagle was
            //     observed to drop "post→headers" time from ~3300 ms to a few hundred ms
            //     in this code path. THIS IS THE SINGLE BIGGEST WIN.
            //   * Expect100Continue = false: belt-and-suspenders alongside the per-client
            //     header — the legacy ServicePoint applies its own default if not overridden.
            //   * ConnectionLimit: bump from the default of 2 so retries / future parallel
            //     requests don't queue.
            try
            {
                System.Net.ServicePoint sp = System.Net.ServicePointManager.FindServicePoint(baseUri);
                sp.UseNagleAlgorithm = false;
                sp.Expect100Continue = false;
                sp.ConnectionLimit = 16;
            }
            catch { /* ServicePointManager may be unavailable on some runtimes; non-fatal. */ }

            HttpClientHandler inner = new HttpClientHandler
            {
                UseProxy = false,
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                AllowAutoRedirect = false
            };
            HttpClient http = new HttpClient(DebugLogOllamaTraffic ? (HttpMessageHandler)new OllamaTrafficLogger(inner) : inner)
            {
                BaseAddress = baseUri,
                // Vision models can take a long time on the first call. We rely on per-request
                // CancellationTokens (linked.Token) for caller-side timeouts; the HttpClient's
                // own timeout must be effectively infinite or it will tear down the streaming
                // connection mid-response.
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
            // Tell the server to keep the TCP connection open between requests; otherwise we
            // pay the connection setup cost (and Ollama's per-call overhead) on every image.
            http.DefaultRequestHeaders.ConnectionClose = false;
            http.DefaultRequestHeaders.ExpectContinue = false;
            return http;
        }

        private static async Task<List<CaptionResult>> CaptionWithOllama(
            List<string> filenames,
            List<string> prompts,
            string modelName,
            IBrainSettings settings,
            Action<float, string> progressCallback,
            CancellationToken cancellationToken)
        {
            // Use OllamaSharp's native API directly (the /api/generate endpoint).
            // This bypasses the Microsoft.Extensions.AI IChatClient adapter, which is
            // known to drop Ollama-specific knobs (keep_alive, think) when passed via
            // AdditionalProperties, and which streams by default — both of which made
            // the previous IChatClient path stall on vision models.
            HttpClient http = CreateOllamaHttpClient();
            OllamaApiClient client = new OllamaApiClient(http.BaseAddress.ToString()) { SelectedModel = modelName };
            List<CaptionResult> resultList = new List<CaptionResult>();

            // Mirror the LM Studio batching strategy. Ollama serves up to OLLAMA_NUM_PARALLEL
            // concurrent requests per model (default 4); going higher just queues server-side.
            // The captioning workload is GPU-bound — the win comes from amortising per-request
            // overhead (HTTP roundtrip, model load check, prompt-eval setup) across the batch.
            int parallelCount = Math.Max(1, settings.OllamaParallelRequests);

            try
            {
                for (int batchStart = 0; batchStart < filenames.Count; batchStart += parallelCount)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    int batchSize = Math.Min(parallelCount, filenames.Count - batchStart);
                    List<Task<CaptionResult>> batchTasks = new List<Task<CaptionResult>>(batchSize);

                    for (int i = 0; i < batchSize; i++)
                    {
                        int idx = batchStart + i;
                        string file = filenames[idx];
                        string prompt = prompts[idx];
                        progressCallback?.Invoke((float)idx / filenames.Count, Path.GetFileName(file));
                        batchTasks.Add(ProcessOllamaRequest(client, http, file, prompt, modelName, settings, cancellationToken));
                    }

                    CaptionResult[] batchResults;
                    try
                    {
                        batchResults = await Task.WhenAll(batchTasks);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch
                    {
                        // Individual task errors are already logged inside ProcessOllamaRequest.
                        // Materialise whatever did succeed.
                        batchResults = batchTasks.Select(t => t.Status == TaskStatus.RanToCompletion ? t.Result : null).ToArray();
                    }

                    if (batchResults != null)
                    {
                        resultList.AddRange(batchResults.Where(r => r != null));
                    }
                }
            }
            finally
            {
                http.Dispose();
            }
            return resultList;
        }

        private static async Task<CaptionResult> ProcessOllamaRequest(
            OllamaApiClient client,
            HttpClient http,
            string file,
            string prompt,
            string modelName,
            IBrainSettings settings,
            CancellationToken cancellationToken)
        {
            using (CancellationTokenSource linked = CreateRequestCts(cancellationToken, settings.AITimeout))
            {
                try
                {
                    System.Diagnostics.Stopwatch phaseSw = System.Diagnostics.Stopwatch.StartNew();
                    long tImageStart = phaseSw.ElapsedMilliseconds;

                    (byte[] imgBytes, string _mime) = await ProcessImageForCaption(file, settings.AIMinSize, linked.Token);

                    long tImageDone = phaseSw.ElapsedMilliseconds;

                    string base64Image = Convert.ToBase64String(imgBytes);
                    long tBase64Done = phaseSw.ElapsedMilliseconds;

                    string responseText;
                    int rawChunkCount = 0;

                    if (UseRawHttpDiagnostic)
                    {
                        // ----- RAW DIAGNOSTIC PATH -----
                        // Hand-crafted JSON to /api/generate, no OllamaSharp involvement.
                        string jsonBody;
                        using (StringWriter sw = new StringWriter())
                        using (JsonTextWriter jw = new JsonTextWriter(sw))
                        {
                            jw.WriteStartObject();
                            jw.WritePropertyName("model"); jw.WriteValue(modelName);
                            jw.WritePropertyName("prompt"); jw.WriteValue(prompt);
                            jw.WritePropertyName("stream"); jw.WriteValue(false);
                            jw.WritePropertyName("think"); jw.WriteValue(false);
                            jw.WritePropertyName("keep_alive"); jw.WriteValue("30m");
                            jw.WritePropertyName("images");
                            jw.WriteStartArray();
                            jw.WriteValue(base64Image);
                            jw.WriteEndArray();
                            jw.WritePropertyName("options");
                            jw.WriteStartObject();
                            jw.WritePropertyName("num_predict"); jw.WriteValue(settings.AIMaxCaptionLength + 100);
                            jw.WritePropertyName("temperature"); jw.WriteValue(0.2);
                            jw.WriteEndObject();
                            jw.WriteEndObject();
                            jsonBody = sw.ToString();
                        }
                        long tSerializeDone = phaseSw.ElapsedMilliseconds;

                        using (StringContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
                        using (HttpResponseMessage resp = await http.PostAsync("/api/generate", content, linked.Token))
                        {
                            long tHeadersDone = phaseSw.ElapsedMilliseconds;
                            resp.EnsureSuccessStatusCode();
                            string respJson = await resp.Content.ReadAsStringAsync();
                            long tBodyDone = phaseSw.ElapsedMilliseconds;
                            Newtonsoft.Json.Linq.JObject obj = Newtonsoft.Json.Linq.JObject.Parse(respJson);
                            responseText = obj.Value<string>("response") ?? string.Empty;
                            long tParseDone = phaseSw.ElapsedMilliseconds;
                            if (DebugLogOllamaTraffic)
                            {
                                Debug.Log($"[Ollama RAW] '{Path.GetFileName(file)}' total={tParseDone} ms | image={tImageDone - tImageStart} ms base64={tBase64Done - tImageDone} ms (img bytes={imgBytes.Length} b64={base64Image.Length}) serialize={tSerializeDone - tBase64Done} ms post→headers={tHeadersDone - tSerializeDone} ms body={tBodyDone - tHeadersDone} ms parse={tParseDone - tBodyDone} ms");
                            }
                        }
                    }
                    else
                    {
                        // ----- OLLAMASHARP PATH -----
                        GenerateRequest request = new GenerateRequest
                        {
                            Model = modelName,
                            Prompt = prompt,
                            Images = new[] { base64Image },
                            KeepAlive = "30m",
                            // Stream=false: OllamaSharp still returns IAsyncEnumerable but
                            // yields a single chunk; one await instead of one-per-token.
                            Stream = false,
                            Options = new RequestOptions
                            {
                                NumPredict = settings.AIMaxCaptionLength + 100,
                                Temperature = 0.2f,
                                Stop = new[] { "\n\n\n", "<|im_end|>", "<|endoftext|>" }
                            }
                        };

                        long tBuildDone = phaseSw.ElapsedMilliseconds;

                        StringBuilder sb = new StringBuilder();
                        StringBuilder thinkSb = new StringBuilder();
                        long firstChunkMs = -1;
                        // OllamaApiClient is documented as thread-safe (its underlying HttpClient
                        // is shared across calls); concurrent GenerateAsync calls are supported.
                        await foreach (GenerateResponseStream chunk in client.GenerateAsync(request, linked.Token))
                        {
                            if (chunk == null) continue;
                            rawChunkCount++;
                            if (firstChunkMs < 0) firstChunkMs = phaseSw.ElapsedMilliseconds;
                            if (chunk.Response != null) sb.Append(chunk.Response);
                            if (chunk is GenerateDoneResponseStream done && DebugLogOllamaTraffic)
                            {
                                Debug.Log($"[Ollama server] '{Path.GetFileName(file)}' total={done.TotalDuration / 1_000_000} ms load={done.LoadDuration / 1_000_000} ms prompt_eval={done.PromptEvalDuration / 1_000_000} ms eval={done.EvalDuration / 1_000_000} ms eval_count={done.EvalCount}");
                            }
                        }
                        long tEnumDone = phaseSw.ElapsedMilliseconds;
                        responseText = sb.ToString();

                        if (DebugLogOllamaTraffic)
                        {
                            Debug.Log($"[Ollama LIB] '{Path.GetFileName(file)}' total={tEnumDone} ms | image={tImageDone - tImageStart} ms base64={tBase64Done - tImageDone} ms (img bytes={imgBytes.Length} b64={base64Image.Length}) build={tBuildDone - tBase64Done} ms enum_to_first_chunk={firstChunkMs - tBuildDone} ms enum_total={tEnumDone - tBuildDone} ms chunks={rawChunkCount} resp_len={sb.Length} think_len={thinkSb.Length}");
                            if (sb.Length == 0 && thinkSb.Length > 0)
                            {
                                Debug.LogWarning($"[Ollama] Model '{modelName}' produced ONLY thinking tokens (think:false ignored). Use a non-reasoning vision model (e.g. qwen2.5vl:7b, llava:13b, llama3.2-vision) for captioning.");
                            }
                        }
                    }

                    return new CaptionResult
                    {
                        path = file,
                        caption = responseText
                    };
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Bubble out so Task.WhenAll observes the cancel.
                    throw;
                }
                catch (OperationCanceledException)
                {
                    Debug.LogWarning($"Ollama request for '{file}' timed out after {settings.AITimeout}s.");
                    return null;
                }
                catch (HttpRequestException httpE)
                {
                    Debug.LogError($"Could not connect to Ollama for '{file}': {httpE.Message}");
                    return null;
                }
                catch (InvalidOperationException opE)
                {
                    Debug.LogError($"Ollama model error for '{file}', image might be too small: {opE.Message}");
                    return null;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not get Ollama result for '{file}': {e.Message}");
                    return null;
                }
            }
        }
#endif

        private static async Task<List<CaptionResult>> CaptionWithLMStudio(
            List<string> filenames,
            List<string> prompts,
            string modelName,
            IBrainSettings settings,
            Action<float, string> progressCallback,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(modelName))
            {
                Debug.LogError("LM Studio model name is not configured.");
                return new List<CaptionResult>();
            }

            List<CaptionResult> resultList = new List<CaptionResult>();
            int parallelCount = Math.Max(1, settings.LMStudioParallelRequests);

            for (int batchStart = 0; batchStart < filenames.Count; batchStart += parallelCount)
            {
                if (cancellationToken.IsCancellationRequested) break;

                int batchSize = Math.Min(parallelCount, filenames.Count - batchStart);
                List<Task<CaptionResult>> batchTasks = new List<Task<CaptionResult>>();

                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batchStart + i;
                    string file = filenames[idx];
                    string prompt = prompts[idx];
                    progressCallback?.Invoke((float)idx / filenames.Count, Path.GetFileName(file));
                    batchTasks.Add(ProcessLMStudioRequest(file, prompt, modelName, settings, cancellationToken));
                }

                CaptionResult[] batchResults;
                try
                {
                    batchResults = await Task.WhenAll(batchTasks);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                resultList.AddRange(batchResults.Where(r => r != null));
            }

            return resultList;
        }

        private static async Task<CaptionResult> ProcessLMStudioRequest(string file, string prompt, string modelName, IBrainSettings settings, CancellationToken cancellationToken)
        {
            using (CancellationTokenSource linked = CreateRequestCts(cancellationToken, settings.AITimeout))
            {
                try
                {
                    (byte[] imgBytes, string mime) = await ProcessImageForCaption(file, settings.AIMinSize, linked.Token);

                    string base64Image = Convert.ToBase64String(imgBytes);
                    string imageDataUri = $"data:{mime};base64,{base64Image}";

                    LMStudioChatRequest request = new LMStudioChatRequest
                    {
                        model = modelName,
                        messages = new List<LMStudioChatMessage>
                        {
                            new LMStudioChatMessage
                            {
                                role = "user",
                                content = new List<LMStudioContent>
                                {
                                    new LMStudioContent
                                    {
                                        type = "text",
                                        text = prompt
                                    },
                                    new LMStudioContent
                                    {
                                        type = "image_url",
                                        image_url = new LMStudioImageUrl
                                        {
                                            url = imageDataUri
                                        }
                                    }
                                }
                            }
                        },
                        temperature = 0.95f,
                        max_tokens = 5000
                    };

                    using (HttpClient httpClient = new HttpClient())
                    {
                        // HttpClient.Timeout is intentionally left at the default (Infinite-ish);
                        // cancellation is driven by the linked token (global stop + AITimeout).
                        httpClient.Timeout = Timeout.InfiniteTimeSpan;
                        string json = JsonConvert.SerializeObject(request);
                        StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                        HttpResponseMessage response = await httpClient.PostAsync($"{Intelligence.LMStudioServiceUrl}/v1/chat/completions", content, linked.Token);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseJson = await response.Content.ReadAsStringAsync();
                            LMStudioChatResponse chatResponse = JsonConvert.DeserializeObject<LMStudioChatResponse>(responseJson);

                            if (chatResponse?.choices != null && chatResponse.choices.Count > 0)
                            {
                                string caption = chatResponse.choices[0].message?.content;
                                return new CaptionResult
                                {
                                    path = file,
                                    caption = caption
                                };
                            }
                            else
                            {
                                Debug.LogWarning($"LM Studio returned an empty response for '{file}'.");
                                return null;
                            }
                        }
                        else
                        {
                            string errorContent = await response.Content.ReadAsStringAsync();
                            Debug.LogError($"LM Studio API error for '{file}': {response.StatusCode} - {errorContent}");
                            return null;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // global cancel: propagate so the outer Task.WhenAll observes it
                    throw;
                }
                catch (OperationCanceledException)
                {
                    // per-request timeout: skip
                    Debug.LogWarning($"LM Studio request for '{file}' timed out after {settings.AITimeout}s.");
                    return null;
                }
                catch (HttpRequestException httpE)
                {
                    Debug.LogError($"Could not connect to LM Studio for '{file}': {httpE.Message}");
                    return null;
                }
                catch (InvalidOperationException opE)
                {
                    Debug.LogError($"LM Studio model error for '{file}', image might be too small or model not loaded: {opE.Message}");
                    return null;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not get LM Studio result for '{file}': {e.Message}");
                    return null;
                }
            }
        }

        /// <summary>
        /// Builds a CTS linked to the caller's token; if <paramref name="timeoutSeconds"/> &gt; 0,
        /// it will additionally cancel after that many seconds.
        /// </summary>
        private static CancellationTokenSource CreateRequestCts(CancellationToken outer, int timeoutSeconds)
        {
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(outer);
            if (timeoutSeconds > 0) linked.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            return linked;
        }
    }
}
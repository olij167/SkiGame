using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
using Microsoft.Extensions.AI;
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
        public static async Task<(byte[] imageBytes, string mimeType)> ProcessImageForCaption(string filePath, int minSize = 32)
        {
            using (Image<Rgba32> img = await Image.LoadAsync<Rgba32>(filePath))
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
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    IImageEncoder encoder = ext == ".png" ? new PngEncoder() : new JpegEncoder();
                    await img.SaveAsync(ms, encoder);
                    byte[] imgBytes = ms.ToArray();
                    string mime = ext == ".png" ? "image/png" : "image/jpeg";
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
            Action<float, string> progressCallback = null)
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
                    resultList = await CaptionWithOllama(filenames, prompts, modelName ?? settings.OllamaModel, settings, progressCallback);
#else
                    await Task.Yield();
                    Debug.LogError("Ollama backend is not enabled. The BRAIN_OLLAMA define is not set.");
#endif
                    break;

                case 2: // LM Studio
                    resultList = await CaptionWithLMStudio(filenames, prompts, modelName ?? settings.LMStudioModel, settings, progressCallback);
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
        public static async Task<string> CaptionImage(string filename, string prompt, string modelName = null)
        {
            List<CaptionResult> results = await CaptionImages(
                new List<string> {filename},
                new List<string> {prompt},
                modelName);
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
        private static async Task<List<CaptionResult>> CaptionWithOllama(
            List<string> filenames,
            List<string> prompts,
            string modelName,
            IBrainSettings settings,
            Action<float, string> progressCallback)
        {
            IChatClient client = new OllamaChatClient(new Uri(Intelligence.OllamaServiceUrl), modelName);
            List<CaptionResult> resultList = new List<CaptionResult>();

            for (int idx = 0; idx < filenames.Count; idx++)
            {
                string file = filenames[idx];
                string prompt = prompts[idx];
                progressCallback?.Invoke((float)idx / filenames.Count, Path.GetFileName(file));

                try
                {
                    (byte[] imgBytes, string mime) = await ProcessImageForCaption(file, settings.AIMinSize);

                    ChatMessage msg = new ChatMessage(ChatRole.User, prompt);
                    msg.Contents.Add(new DataContent(imgBytes, mime));

                    ChatResponse response = await client.GetResponseAsync(msg);
                    resultList.Add(new CaptionResult
                    {
                        path = file,
                        caption = response.Text
                    });
                }
                catch (HttpRequestException httpE)
                {
                    Debug.LogError($"Could not connect to Ollama for '{file}': {httpE.Message}");
                }
                catch (InvalidOperationException opE)
                {
                    Debug.LogError($"Ollama model error for '{file}', image might be too small: {opE.Message}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not get Ollama result for '{file}': {e.Message}");
                }
            }

            return resultList;
        }
#endif

        private static async Task<List<CaptionResult>> CaptionWithLMStudio(
            List<string> filenames,
            List<string> prompts,
            string modelName,
            IBrainSettings settings,
            Action<float, string> progressCallback)
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
                int batchSize = Math.Min(parallelCount, filenames.Count - batchStart);
                List<Task<CaptionResult>> batchTasks = new List<Task<CaptionResult>>();

                for (int i = 0; i < batchSize; i++)
                {
                    int idx = batchStart + i;
                    string file = filenames[idx];
                    string prompt = prompts[idx];
                    progressCallback?.Invoke((float)idx / filenames.Count, Path.GetFileName(file));
                    batchTasks.Add(ProcessLMStudioRequest(file, prompt, modelName, settings));
                }

                CaptionResult[] batchResults = await Task.WhenAll(batchTasks);
                resultList.AddRange(batchResults.Where(r => r != null));
            }

            return resultList;
        }

        private static async Task<CaptionResult> ProcessLMStudioRequest(string file, string prompt, string modelName, IBrainSettings settings)
        {
            try
            {
                (byte[] imgBytes, string mime) = await ProcessImageForCaption(file, settings.AIMinSize);

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
                    httpClient.Timeout = TimeSpan.FromMinutes(5);
                    string json = JsonConvert.SerializeObject(request);
                    StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await httpClient.PostAsync($"{Intelligence.LMStudioServiceUrl}/v1/chat/completions", content);

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
}
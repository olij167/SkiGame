using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
#if BRAIN_OLLAMA
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
#endif

namespace Brain
{
    /// <summary>
    /// Main service for AI backend connectivity. Manages connections to Ollama, LM Studio, and BLIP.
    /// </summary>
    public static class Intelligence
    {
        public const string OLLAMA_WEBSITE = "https://www.ollama.com";
        public const string OLLAMA_LIBRARY = "https://ollama.com/search?c=vision";
        public const string OLLAMA_SERVICE_URL = "http://localhost:11434";

        public const string LMSTUDIO_WEBSITE = "https://lmstudio.ai";
        public const string LMSTUDIO_SERVICE_URL = "http://localhost:1234";

        public static readonly string[] SuggestedOllamaModels =
        {
            "qwen3-vl:8b (better but significantly slower)",
            "qwen2.5vl:7b (recommended)",
            "qwen2.5vl:3b (still good, faster, lower memory requirements)",
            "llava:7b (good alternative, comes down to personal preference)"
        };

        public static readonly string DefaultPrompt =
            "The image filename is: $filename"
            + "\nFile path: $path"
            + "\n\nWrite a single-sentence caption for the image, followed by 5-10 short comma-separated keywords."
            + "\nThe image is a preview of a 2D or 3D graphic."
            + "\nUse at most 25 words for the sentence."
            + "\nClearly state if the style is cartoon or realistic."
            + "\nFocus only on the main subject and its most visible colors and materials."
            + "\nDo not mention how or where it could be used."
            + "\nDo not mention 'game', 'engine', 'asset', 'Unity', or 'preview'."
            + "\nPrefer this structure:"
            + "\n[short caption sentence]. [style], [main colors], [subject type], [mood or theme], [extra visual traits]";

        private static IBrainSettings _settings;

        /// <summary>
        /// The current settings instance. Must be set before using any Brain services.
        /// </summary>
        public static IBrainSettings Settings
        {
            get => _settings ?? (_settings = new BrainSettings());
            set => _settings = value;
        }

#if BRAIN_OLLAMA
        private static IEnumerable<Model> _models;
        private static IEnumerable<ModelInfo> _modelsInfo;
        private static bool? _ollamaInstalled;
        private static string _ollamaVersion;

        /// <summary>
        /// List of locally available Ollama models (raw OllamaSharp type, for internal use).
        /// </summary>
        internal static IEnumerable<Model> OllamaModelsRaw
        {
            get
            {
                if (_models == null)
                {
                    _ = LoadOllamaModels();
                }
                return _models;
            }
        }

        /// <summary>
        /// List of locally available Ollama models.
        /// </summary>
        public static IEnumerable<ModelInfo> OllamaModels
        {
            get
            {
                if (_modelsInfo == null && _models != null)
                {
                    _modelsInfo = _models.Select(m => new ModelInfo
                    {
                        Name = m.Name,
                        ModifiedAt = m.ModifiedAt,
                        Size = m.Size,
                        Family = m.Details.Family,
                        ParameterSize = m.Details.ParameterSize,
                        QuantizationLevel = m.Details.QuantizationLevel
                    }).ToList();
                }
                if (_modelsInfo == null)
                {
                    _ = LoadOllamaModels();
                }
                return _modelsInfo ?? Enumerable.Empty<ModelInfo>();
            }
        }

        /// <summary>
        /// Checks if a specific Ollama model is downloaded locally.
        /// </summary>
        public static bool OllamaModelDownloaded(string name) => OllamaModels != null && OllamaModels.Any(m => m.Name == name || m.Name.StartsWith(name + ":"));

        public static bool LoadingModels;
        public static bool DownloadingModel;
        public static CancellationTokenSource OllamaDownloadToken;

        /// <summary>
        /// Whether Ollama is installed and running.
        /// </summary>
        public static bool IsOllamaInstalled
        {
            get
            {
                if (_ollamaInstalled == null)
                {
                    try
                    {
                        _ = CheckOllamaInstalled();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error checking Ollama installation. Restart Unity to solve: {e.Message}");
                        _ollamaInstalled = false;
                    }
                }
                return _ollamaInstalled ?? false;
            }
        }

        /// <summary>
        /// The installed Ollama version, or null if not installed.
        /// </summary>
        public static string OllamaVersion => IsOllamaInstalled ? _ollamaVersion : null;

        /// <summary>
        /// The Ollama service URL from settings, or default.
        /// </summary>
        public static string OllamaServiceUrl =>
            string.IsNullOrWhiteSpace(Settings.OllamaServiceUrl)
                ? OLLAMA_SERVICE_URL
                : Settings.OllamaServiceUrl;

        /// <summary>
        /// Refreshes Ollama connection status and model list.
        /// </summary>
        public static void RefreshOllama()
        {
            _ollamaInstalled = null;
            _models = null;
            _modelsInfo = null;
            _ollamaVersion = null;
            LoadingModels = false;
            DownloadingModel = false;

            _ = CheckOllamaInstalled();
            _ = LoadOllamaModels();
        }

        private static async Task CheckOllamaInstalled()
        {
            _ollamaInstalled = false;

            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                if (await ollama.IsRunningAsync())
                {
                    _ollamaInstalled = true;
                    _ollamaVersion = await ollama.GetVersionAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error checking Ollama installation: {e.Message}");
            }
        }

        private static async Task LoadOllamaModels()
        {
            if (LoadingModels) return;
            LoadingModels = true;

            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                _models = await ollama.ListLocalModelsAsync();
                _modelsInfo = _models.Select(m => new ModelInfo
                {
                    Name = m.Name,
                    ModifiedAt = m.ModifiedAt,
                    Size = m.Size,
                    Family = m.Details.Family,
                    ParameterSize = m.Details.ParameterSize,
                    QuantizationLevel = m.Details.QuantizationLevel
                }).ToList();
            }
            catch (Exception e)
            {
                _models = Enumerable.Empty<Model>();
                _modelsInfo = Enumerable.Empty<ModelInfo>();
                Debug.LogError($"Error loading Ollama models: {e.Message}");
            }

            LoadingModels = false;
        }

        /// <summary>
        /// Downloads an Ollama model.
        /// </summary>
        /// <param name="name">Model name to download</param>
        /// <param name="statusCallback">Callback for download progress</param>
        public static async Task PullOllamaModel(string name, Action<PullModelResponse> statusCallback)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (DownloadingModel) return;
            DownloadingModel = true;

            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                using (OllamaDownloadToken = new CancellationTokenSource())
                {
                    await foreach (PullModelResponse status in ollama.PullModelAsync(name, OllamaDownloadToken.Token))
                    {
                        statusCallback?.Invoke(status);
                    }
                }
                _models = null; // force reload
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading Ollama model {name}: {e.Message}");
            }

            DownloadingModel = false;
        }

        /// <summary>
        /// Deletes an Ollama model.
        /// </summary>
        public static async Task DeleteOllamaModel(string name)
        {
            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                await ollama.DeleteModelAsync(new DeleteModelRequest {Model = name});
                _models = null; // force reload
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting Ollama model {name}: {e.Message}");
            }
        }

        /// <summary>
        /// Checks if Ollama is running asynchronously.
        /// </summary>
        public static async Task<bool> IsOllamaRunningAsync()
        {
            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                return await ollama.IsRunningAsync();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the Ollama version asynchronously.
        /// </summary>
        public static async Task<string> GetOllamaVersionAsync()
        {
            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                return await ollama.GetVersionAsync();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Lists locally available Ollama models.
        /// </summary>
        public static async Task<IEnumerable<ModelInfo>> ListOllamaModelsAsync()
        {
            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                IEnumerable<Model> models = await ollama.ListLocalModelsAsync();
                return models.Select(m => new ModelInfo
                {
                    Name = m.Name,
                    ModifiedAt = m.ModifiedAt,
                    Size = m.Size,
                    Family = m.Details.Family,
                    ParameterSize = m.Details.ParameterSize,
                    QuantizationLevel = m.Details.QuantizationLevel
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing Ollama models: {e.Message}");
                return Enumerable.Empty<ModelInfo>();
            }
        }

        /// <summary>
        /// Downloads an Ollama model.
        /// </summary>
        public static async Task PullOllamaModelAsync(string name, Action<DownloadStatus> statusCallback, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name)) return;

            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                await foreach (PullModelResponse status in ollama.PullModelAsync(name, cancellationToken))
                {
                    statusCallback?.Invoke(new DownloadStatus
                    {
                        Status = status?.Status,
                        Digest = status?.Digest,
                        Total = status?.Total ?? 0,
                        Completed = status?.Completed ?? 0
                    });
                }
                _models = null; // force reload
            }
            catch (OperationCanceledException)
            {
                // Download was cancelled, that's ok
            }
            catch (Exception e)
            {
                Debug.LogError($"Error downloading Ollama model {name}: {e.Message}");
            }
        }

        /// <summary>
        /// Deletes an Ollama model.
        /// </summary>
        public static async Task DeleteOllamaModelAsync(string name)
        {
            await DeleteOllamaModel(name);
        }

        /// <summary>
        /// Simple chat completion using Ollama.
        /// </summary>
        /// <param name="systemPrompt">System context/instructions</param>
        /// <param name="userMessage">User's message</param>
        /// <returns>The assistant's response text</returns>
        public static async Task<string> ChatAsync(string systemPrompt, string userMessage)
        {
            try
            {
                OllamaApiClient ollama = new OllamaApiClient(new Uri(OllamaServiceUrl));
                ollama.SelectedModel = Settings.OllamaModel;

                ChatRequest request = new ChatRequest
                {
                    Model = Settings.OllamaModel,
                    Messages = new List<Message>
                    {
                        new Message {Role = ChatRole.System, Content = systemPrompt},
                        new Message {Role = ChatRole.User, Content = userMessage}
                    }
                };

                string fullResponse = "";
                await foreach (ChatResponseStream chunk in ollama.ChatAsync(request))
                {
                    if (chunk?.Message?.Content != null)
                    {
                        fullResponse += chunk.Message.Content;
                    }
                }
                return fullResponse;
            }
            catch (Exception e)
            {
                Debug.LogError($"Chat error: {e.Message}");
                return null;
            }
        }
#else
        public static bool IsOllamaInstalled => false;
        public static string OllamaVersion => null;
        public static string OllamaServiceUrl => Settings.OllamaServiceUrl;
        public static void RefreshOllama() { }

        public static Task<bool> IsOllamaRunningAsync() => Task.FromResult(false);
        public static Task<string> GetOllamaVersionAsync() => Task.FromResult<string>(null);
        public static Task<IEnumerable<ModelInfo>> ListOllamaModelsAsync() => Task.FromResult(Enumerable.Empty<ModelInfo>());
        public static Task PullOllamaModelAsync(string name, Action<DownloadStatus> statusCallback, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public static Task DeleteOllamaModelAsync(string name) => Task.CompletedTask;
        public static Task<string> ChatAsync(string systemPrompt, string userMessage) => Task.FromResult<string>(null);
#endif

        // LM Studio support (always available, no conditional compilation needed)
        private static IEnumerable<LMStudioModel> _lmstudioModels;
        private static bool? _lmstudioInstalled;
        private static string _lmstudioVersion;

        public static bool LoadingLMStudioModels;

        /// <summary>
        /// List of available LM Studio models.
        /// </summary>
        public static IEnumerable<LMStudioModel> LMStudioModels
        {
            get
            {
                if (_lmstudioModels == null)
                {
                    _ = LoadLMStudioModels();
                }
                return _lmstudioModels ?? Enumerable.Empty<LMStudioModel>();
            }
        }

        /// <summary>
        /// Whether LM Studio is installed and running.
        /// </summary>
        public static bool IsLMStudioInstalled
        {
            get
            {
                if (_lmstudioInstalled == null)
                {
                    try
                    {
                        _ = CheckLMStudioInstalled();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error checking LM Studio installation. Restart Unity to solve: {e.Message}");
                        _lmstudioInstalled = false;
                    }
                }
                return _lmstudioInstalled ?? false;
            }
        }

        /// <summary>
        /// The installed LM Studio version, or null if not installed.
        /// </summary>
        public static string LMStudioVersion => IsLMStudioInstalled ? _lmstudioVersion : null;

        /// <summary>
        /// The LM Studio service URL from settings, or default.
        /// </summary>
        public static string LMStudioServiceUrl =>
            string.IsNullOrWhiteSpace(Settings.LMStudioServiceUrl)
                ? LMSTUDIO_SERVICE_URL
                : Settings.LMStudioServiceUrl;

        /// <summary>
        /// Refreshes LM Studio connection status and model list.
        /// </summary>
        public static void RefreshLMStudio()
        {
            _lmstudioInstalled = null;
            _lmstudioModels = null;
            _lmstudioVersion = null;
            LoadingLMStudioModels = false;
            _ = CheckLMStudioInstalled();
            _ = LoadLMStudioModels();
        }

        private static async Task CheckLMStudioInstalled()
        {
            _lmstudioInstalled = false;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    HttpResponseMessage response = await client.GetAsync($"{LMStudioServiceUrl}/api/v0/models");
                    if (response.IsSuccessStatusCode)
                    {
                        _lmstudioInstalled = true;
                        try
                        {
                            HttpResponseMessage versionResponse = await client.GetAsync($"{LMStudioServiceUrl}/api/v0/info");
                            if (versionResponse.IsSuccessStatusCode)
                            {
                                string versionJson = await versionResponse.Content.ReadAsStringAsync();
                                LMStudioInfoResponse info = JsonConvert.DeserializeObject<LMStudioInfoResponse>(versionJson);
                                _lmstudioVersion = info?.version;
                            }
                        }
                        catch
                        {
                            // Version endpoint might not be available
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error checking LM Studio installation: {e.Message}");
            }
        }

        private static async Task LoadLMStudioModels()
        {
            if (LoadingLMStudioModels) return;
            LoadingLMStudioModels = true;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    HttpResponseMessage response = await client.GetAsync($"{LMStudioServiceUrl}/api/v0/models");
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        LMStudioModelsResponse modelsResponse = JsonConvert.DeserializeObject<LMStudioModelsResponse>(json);
                        _lmstudioModels = modelsResponse?.data ?? Enumerable.Empty<LMStudioModel>();
                    }
                    else
                    {
                        _lmstudioModels = Enumerable.Empty<LMStudioModel>();
                    }
                }
            }
            catch (Exception e)
            {
                _lmstudioModels = Enumerable.Empty<LMStudioModel>();
                Debug.LogError($"Error loading LM Studio models: {e.Message}");
            }

            LoadingLMStudioModels = false;
        }
    }

    // LM Studio data classes
    [Serializable]
    public class LMStudioModel
    {
        public string id;
        public string type;
        public string state;
        public string quantization;
        public int? max_context_length;
    }

    [Serializable]
    public class LMStudioModelsResponse
    {
        public List<LMStudioModel> data;
    }

    [Serializable]
    public class LMStudioInfoResponse
    {
        public string version;
    }

    // LM Studio chat request/response classes
    [Serializable]
    public class LMStudioChatRequest
    {
        public string model;
        public List<LMStudioChatMessage> messages;
        public float temperature;
        public int max_tokens;
    }

    [Serializable]
    public class LMStudioChatMessage
    {
        public string role;
        public List<LMStudioContent> content;
    }

    [Serializable]
    public class LMStudioContent
    {
        public string type;
        public string text;
        public LMStudioImageUrl image_url;
    }

    [Serializable]
    public class LMStudioImageUrl
    {
        public string url;
    }

    [Serializable]
    public class LMStudioChatResponse
    {
        public List<LMStudioChoice> choices;
    }

    [Serializable]
    public class LMStudioChoice
    {
        public LMStudioMessage message;
    }

    [Serializable]
    public class LMStudioMessage
    {
        public string content;
    }
}
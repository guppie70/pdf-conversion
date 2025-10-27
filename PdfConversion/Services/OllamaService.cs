using System.Net.Http.Json;
using System.Text.Json;
using PdfConversion.Models;

namespace PdfConversion.Services;

public interface IOllamaService
{
    Task<bool> CheckHealthAsync();
    Task<List<OllamaModel>> GetAvailableModelsAsync();
    Task WarmUpModelAsync(string modelName, CancellationToken cancellationToken = default);
    Task<string> GenerateAsync(
        string model,
        string prompt,
        double temperature = 0.3,
        CancellationToken cancellationToken = default
    );
}

public class OllamaService : IOllamaService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private const string BaseUrl = "http://host.docker.internal:11434";

    public OllamaService(IHttpClientFactory httpClientFactory, ILogger<OllamaService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Long timeout for large models
        _logger = logger;
    }

    /// <summary>
    /// Check if Ollama service is running and accessible
    /// </summary>
    public async Task<bool> CheckHealthAsync()
    {
        try
        {
            _logger.LogInformation("[OllamaService] Checking health at {BaseUrl}", BaseUrl);
            var response = await _httpClient.GetAsync("/api/tags");
            var isHealthy = response.IsSuccessStatusCode;

            _logger.LogInformation("[OllamaService] Health check result: {IsHealthy}", isHealthy);
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[OllamaService] Health check failed. Is Ollama running?");
            return false;
        }
    }

    /// <summary>
    /// Get list of available models from Ollama
    /// </summary>
    public async Task<List<OllamaModel>> GetAvailableModelsAsync()
    {
        try
        {
            _logger.LogInformation("[OllamaService] Fetching available models");

            var response = await _httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            var models = new List<OllamaModel>();

            if (data.TryGetProperty("models", out var modelsArray))
            {
                foreach (var modelElement in modelsArray.EnumerateArray())
                {
                    var model = new OllamaModel
                    {
                        Name = modelElement.GetProperty("name").GetString() ?? "",
                        Model = modelElement.GetProperty("model").GetString() ?? "",
                        Size = modelElement.GetProperty("size").GetInt64(),
                        ModifiedAt = modelElement.GetProperty("modified_at").GetDateTime()
                    };

                    if (modelElement.TryGetProperty("details", out var details))
                    {
                        model.Details = new OllamaModelDetails
                        {
                            Format = details.GetProperty("format").GetString() ?? "",
                            Family = details.GetProperty("family").GetString() ?? "",
                            ParameterSize = details.GetProperty("parameter_size").GetString() ?? "",
                            QuantizationLevel = details.GetProperty("quantization_level").GetString() ?? ""
                        };
                    }

                    models.Add(model);
                }
            }

            _logger.LogInformation("[OllamaService] Found {Count} models", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OllamaService] Failed to get available models");
            throw;
        }
    }

    /// <summary>
    /// Warm up (preload) a model by sending a minimal generation request
    /// This loads the model into memory for faster subsequent requests
    /// </summary>
    public async Task WarmUpModelAsync(string modelName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[OllamaService] Warming up model: {Model}", modelName);

            var request = new OllamaGenerateRequest
            {
                Model = modelName,
                Prompt = "Hi",
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = 0.3,
                    TopP = 0.9,
                    NumPredict = 1  // Only generate 1 token to load model quickly
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("[OllamaService] Model warmed up successfully: {Model}", modelName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OllamaService] Model warm-up cancelled: {Model}", modelName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OllamaService] Failed to warm up model: {Model}", modelName);
            throw;
        }
    }

    /// <summary>
    /// Generate text using the specified model and prompt
    /// Returns the generated text as a string
    /// </summary>
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        double temperature = 0.3,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[OllamaService] Starting generation with model: {Model}, prompt length: {Length} chars",
                model, prompt.Length);

            var request = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Format = "json",  // Request JSON response for hierarchy generation
                Options = new OllamaOptions
                {
                    Temperature = temperature,
                    TopP = 0.9,
                    NumPredict = 4096
                }
            };

            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            var duration = DateTime.UtcNow - startTime;

            if (result == null)
            {
                throw new InvalidOperationException("Ollama returned null response");
            }

            _logger.LogInformation("[OllamaService] Generation completed in {Duration:F1}s, tokens: {Tokens}",
                duration.TotalSeconds, result.EvalCount);

            return result.Response;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OllamaService] Generation cancelled for model: {Model}", model);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OllamaService] Generation failed for model: {Model}", model);
            throw;
        }
    }
}

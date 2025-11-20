using PdfConversion.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Polly;
using Polly.CircuitBreaker;

namespace PdfConversion.Services;

/// <summary>
/// Client for the XSLT3Service (Saxon-HE XSLT 2.0/3.0 transformation engine)
/// </summary>
public interface IXslt3ServiceClient
{
    /// <summary>
    /// Transforms XML using XSLT 2.0/3.0 via the XSLT3Service
    /// </summary>
    Task<TransformationResult> TransformAsync(string xml, string xslt, Dictionary<string, string>? parameters = null);

    /// <summary>
    /// Checks if the XSLT3Service is available and responding
    /// </summary>
    Task<bool> IsServiceAvailableAsync();
}

/// <summary>
/// Implementation of XSLT3Service client with retry logic and circuit breaker
/// </summary>
public class Xslt3ServiceClient : IXslt3ServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Xslt3ServiceClient> _logger;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
    private const int MaxRetryAttempts = 3;
    private const int CircuitBreakerFailureThreshold = 5;
    private const int CircuitBreakerDurationSeconds = 30;

    public Xslt3ServiceClient(HttpClient httpClient, ILogger<Xslt3ServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Configure circuit breaker: open circuit after 5 failures, stay open for 30 seconds
        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                CircuitBreakerFailureThreshold,
                TimeSpan.FromSeconds(CircuitBreakerDurationSeconds),
                onBreak: (exception, duration) =>
                {
                    _logger.LogWarning("Circuit breaker opened for {Duration}s due to: {Error}",
                        duration.TotalSeconds, exception.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset - service is healthy again");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open - testing service health");
                });
    }

    public async Task<TransformationResult> TransformAsync(string xml, string xslt, Dictionary<string, string>? parameters = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new TransformationResult();

        try
        {
            _logger.LogDebug("Calling XSLT3Service for transformation");

            // Check circuit breaker state first
            if (_circuitBreakerPolicy.CircuitState == CircuitState.Open)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "XSLT3Service is currently unavailable (circuit breaker open)";
                result.WarningMessages.Add("Fallback to local XSLT 1.0 processor recommended");
                return result;
            }

            // Prepare multipart form content
            using var content = new MultipartFormDataContent();

            // Add XML file
            var xmlContent = new StringContent(xml, Encoding.UTF8, "application/xml");
            xmlContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(xmlContent, "xml", "input.xml");

            // Add XSLT file
            var xsltContent = new StringContent(xslt, Encoding.UTF8, "application/xml");
            xsltContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(xsltContent, "xsl", "transform.xsl");

            // Build query string with parameters
            var queryParams = new List<string>();

            // Add output formatting
            queryParams.Add("output=indent%3Dyes");

            // Add custom parameters
            if (parameters != null && parameters.Any())
            {
                var paramString = string.Join(",", parameters.Select(p => $"{p.Key}={p.Value}"));
                queryParams.Add($"parameters={Uri.EscapeDataString(paramString)}");
            }

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var requestUri = $"/transform{queryString}";

            // Execute with retry logic and circuit breaker
            var retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    MaxRetryAttempts,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Retry {RetryCount}/{MaxRetries} after {Delay}s due to: {Error}",
                            retryCount, MaxRetryAttempts, timeSpan.TotalSeconds, exception.Message);
                    });

            var policyWrap = Policy.WrapAsync(_circuitBreakerPolicy, retryPolicy);

            HttpResponseMessage response = await policyWrap.ExecuteAsync(async () =>
            {
                return await _httpClient.PostAsync(requestUri, content);
            });

            if (response.IsSuccessStatusCode)
            {
                result.OutputContent = await response.Content.ReadAsStringAsync();
                result.IsSuccess = true;
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("XSLT3Service transformation completed in {ElapsedMs}ms",
                    result.ProcessingTimeMs);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                result.IsSuccess = false;
                result.ErrorMessage = FormatErrorMessage(response.StatusCode.ToString(), errorContent);
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

                _logger.LogError("XSLT3Service transformation failed: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
            }
        }
        catch (BrokenCircuitException ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "XSLT3Service is temporarily unavailable (circuit breaker open)";
            result.WarningMessages.Add("Consider using fallback XSLT 1.0 processor");
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogWarning(ex, "Circuit breaker prevented call to XSLT3Service");
        }
        catch (HttpRequestException ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Failed to connect to XSLT3Service: {ex.Message}";
            result.WarningMessages.Add("XSLT3Service may be down or unreachable");
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "HTTP request to XSLT3Service failed");
        }
        catch (TaskCanceledException ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "XSLT3Service request timed out";
            result.WarningMessages.Add("Transformation may be too complex or service is overloaded");
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "XSLT3Service request timed out");
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Unexpected error calling XSLT3Service: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogError(ex, "Unexpected error during XSLT3Service call");
        }

        return result;
    }

    public async Task<bool> IsServiceAvailableAsync()
    {
        try
        {
            _logger.LogDebug("Checking XSLT3Service availability");

            // Use /health/readiness for dependency checks (more appropriate than /health)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync(
                "/health/readiness",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            var isAvailable = response.IsSuccessStatusCode;

            if (isAvailable)
            {
                _logger.LogInformation("XSLT3Service is available and ready");
            }
            else
            {
                _logger.LogWarning(
                    "XSLT3Service readiness check returned {StatusCode}",
                    response.StatusCode);
            }

            return isAvailable;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("XSLT3Service health check timed out after 5 seconds");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "XSLT3Service is unreachable");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XSLT3Service health check failed unexpectedly");
            return false;
        }
    }

    /// <summary>
    /// Formats error messages from XSLT3Service for user-friendly display
    /// </summary>
    private string FormatErrorMessage(string statusCode, string errorContent)
    {
        try
        {
            // Try to parse JSON error response
            using var doc = JsonDocument.Parse(errorContent);
            var root = doc.RootElement;

            // Extract error details
            var exceptionType = root.TryGetProperty("exceptionType", out var typeElement)
                ? typeElement.GetString() ?? "Error"
                : "Error";

            var message = root.TryGetProperty("message", out var msgElement)
                ? msgElement.GetString() ?? errorContent
                : errorContent;

            // Format nicely for toast display
            var formattedMessage = new StringBuilder();
            formattedMessage.AppendLine($"XSLT Transformation Error ({exceptionType})");
            formattedMessage.AppendLine();
            formattedMessage.AppendLine(message);

            return formattedMessage.ToString();
        }
        catch (JsonException)
        {
            // Not JSON or malformed - return as-is with status code
            return $"XSLT3Service Error ({statusCode}):\n\n{errorContent}";
        }
    }
}

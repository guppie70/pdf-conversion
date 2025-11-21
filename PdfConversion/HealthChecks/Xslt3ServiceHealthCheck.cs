using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace PdfConversion.HealthChecks;

/// <summary>
/// Health check for the XSLT3Service dependency
/// </summary>
public class Xslt3ServiceHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Xslt3ServiceHealthCheck> _logger;
    private const int HealthCheckTimeoutSeconds = 5;

    public Xslt3ServiceHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<Xslt3ServiceHealthCheck> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Xslt3ServiceHealthCheck");
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Performing XSLT3Service health check");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(HealthCheckTimeoutSeconds));

            var response = await _httpClient.GetAsync(
                "/health/readiness",
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);

                // Parse the response to get status
                var healthStatus = ParseHealthResponse(content);

                var data = new Dictionary<string, object>
                {
                    { "status", healthStatus },
                    { "endpoint", "/health/readiness" },
                    { "responseTime", $"{response.Headers.Date:yyyy-MM-dd HH:mm:ss}" }
                };

                _logger.LogDebug("XSLT3Service health check passed: {Status}", healthStatus);

                return HealthCheckResult.Healthy(
                    "XSLT3Service is responding and ready",
                    data);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cts.Token);

                _logger.LogWarning(
                    "XSLT3Service health check returned {StatusCode}: {Content}",
                    response.StatusCode,
                    errorContent);

                var data = new Dictionary<string, object>
                {
                    { "statusCode", (int)response.StatusCode },
                    { "error", errorContent },
                    { "endpoint", "/health/readiness" }
                };

                return HealthCheckResult.Unhealthy(
                    $"XSLT3Service returned {response.StatusCode}",
                    null,
                    data);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            _logger.LogWarning(
                "XSLT3Service health check timed out after {Timeout}s",
                HealthCheckTimeoutSeconds);

            var data = new Dictionary<string, object>
            {
                { "timeout", $"{HealthCheckTimeoutSeconds}s" },
                { "endpoint", "/health/readiness" }
            };

            return HealthCheckResult.Degraded(
                $"XSLT3Service health check timed out after {HealthCheckTimeoutSeconds}s",
                null,
                data);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "XSLT3Service health check failed due to connection error");

            var data = new Dictionary<string, object>
            {
                { "error", ex.Message },
                { "errorType", "HttpRequestException" },
                { "endpoint", "/health/readiness" }
            };

            return HealthCheckResult.Unhealthy(
                "XSLT3Service is unreachable",
                ex,
                data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XSLT3Service health check failed unexpectedly");

            var data = new Dictionary<string, object>
            {
                { "error", ex.Message },
                { "errorType", ex.GetType().Name },
                { "endpoint", "/health/readiness" }
            };

            return HealthCheckResult.Unhealthy(
                "XSLT3Service health check encountered an error",
                ex,
                data);
        }
    }

    private string ParseHealthResponse(string content)
    {
        try
        {
            // Expected format: {"status":"UP"}
            var jsonDoc = JsonDocument.Parse(content);
            if (jsonDoc.RootElement.TryGetProperty("status", out var statusElement))
            {
                return statusElement.GetString() ?? "UNKNOWN";
            }
            return "UNKNOWN";
        }
        catch
        {
            return "UNKNOWN";
        }
    }
}

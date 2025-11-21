using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using PdfConversion.HealthChecks;
using System.Net;
using Xunit;

namespace PdfConversion.Tests.HealthChecks;

public class Xslt3ServiceHealthCheckTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger<Xslt3ServiceHealthCheck>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public Xslt3ServiceHealthCheckTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger<Xslt3ServiceHealthCheck>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServiceIsHealthy_ReturnsHealthyResult()
    {
        // Arrange
        var responseContent = "{\"status\":\"UP\"}";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("XSLT3Service is responding and ready", result.Description);
        Assert.Contains("status", result.Data.Keys);
        Assert.Equal("UP", result.Data["status"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServiceReturns500_ReturnsUnhealthyResult()
    {
        // Arrange
        var errorContent = "Internal Server Error";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(errorContent)
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("XSLT3Service returned", result.Description);
        Assert.Contains("statusCode", result.Data.Keys);
        Assert.Equal(500, result.Data["statusCode"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServiceIsUnreachable_ReturnsUnhealthyResult()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("XSLT3Service is unreachable", result.Description);
        Assert.NotNull(result.Exception);
        Assert.Contains("error", result.Data.Keys);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTimeoutOccurs_ReturnsDegradedResult()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Operation timed out"));

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("timed out", result.Description);
        Assert.Contains("timeout", result.Data.Keys);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesCorrectEndpoint()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"UP\"}")
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal("/health/readiness", capturedRequest.RequestUri?.PathAndQuery);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
    }

    [Fact]
    public async Task CheckHealthAsync_ParsesJsonResponseCorrectly()
    {
        // Arrange
        var responseContent = "{\"status\":\"UP\",\"groups\":[\"liveness\",\"readiness\"]}";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("UP", result.Data["status"]);
        Assert.Equal("/health/readiness", result.Data["endpoint"]);
    }

    [Fact]
    public async Task CheckHealthAsync_HandlesInvalidJsonGracefully()
    {
        // Arrange
        var responseContent = "Not a valid JSON";
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent)
            });

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:4806")
        };

        _httpClientFactoryMock
            .Setup(f => f.CreateClient("Xslt3ServiceHealthCheck"))
            .Returns(httpClient);

        var healthCheck = new Xslt3ServiceHealthCheck(_httpClientFactoryMock.Object, _loggerMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal("UNKNOWN", result.Data["status"]); // Falls back to UNKNOWN for invalid JSON
    }
}

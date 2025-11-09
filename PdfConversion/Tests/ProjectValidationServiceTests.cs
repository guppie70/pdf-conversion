using Xunit;
using PdfConversion.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace PdfConversion.Tests;

public class ProjectValidationServiceTests
{
    private readonly ProjectValidationService _service;
    private readonly Mock<ILogger<ProjectValidationService>> _loggerMock;

    public ProjectValidationServiceTests()
    {
        _loggerMock = new Mock<ILogger<ProjectValidationService>>();
        _service = new ProjectValidationService(_loggerMock.Object);
    }

    [Theory]
    [InlineData("optiver", "ar24", true)]
    [InlineData("taxxor", "ar25-1", true)]
    [InlineData("test", "test-pdf", true)]
    [InlineData("../malicious", "path", false)]  // Path traversal attempt
    [InlineData("customer", "../../etc/passwd", false)]  // Path traversal
    [InlineData("", "ar24", false)]  // Empty customer
    [InlineData("optiver", "", false)]  // Empty projectId
    public void IsValidProjectParameters_ValidatesCorrectly(string customer, string projectId, bool expected)
    {
        var result = _service.IsValidProjectParameters(customer, projectId);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetProjectInputPath_ReturnsCorrectPath()
    {
        var path = _service.GetProjectInputPath("optiver", "ar24");
        Assert.Equal("/app/data/input/optiver/projects/ar24", path);
    }

    [Fact]
    public void GetProjectOutputPath_ReturnsCorrectPath()
    {
        var path = _service.GetProjectOutputPath("taxxor", "ar25-1");
        Assert.Equal("/app/data/output/taxxor/projects/ar25-1", path);
    }
}

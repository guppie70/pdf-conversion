using PdfConversion.Models;
using PdfConversion.Services;
using System.Text.Json;
using Xunit;

namespace PdfConversion.Tests.Integration;

public class ProjectMetadataServiceTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly ProjectMetadataService _service;

    public ProjectMetadataServiceTests()
    {
        _testFilePath = Path.Combine(Path.GetTempPath(), $"test-metadata-{Guid.NewGuid()}.json");
        _service = new ProjectMetadataService(_testFilePath);
    }

    [Fact]
    public async Task GetAllProjects_EmptyFile_ReturnsEmptyDictionary()
    {
        var projects = await _service.GetAllProjects();
        Assert.NotNull(projects);
        Assert.Empty(projects);
    }

    [Fact]
    public async Task UpdateProjectStatus_NewProject_CreatesMetadata()
    {
        await _service.UpdateProjectStatus("optiver", "ar24-1", ProjectLifecycleStatus.InProgress);

        var metadata = await _service.GetProjectMetadata("optiver", "ar24-1");
        Assert.NotNull(metadata);
        Assert.Equal(ProjectLifecycleStatus.InProgress, metadata.Status);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }
}

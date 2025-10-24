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

    [Fact]
    public async Task GetActiveProjects_FiltersOutReadyAndParked()
    {
        await _service.UpdateProjectStatus("optiver", "ar24-1", ProjectLifecycleStatus.Open);
        await _service.UpdateProjectStatus("optiver", "ar24-2", ProjectLifecycleStatus.InProgress);
        await _service.UpdateProjectStatus("optiver", "ar24-3", ProjectLifecycleStatus.Ready);
        await _service.UpdateProjectStatus("optiver", "ar24-4", ProjectLifecycleStatus.Parked);

        var active = await _service.GetActiveProjects();

        Assert.Equal(2, active["optiver"].Count);
        Assert.Contains("ar24-1", active["optiver"].Keys);
        Assert.Contains("ar24-2", active["optiver"].Keys);
        Assert.DoesNotContain("ar24-3", active["optiver"].Keys);
        Assert.DoesNotContain("ar24-4", active["optiver"].Keys);
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }
}

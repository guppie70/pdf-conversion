using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Integration tests for MetadataSyncService
/// Tests metadata synchronization with filesystem, preserving existing data, and thread safety
/// </summary>
public class MetadataSyncServiceTests : IDisposable
{
    private readonly Mock<IProjectDirectoryWatcherService> _mockDirectoryWatcher;
    private readonly Mock<IProjectLabelService> _mockLabelService;
    private readonly Mock<ILogger<MetadataSyncService>> _mockLogger;
    private bool _disposed;

    public MetadataSyncServiceTests()
    {
        _mockDirectoryWatcher = new Mock<IProjectDirectoryWatcherService>();
        _mockLabelService = new Mock<IProjectLabelService>();
        _mockLogger = new Mock<ILogger<MetadataSyncService>>();
    }

    [Fact]
    public async Task SyncMetadataAsync_WithNewProjects_ShouldAddToMetadata()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" },
            new() { Customer = "optiver", ProjectId = "ar24-2" },
            new() { Customer = "test", ProjectId = "test-1" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                data.Projects.ContainsKey("optiver") &&
                data.Projects["optiver"].ContainsKey("ar24-1") &&
                data.Projects["optiver"]["ar24-1"].Status == ProjectLifecycleStatus.Open &&
                data.Projects["optiver"]["ar24-1"].Label == "ar24-1" &&
                data.Projects["optiver"].ContainsKey("ar24-2") &&
                data.Projects["optiver"]["ar24-2"].Status == ProjectLifecycleStatus.Open &&
                data.Projects["test"].ContainsKey("test-1") &&
                data.Projects["test"]["test-1"].Status == ProjectLifecycleStatus.Open)),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_WithExistingProjects_ShouldPreserveCustomData()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingMetadata = new ProjectMetadata
        {
            Label = "Custom Annual Report 2024",
            Status = ProjectLifecycleStatus.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            LastModified = DateTime.UtcNow.AddDays(-2)
        };

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>
            {
                ["optiver"] = new Dictionary<string, ProjectMetadata>
                {
                    ["ar24-1"] = existingMetadata
                }
            }
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        // Verify SaveProjectLabelsDataAsync was NOT called (no changes needed)
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(It.IsAny<ProjectLabelsData>()),
            Times.Never);

        // Verify existing metadata was NOT modified
        Assert.Equal("Custom Annual Report 2024", existingMetadata.Label);
        Assert.Equal(ProjectLifecycleStatus.InProgress, existingMetadata.Status);
    }

    [Fact]
    public async Task SyncMetadataAsync_WithMixedProjects_ShouldAddOnlyMissing()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" }, // Existing
            new() { Customer = "optiver", ProjectId = "ar24-2" }, // New
            new() { Customer = "test", ProjectId = "test-1" },    // Existing
            new() { Customer = "test", ProjectId = "test-2" }     // New
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>
            {
                ["optiver"] = new Dictionary<string, ProjectMetadata>
                {
                    ["ar24-1"] = new ProjectMetadata
                    {
                        Label = "Existing Label 1",
                        Status = ProjectLifecycleStatus.Ready
                    }
                },
                ["test"] = new Dictionary<string, ProjectMetadata>
                {
                    ["test-1"] = new ProjectMetadata
                    {
                        Label = "Existing Label 2",
                        Status = ProjectLifecycleStatus.Parked
                    }
                }
            }
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                // Existing projects preserved
                data.Projects["optiver"]["ar24-1"].Label == "Existing Label 1" &&
                data.Projects["optiver"]["ar24-1"].Status == ProjectLifecycleStatus.Ready &&
                data.Projects["test"]["test-1"].Label == "Existing Label 2" &&
                data.Projects["test"]["test-1"].Status == ProjectLifecycleStatus.Parked &&
                // New projects added with defaults
                data.Projects["optiver"].ContainsKey("ar24-2") &&
                data.Projects["optiver"]["ar24-2"].Status == ProjectLifecycleStatus.Open &&
                data.Projects["test"].ContainsKey("test-2") &&
                data.Projects["test"]["test-2"].Status == ProjectLifecycleStatus.Open)),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_ConcurrentCalls_ShouldHandleThreadSafely()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" },
            new() { Customer = "optiver", ProjectId = "ar24-2" },
            new() { Customer = "optiver", ProjectId = "ar24-3" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act - Run 10 concurrent syncs
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.SyncMetadataAsync())
            .ToArray();

        // Should not throw exceptions
        await Task.WhenAll(tasks);

        // Assert - SaveProjectLabelsDataAsync should be called at least once
        // (some calls may be skipped due to semaphore, which is expected behavior)
        _mockLabelService.Verify(
            x => x.SaveProjectLabelsDataAsync(It.IsAny<ProjectLabelsData>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SyncMetadataAsync_WithNoProjectsOnFilesystem_ShouldNotModifyMetadata()
    {
        // Arrange
        var projects = new List<ProjectInfo>(); // Empty list

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>
            {
                ["optiver"] = new Dictionary<string, ProjectMetadata>
                {
                    ["ar24-1"] = new ProjectMetadata { Label = "Existing" }
                }
            }
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert - Should not save since no new projects were found
        _mockLabelService.Verify(
            x => x.SaveProjectLabelsDataAsync(It.IsAny<ProjectLabelsData>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncMetadataAsync_WithException_ShouldLogErrorAndNotThrow()
    {
        // Arrange
        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ThrowsAsync(new IOException("Filesystem error"));

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act - Should not throw
        await service.SyncMetadataAsync();

        // Assert - Should have logged error
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error syncing metadata")),
                It.IsAny<IOException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_WithNewCustomer_ShouldCreateCustomerEntry()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "newcustomer", ProjectId = "test-1" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                data.Projects.ContainsKey("newcustomer") &&
                data.Projects["newcustomer"].ContainsKey("test-1"))),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_DefaultLabelIsProjectId()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-5" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                data.Projects["optiver"]["ar24-5"].Label == "ar24-5")),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_DefaultStatusIsOpen()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        // Act
        await service.SyncMetadataAsync();

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                data.Projects["optiver"]["ar24-1"].Status == ProjectLifecycleStatus.Open)),
            Times.Once);
    }

    [Fact]
    public async Task SyncMetadataAsync_SetsTimestamps()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new() { Customer = "optiver", ProjectId = "ar24-1" }
        };

        _mockLabelService.Setup(x => x.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(projects);

        var existingData = new ProjectLabelsData
        {
            Projects = new Dictionary<string, Dictionary<string, ProjectMetadata>>()
        };

        _mockLabelService.Setup(x => x.GetProjectLabelsDataAsync())
            .ReturnsAsync(existingData);

        var service = new MetadataSyncService(
            _mockDirectoryWatcher.Object,
            _mockLabelService.Object,
            _mockLogger.Object);

        var beforeSync = DateTime.UtcNow;

        // Act
        await service.SyncMetadataAsync();

        var afterSync = DateTime.UtcNow;

        // Assert
        _mockLabelService.Verify(x => x.SaveProjectLabelsDataAsync(
            It.Is<ProjectLabelsData>(data =>
                data.Projects["optiver"]["ar24-1"].CreatedAt >= beforeSync &&
                data.Projects["optiver"]["ar24-1"].CreatedAt <= afterSync &&
                data.Projects["optiver"]["ar24-1"].LastModified >= beforeSync &&
                data.Projects["optiver"]["ar24-1"].LastModified <= afterSync &&
                data.LastModified >= beforeSync &&
                data.LastModified <= afterSync)),
            Times.Once);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _mockDirectoryWatcher.Object?.Dispose();
        _disposed = true;
    }
}

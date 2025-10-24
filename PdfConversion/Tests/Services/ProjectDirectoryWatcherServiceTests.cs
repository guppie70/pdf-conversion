using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Integration tests for ProjectDirectoryWatcherService
/// Tests filesystem watching, event firing, and debouncing behavior
/// </summary>
public class ProjectDirectoryWatcherServiceTests : IDisposable
{
    private readonly string _testBasePath;
    private readonly ProjectDirectoryWatcherService _service;
    private readonly Mock<ILogger<ProjectDirectoryWatcherService>> _mockLogger;
    private readonly Mock<IProjectLabelService> _mockLabelService;
    private readonly List<ProjectsChangedEventArgs> _eventsReceived;

    public ProjectDirectoryWatcherServiceTests()
    {
        // Create unique temp directory for each test run
        _testBasePath = Path.Combine(Path.GetTempPath(), $"test-watcher-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testBasePath);

        _mockLogger = new Mock<ILogger<ProjectDirectoryWatcherService>>();
        _mockLabelService = new Mock<IProjectLabelService>();

        // Setup mock to return test projects
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        _service = new ProjectDirectoryWatcherService(_mockLogger.Object, _mockLabelService.Object);

        _eventsReceived = new List<ProjectsChangedEventArgs>();
        _service.ProjectsChanged += (sender, e) => _eventsReceived.Add(e);
    }

    public void Dispose()
    {
        _service.Dispose();

        // Clean up test data
        if (Directory.Exists(_testBasePath))
        {
            try
            {
                Directory.Delete(_testBasePath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors (watchers may still have handles)
            }
        }
    }

    [Fact]
    public async Task StartWatching_NewProjectCreated_FiresEvent()
    {
        // Arrange
        var customerPath = Path.Combine(_testBasePath, "customer1", "projects");
        Directory.CreateDirectory(customerPath);

        var testProjects = new List<ProjectInfo>
        {
            new() { Customer = "customer1", ProjectId = "ar24-1", DisplayString = "AR 2024 (1) (ar24-1)" }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(_testBasePath))
            .ReturnsAsync(testProjects);

        _service.StartWatching(_testBasePath);

        // Wait for watchers to initialize
        await Task.Delay(200);

        _eventsReceived.Clear();

        // Act - Create new project directory
        var newProjectPath = Path.Combine(customerPath, "ar24-1");
        Directory.CreateDirectory(newProjectPath);

        // Wait for debounce (500ms) + processing time
        await Task.Delay(800);

        // Assert
        Assert.NotEmpty(_eventsReceived);
        var lastEvent = _eventsReceived.Last();
        Assert.NotNull(lastEvent.Projects);
        Assert.Equal(testProjects.Count, lastEvent.Projects.Count);
    }

    [Fact]
    public async Task StartWatching_ProjectDeleted_FiresEvent()
    {
        // Arrange
        var customerPath = Path.Combine(_testBasePath, "customer1", "projects");
        var projectPath = Path.Combine(customerPath, "ar24-1");
        Directory.CreateDirectory(projectPath);

        var emptyProjects = new List<ProjectInfo>();

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(_testBasePath))
            .ReturnsAsync(emptyProjects);

        _service.StartWatching(_testBasePath);

        // Wait for watchers to initialize
        await Task.Delay(200);

        _eventsReceived.Clear();

        // Act - Delete project directory
        Directory.Delete(projectPath, recursive: false);

        // Wait for debounce (500ms) + processing time
        await Task.Delay(800);

        // Assert
        Assert.NotEmpty(_eventsReceived);
        var lastEvent = _eventsReceived.Last();
        Assert.NotNull(lastEvent.Projects);
        Assert.Empty(lastEvent.Projects);
    }

    [Fact]
    public async Task DebouncesMultipleRapidChanges()
    {
        // Arrange
        var customerPath = Path.Combine(_testBasePath, "customer1", "projects");
        Directory.CreateDirectory(customerPath);

        var testProjects = new List<ProjectInfo>
        {
            new() { Customer = "customer1", ProjectId = "ar24-1", DisplayString = "AR 2024 (1) (ar24-1)" },
            new() { Customer = "customer1", ProjectId = "ar24-2", DisplayString = "AR 2024 (2) (ar24-2)" },
            new() { Customer = "customer1", ProjectId = "ar24-3", DisplayString = "AR 2024 (3) (ar24-3)" }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(_testBasePath))
            .ReturnsAsync(testProjects);

        _service.StartWatching(_testBasePath);

        // Wait for watchers to initialize
        await Task.Delay(200);

        _eventsReceived.Clear();

        // Act - Create multiple directories rapidly (within 500ms window)
        Directory.CreateDirectory(Path.Combine(customerPath, "ar24-1"));
        await Task.Delay(50);
        Directory.CreateDirectory(Path.Combine(customerPath, "ar24-2"));
        await Task.Delay(50);
        Directory.CreateDirectory(Path.Combine(customerPath, "ar24-3"));

        // Wait for debounce to complete (500ms) + processing time
        await Task.Delay(800);

        // Assert - Should only fire once due to debouncing
        // Allow for 1-2 events (timing may vary slightly)
        Assert.InRange(_eventsReceived.Count, 1, 2);

        var lastEvent = _eventsReceived.Last();
        Assert.Equal(3, lastEvent.Projects.Count);
    }

    [Fact]
    public void StopWatching_DisposesAllWatchers()
    {
        // Arrange
        var customer1Path = Path.Combine(_testBasePath, "customer1", "projects");
        var customer2Path = Path.Combine(_testBasePath, "customer2", "projects");
        Directory.CreateDirectory(customer1Path);
        Directory.CreateDirectory(customer2Path);

        _service.StartWatching(_testBasePath);

        // Act
        _service.StopWatching();

        // Assert - Service should stop without errors
        // Create a directory - should not trigger events
        _eventsReceived.Clear();

        var newProjectPath = Path.Combine(customer1Path, "ar24-1");
        Directory.CreateDirectory(newProjectPath);

        // Wait to ensure no events fired
        Thread.Sleep(1000);

        Assert.Empty(_eventsReceived);
    }

    [Fact]
    public async Task StartWatching_HandlesMultipleCustomers()
    {
        // Arrange
        var customer1Path = Path.Combine(_testBasePath, "customer1", "projects");
        var customer2Path = Path.Combine(_testBasePath, "customer2", "projects");
        Directory.CreateDirectory(customer1Path);
        Directory.CreateDirectory(customer2Path);

        var testProjects = new List<ProjectInfo>
        {
            new() { Customer = "customer1", ProjectId = "ar24-1", DisplayString = "AR 2024 (1) (ar24-1)" },
            new() { Customer = "customer2", ProjectId = "ar24-1", DisplayString = "AR 2024 (1) (ar24-1)" }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(_testBasePath))
            .ReturnsAsync(testProjects);

        _service.StartWatching(_testBasePath);

        // Wait for watchers to initialize
        await Task.Delay(200);

        _eventsReceived.Clear();

        // Act - Create project in customer1
        Directory.CreateDirectory(Path.Combine(customer1Path, "ar24-1"));

        // Wait for event
        await Task.Delay(800);

        var event1Count = _eventsReceived.Count;
        _eventsReceived.Clear();

        // Act - Create project in customer2
        Directory.CreateDirectory(Path.Combine(customer2Path, "ar24-1"));

        // Wait for event
        await Task.Delay(800);

        var event2Count = _eventsReceived.Count;

        // Assert - Both customers should trigger events
        Assert.NotEqual(0, event1Count);
        Assert.NotEqual(0, event2Count);
    }

    [Fact]
    public void StartWatching_NonExistentPath_LogsWarning()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testBasePath, "nonexistent");

        // Act
        _service.StartWatching(nonExistentPath);

        // Assert - Should log warning
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("does not exist")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProjectsChangedEvent_ContainsCorrectTimestamp()
    {
        // Arrange
        var customerPath = Path.Combine(_testBasePath, "customer1", "projects");
        Directory.CreateDirectory(customerPath);

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(_testBasePath))
            .ReturnsAsync(new List<ProjectInfo>());

        _service.StartWatching(_testBasePath);

        // Wait for watchers to initialize
        await Task.Delay(200);

        _eventsReceived.Clear();
        var beforeCreate = DateTime.UtcNow;

        // Act - Create new project directory
        Directory.CreateDirectory(Path.Combine(customerPath, "ar24-1"));

        // Wait for event
        await Task.Delay(800);
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotEmpty(_eventsReceived);
        var lastEvent = _eventsReceived.Last();
        Assert.InRange(lastEvent.Timestamp, beforeCreate, afterCreate);
    }
}

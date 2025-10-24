using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PdfConversion.Models;
using PdfConversion.Pages;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Components;

/// <summary>
/// Integration tests for Home page component
/// Tests project table rendering, loading states, and custom label display
/// </summary>
public class HomePageTests : TestContext
{
    private readonly Mock<IProjectLabelService> _mockLabelService;
    private readonly Mock<IProjectDirectoryWatcherService> _mockWatcherService;
    private readonly Mock<IXslt3ServiceClient> _mockXsltClient;
    private readonly ProjectMetadataService _metadataService;

    public HomePageTests()
    {
        _mockLabelService = new Mock<IProjectLabelService>();
        _mockWatcherService = new Mock<IProjectDirectoryWatcherService>();
        _mockXsltClient = new Mock<IXslt3ServiceClient>();
        _metadataService = new ProjectMetadataService(Path.Combine(Path.GetTempPath(), $"test-metadata-{Guid.NewGuid()}.json"));

        // Setup default mock behavior
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        _mockXsltClient
            .Setup(c => c.IsServiceAvailableAsync())
            .ReturnsAsync(true);

        // Register services
        Services.AddSingleton(_mockLabelService.Object);
        Services.AddSingleton(_mockWatcherService.Object);
        Services.AddSingleton(_mockXsltClient.Object);
        Services.AddSingleton(_metadataService);
    }

    [Fact]
    public void RendersProjectTableGroupedByCustomer()
    {
        // Arrange
        var testProjects = new List<ProjectInfo>
        {
            new()
            {
                Customer = "optiver",
                ProjectId = "ar24-3",
                CustomLabel = "Test Label",
                DisplayString = "Test Label (ar24-3)",
                FolderPath = "/test/optiver/projects/ar24-3"
            },
            new()
            {
                Customer = "optiver",
                ProjectId = "ar24-4",
                CustomLabel = null,
                DisplayString = "Annual Report 2024 (4) (ar24-4)",
                FolderPath = "/test/optiver/projects/ar24-4"
            },
            new()
            {
                Customer = "acme",
                ProjectId = "ar24-1",
                CustomLabel = "ACME Annual Report",
                DisplayString = "ACME Annual Report (ar24-1)",
                FolderPath = "/test/acme/projects/ar24-1"
            }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(testProjects);

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var customerHeaders = cut.FindAll("h2");
            Assert.True(customerHeaders.Count >= 2, "Should have at least 2 customer headers");
        }, TimeSpan.FromSeconds(2));

        // Assert - Verify customer sections exist
        var customerSections = cut.FindAll(".customer-section");
        Assert.Equal(2, customerSections.Count);

        // Assert - Verify both customer sections are present (alphabetically sorted)
        var customerHeaders = cut.FindAll("h2").Select(h => h.TextContent).ToList();
        Assert.Contains(customerHeaders, h => h.Contains("optiver"));
        Assert.Contains(customerHeaders, h => h.Contains("acme"));

        // Assert - Verify project IDs are displayed
        var projectIdCells = cut.FindAll(".project-id-cell code");
        Assert.True(projectIdCells.Count >= 3, "Should have at least 3 project ID cells");

        var projectIds = projectIdCells.Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("ar24-3", projectIds);
        Assert.Contains("ar24-4", projectIds);
        Assert.Contains("ar24-1", projectIds);
    }

    [Fact]
    public void DisplaysCorrectFormatForAllProjects()
    {
        // Arrange
        var testProjects = new List<ProjectInfo>
        {
            new()
            {
                Customer = "testcustomer",
                ProjectId = "ar24-1",
                CustomLabel = "Custom Label One",
                DisplayString = "Custom Label One (ar24-1)",
                FolderPath = "/test/path"
            },
            new()
            {
                Customer = "testcustomer",
                ProjectId = "ar24-2",
                CustomLabel = null,
                DisplayString = "Annual Report 2024 (2) (ar24-2)",
                FolderPath = "/test/path"
            },
            new()
            {
                Customer = "testcustomer",
                ProjectId = "xyz-123",
                CustomLabel = null,
                DisplayString = "xyz-123 (xyz-123)",
                FolderPath = "/test/path"
            }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(testProjects);

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var displayCells = cut.FindAll(".display-cell");
            Assert.True(displayCells.Count >= 3, "Should have at least 3 display cells");
        }, TimeSpan.FromSeconds(2));

        // Assert - Verify display format
        var editableLabels = cut.FindAll(".editable-label");
        Assert.Equal(3, editableLabels.Count);

        var displayStrings = editableLabels.Select(p => p.TextContent.Trim()).ToList();

        // All should follow format: "{label} ({id})"
        Assert.Contains("Custom Label One (ar24-1)", displayStrings);
        Assert.Contains("Annual Report 2024 (2) (ar24-2)", displayStrings);
        Assert.Contains("xyz-123 (xyz-123)", displayStrings);

        // Verify format pattern
        foreach (var displayString in displayStrings)
        {
            Assert.Matches(@"^.+\s\(.+\)$", displayString); // Pattern: "{text} ({id})"
        }
    }

    [Fact]
    public void EmptyState_ShowsHelpfulMessage()
    {
        // Arrange - Return empty list
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var emptyState = cut.Find(".empty-state");
            Assert.NotNull(emptyState);
        }, TimeSpan.FromSeconds(2));

        // Assert
        var emptyState = cut.Find(".empty-state");
        Assert.Contains("No projects found", emptyState.TextContent);
        Assert.Contains("data/input/{customer}/projects/", emptyState.TextContent);

        // Should also have an icon
        var icon = emptyState.QuerySelector("i.oi-folder");
        Assert.NotNull(icon);
    }

    [Fact]
    public void LoadingState_ShowsSpinner()
    {
        // Arrange - Create a slow-completing task
        var tcs = new TaskCompletionSource<List<ProjectInfo>>();
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .Returns(tcs.Task);

        // Act
        var cut = RenderComponent<Home>();

        // Assert - Should show loading state immediately
        var loadingState = cut.Find(".loading-state");
        Assert.NotNull(loadingState);

        var spinner = loadingState.QuerySelector(".spinner-border");
        Assert.NotNull(spinner);

        Assert.Contains("Loading projects", loadingState.TextContent);

        // Complete the task
        tcs.SetResult(new List<ProjectInfo>());
    }

    [Fact]
    public void ErrorState_ShowsErrorMessage()
    {
        // Arrange - Setup service to throw exception
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Test error message"));

        // Act
        var cut = RenderComponent<Home>();

        // Wait for error to be caught
        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find(".alert-danger");
            Assert.NotNull(alert);
        }, TimeSpan.FromSeconds(2));

        // Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("Error", errorAlert.TextContent);
        Assert.Contains("Test error message", errorAlert.TextContent);

        var icon = errorAlert.QuerySelector("i.oi-warning");
        Assert.NotNull(icon);
    }

    [Fact]
    public void SystemStatus_DisplaysXsltServiceConnection()
    {
        // Arrange
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        _mockXsltClient
            .Setup(c => c.IsServiceAvailableAsync())
            .ReturnsAsync(true);

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var statusCard = cut.Find(".system-status-card");
            Assert.NotNull(statusCard);
        }, TimeSpan.FromSeconds(2));

        // Assert
        var statusCard = cut.Find(".system-status-card");
        Assert.Contains("System Status", statusCard.TextContent);
        Assert.Contains("XSLT Service", statusCard.TextContent);

        // Should show connected badge
        var connectedBadge = statusCard.QuerySelector(".badge.bg-success");
        Assert.NotNull(connectedBadge);
        Assert.Contains("Connected", connectedBadge!.TextContent);
    }

    [Fact]
    public void SystemStatus_ShowsProjectCount()
    {
        // Arrange
        var testProjects = new List<ProjectInfo>
        {
            new() { Customer = "customer1", ProjectId = "ar24-1", DisplayString = "AR1 (ar24-1)", FolderPath = "/test" },
            new() { Customer = "customer1", ProjectId = "ar24-2", DisplayString = "AR2 (ar24-2)", FolderPath = "/test" },
            new() { Customer = "customer2", ProjectId = "ar24-3", DisplayString = "AR3 (ar24-3)", FolderPath = "/test" }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(testProjects);

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var statusCard = cut.Find(".system-status-card");
            Assert.Contains("Projects Available", statusCard.TextContent);
        }, TimeSpan.FromSeconds(2));

        // Assert
        var statusCard = cut.Find(".system-status-card");

        // Find all badges and locate the one containing the project count
        var badges = statusCard.QuerySelectorAll(".badge");
        var projectCountBadge = badges.FirstOrDefault(b => b.TextContent.Trim() == "3");

        Assert.NotNull(projectCountBadge);

        // Also verify the full "Projects Available" text includes the count
        Assert.Contains("Projects Available:", statusCard.TextContent);
        Assert.Contains("3", statusCard.TextContent);
    }

    [Fact]
    public void WatcherService_SubscribesToProjectsChangedEvent()
    {
        // Arrange
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        // Act
        var cut = RenderComponent<Home>();

        // Wait for initialization
        cut.WaitForAssertion(() =>
        {
            var statusCard = cut.Find(".system-status-card");
            Assert.NotNull(statusCard);
        }, TimeSpan.FromSeconds(2));

        // Assert - Verify StartWatching was called
        _mockWatcherService.Verify(
            s => s.StartWatching(It.IsAny<string>()),
            Times.Once);

        // Verify subscription to ProjectsChanged event
        _mockWatcherService.VerifyAdd(
            s => s.ProjectsChanged += It.IsAny<EventHandler<ProjectsChangedEventArgs>>(),
            Times.Once);
    }

    [Fact]
    public void LabelDisplay_ShowsEditableSpanByDefault()
    {
        // Arrange
        var testProjects = new List<ProjectInfo>
        {
            new()
            {
                Customer = "testcustomer",
                ProjectId = "ar24-1",
                CustomLabel = null,
                DisplayString = "ar24-1",
                FolderPath = "/test"
            }
        };

        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(testProjects);

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var editableLabels = cut.FindAll(".editable-label");
            Assert.NotEmpty(editableLabels);
        }, TimeSpan.FromSeconds(2));

        // Assert - Verify editable label is displayed
        var editableLabel = cut.Find(".editable-label");
        Assert.NotNull(editableLabel);
        Assert.Contains("ar24-1", editableLabel.TextContent);

        // Verify it has title attribute for double-click hint
        var title = editableLabel.GetAttribute("title");
        Assert.Equal("Double-click to edit", title);
    }

    [Fact]
    public void ProjectCount_ShowsCorrectBadgeColor()
    {
        // Arrange - Test with zero projects
        _mockLabelService
            .Setup(s => s.GetAllProjectsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<ProjectInfo>());

        // Act
        var cut = RenderComponent<Home>();

        // Wait for async initialization
        cut.WaitForAssertion(() =>
        {
            var statusCard = cut.Find(".system-status-card");
            Assert.Contains("Projects Available", statusCard.TextContent);
        }, TimeSpan.FromSeconds(2));

        // Assert - With zero projects, should show secondary badge
        var statusCard = cut.Find(".system-status-card");
        var projectCountText = statusCard.TextContent;

        // Should contain "Projects Available: 0"
        Assert.Contains("Projects Available", projectCountText);
        Assert.Contains("0", projectCountText);

        // Badge should be secondary (gray) for zero projects
        var badge = statusCard.QuerySelector(".badge.bg-secondary");
        Assert.NotNull(badge);
    }
}

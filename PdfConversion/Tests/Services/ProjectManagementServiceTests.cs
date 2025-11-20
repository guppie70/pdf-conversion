using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Integration tests for ProjectManagementService with custom label support
/// Tests the integration between ProjectManagementService and ProjectLabelService
/// </summary>
public class ProjectManagementServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _testInputPath;
    private readonly string _testOutputPath;
    private readonly ProjectManagementService _service;
    private readonly Mock<ILogger<ProjectManagementService>> _mockLogger;
    private readonly Mock<IProjectLabelService> _mockLabelService;
    private readonly IMemoryCache _cache;

    public ProjectManagementServiceTests()
    {
        // Create unique temp directory for each test run
        _testDataPath = Path.Combine(Path.GetTempPath(), $"test-project-mgmt-{Guid.NewGuid()}");
        _testInputPath = Path.Combine(_testDataPath, "input");
        _testOutputPath = Path.Combine(_testDataPath, "output");

        Directory.CreateDirectory(_testInputPath);
        Directory.CreateDirectory(_testOutputPath);

        // Create mocks
        _mockLogger = new Mock<ILogger<ProjectManagementService>>();
        _mockLabelService = new Mock<IProjectLabelService>();

        // Create memory cache with size limit
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 100
        };
        _cache = new MemoryCache(cacheOptions);

        // Create service with test dependencies
        _service = new ProjectManagementService(
            _mockLogger.Object,
            _cache,
            _mockLabelService.Object);

        // Use reflection to set the private path fields for testing
        SetPrivateField("_inputBasePath", _testInputPath);
        SetPrivateField("_outputBasePath", _testOutputPath);
    }

    public void Dispose()
    {
        _cache.Dispose();

        // Clean up test data
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, recursive: true);
        }
    }

    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(ProjectManagementService).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_service, value);
    }

    private void CreateTestProject(string organization, string projectId, params string[] files)
    {
        var projectPath = Path.Combine(_testInputPath, organization, "projects", projectId);
        Directory.CreateDirectory(projectPath);

        foreach (var fileName in files)
        {
            var filePath = Path.Combine(projectPath, fileName);
            File.WriteAllText(filePath, "<?xml version=\"1.0\"?><root/>");
        }
    }

    #region Integration Tests with Custom Labels

    [Fact]
    public async Task CreateProjectFromDirectory_UsesCustomLabel_WhenAvailable()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";
        var customLabel = "My Custom Annual Report";

        CreateTestProject(organization, projectId, "input.xml");

        // Setup mock to return custom label
        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(organization, projectId))
            .ReturnsAsync($"{customLabel} ({projectId})");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        var project = projects.FirstOrDefault(p => p.ProjectId == projectId && p.Organization == organization);
        Assert.NotNull(project);
        Assert.Equal($"{customLabel} ({projectId})", project.Name);
        _mockLabelService.Verify(s => s.GetDisplayStringAsync(organization, projectId), Times.Once);
    }

    [Fact]
    public async Task CreateProjectFromDirectory_UsesFallback_WhenNoLabel()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-3";
        var fallbackDisplayString = "Annual Report 2024 (3) (ar24-3)";

        CreateTestProject(organization, projectId, "input.xml");

        // Setup mock to return fallback display string (no custom label)
        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(organization, projectId))
            .ReturnsAsync(fallbackDisplayString);

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        var project = projects.FirstOrDefault(p => p.ProjectId == projectId && p.Organization == organization);
        Assert.NotNull(project);
        Assert.Equal(fallbackDisplayString, project.Name);
        _mockLabelService.Verify(s => s.GetDisplayStringAsync(organization, projectId), Times.Once);
    }

    [Fact]
    public async Task GetProjectsAsync_DisplaysCustomLabelsInAllProjects()
    {
        // Arrange - Create multiple projects with different label scenarios
        var projects = new[]
        {
            ("customer1", "ar24-1", "Custom Label One", "Custom Label One (ar24-1)"),
            ("customer1", "ar24-2", null, "Annual Report 2024 (2) (ar24-2)"), // fallback
            ("customer2", "xyz-123", null, "xyz-123 (xyz-123)"), // non-standard fallback
            ("customer2", "ar25-5", "2025 Annual Report", "2025 Annual Report (ar25-5)")
        };

        foreach (var (org, projId, customLabel, displayString) in projects)
        {
            CreateTestProject(org, projId, "input.xml");

            _mockLabelService
                .Setup(s => s.GetDisplayStringAsync(org, projId))
                .ReturnsAsync(displayString);
        }

        // Act
        var result = await _service.GetProjectsAsync();

        // Assert
        Assert.Equal(4, result.Count());

        var project1 = result.First(p => p.ProjectId == "ar24-1");
        var project2 = result.First(p => p.ProjectId == "ar24-2");
        var project3 = result.First(p => p.ProjectId == "xyz-123");
        var project4 = result.First(p => p.ProjectId == "ar25-5");

        Assert.Equal("Custom Label One (ar24-1)", project1.Name);
        Assert.Equal("Annual Report 2024 (2) (ar24-2)", project2.Name);
        Assert.Equal("xyz-123 (xyz-123)", project3.Name);
        Assert.Equal("2025 Annual Report (ar25-5)", project4.Name);

        // Verify all calls to label service
        _mockLabelService.Verify(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(4));
    }

    [Fact]
    public async Task GetProjectsAsync_HandlesMultipleOrganizations()
    {
        // Arrange
        var org1 = "optiver";
        var org2 = "acme";

        CreateTestProject(org1, "ar24-1", "input.xml");
        CreateTestProject(org1, "ar24-2", "input.xml");
        CreateTestProject(org2, "ar24-1", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string org, string projId) => $"{org} - {projId}");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        Assert.Equal(3, projects.Count());
        Assert.Equal(2, projects.Count(p => p.Organization == org1));
        Assert.Single(projects.Where(p => p.Organization == org2));

        var optiverProjects = projects.Where(p => p.Organization == org1).ToList();
        Assert.Contains(optiverProjects, p => p.ProjectId == "ar24-1");
        Assert.Contains(optiverProjects, p => p.ProjectId == "ar24-2");
    }

    [Fact]
    public async Task GetProjectAsync_WithOrganizationPrefix_ReturnsCorrectProject()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";
        var displayString = "Test Project (ar24-1)";

        CreateTestProject(organization, projectId, "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(organization, projectId))
            .ReturnsAsync(displayString);

        // Act - Use full format: "organization/projectId"
        var project = await _service.GetProjectAsync($"{organization}/{projectId}");

        // Assert
        Assert.NotNull(project);
        Assert.Equal(organization, project.Organization);
        Assert.Equal(projectId, project.ProjectId);
        Assert.Equal(displayString, project.Name);
    }

    [Fact]
    public async Task GetProjectAsync_LegacyFormat_StillWorks()
    {
        // Arrange
        var organization = "optiver"; // Default organization
        var projectId = "ar24-1";
        var displayString = "Legacy Project (ar24-1)";

        CreateTestProject(organization, projectId, "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(organization, projectId))
            .ReturnsAsync(displayString);

        // Act - Use legacy format (just projectId, no organization)
        var project = await _service.GetProjectAsync(projectId);

        // Assert
        Assert.NotNull(project);
        Assert.Equal(organization, project.Organization);
        Assert.Equal(projectId, project.ProjectId);
        Assert.Equal(displayString, project.Name);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task GetProjectsAsync_CachesResults()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act - Call twice
        var projects1 = await _service.GetProjectsAsync();
        var projects2 = await _service.GetProjectsAsync();

        // Assert - Label service should only be called once (first time)
        Assert.Equal(projects1.Count(), projects2.Count());
        _mockLabelService.Verify(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SaveOutputAsync_InvalidatesCache()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act - Get projects, save output, get projects again
        var projects1 = await _service.GetProjectsAsync();
        await _service.SaveOutputAsync("testorg/ar24-1", "<output>test</output>");
        var projects2 = await _service.GetProjectsAsync();

        // Assert - Label service should be called twice (cache invalidated)
        _mockLabelService.Verify(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    #endregion

    #region File Management Tests

    [Fact]
    public async Task GetProjectFilesAsync_ReturnsXmlAndHtmlFiles()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";

        CreateTestProject(organization, projectId, "input.xml", "test.html", "readme.txt");

        // Act
        var files = await _service.GetProjectFilesAsync($"{organization}/{projectId}");

        // Assert
        Assert.Equal(2, files.Count()); // Only .xml and .html files
        Assert.Contains("input.xml", files);
        Assert.Contains("test.html", files);
        Assert.DoesNotContain("readme.txt", files);
    }

    [Fact]
    public async Task ReadInputFileAsync_ReadsFileContent()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";
        var fileName = "input.xml";
        var expectedContent = "<?xml version=\"1.0\"?><test>content</test>";

        var projectPath = Path.Combine(_testInputPath, organization, "projects", projectId);
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, fileName), expectedContent);

        // Act
        var content = await _service.ReadInputFileAsync($"{organization}/{projectId}", fileName);

        // Assert
        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task SaveOutputAsync_CreatesOutputDirectory()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";
        var outputContent = "<output>test</output>";

        CreateTestProject(organization, projectId); // Input directory only

        // Act
        await _service.SaveOutputAsync($"{organization}/{projectId}", outputContent);

        // Assert
        var outputPath = Path.Combine(_testOutputPath, organization, "projects", projectId);
        Assert.True(Directory.Exists(outputPath));

        var outputFile = Path.Combine(outputPath, "taxxor.xhtml");
        Assert.True(File.Exists(outputFile));

        var savedContent = await File.ReadAllTextAsync(outputFile);
        Assert.Equal(outputContent, savedContent);
    }

    [Fact]
    public async Task ProjectExistsAsync_ReturnsTrueForExistingProject()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";
        CreateTestProject(organization, projectId);

        // Act
        var exists = await _service.ProjectExistsAsync($"{organization}/{projectId}");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ProjectExistsAsync_ReturnsFalseForNonExistentProject()
    {
        // Arrange - Don't create any projects

        // Act
        var exists = await _service.ProjectExistsAsync("testorg/nonexistent");

        // Assert
        Assert.False(exists);
    }

    #endregion

    #region Project Status Tests

    [Fact]
    public async Task GetProjectsAsync_SetsStatusToNotStartedWhenNoOutput()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        var project = projects.First();
        Assert.Equal(ProjectStatus.NotStarted, project.Status);
        Assert.Null(project.LastProcessedDate);
    }

    [Fact]
    public async Task GetProjectsAsync_SetsStatusToCompletedWhenOutputExists()
    {
        // Arrange
        var organization = "testorg";
        var projectId = "ar24-1";

        CreateTestProject(organization, projectId, "input.xml");

        // Create output file
        var outputPath = Path.Combine(_testOutputPath, organization, "projects", projectId);
        Directory.CreateDirectory(outputPath);
        var outputFile = Path.Combine(outputPath, "taxxor.xhtml");
        File.WriteAllText(outputFile, "<output/>");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        var project = projects.First();
        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.NotNull(project.LastProcessedDate);
    }

    [Fact]
    public async Task GetProjectsAsync_SetsFileCount()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "file1.xml", "file2.xml", "file3.html");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        var project = projects.First();
        Assert.Equal(3, project.FileCount);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GetProjectsAsync_HandlesLabelServiceError_GracefullySkipsProject()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "input.xml");
        CreateTestProject("testorg", "ar24-2", "input.xml");

        // Setup mock to throw exception for first project, succeed for second
        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync("testorg", "ar24-1"))
            .ThrowsAsync(new Exception("Label service error"));

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync("testorg", "ar24-2"))
            .ReturnsAsync("Test (ar24-2)");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert - Should have 1 project (ar24-2 succeeded)
        // ar24-1 threw exception in CreateProjectFromDirectoryAsync, returned null, and was filtered out
        Assert.Single(projects);
        Assert.Equal("ar24-2", projects.First().ProjectId);
        Assert.Equal("Test (ar24-2)", projects.First().Name);

        // Verify that label service was called for both projects
        _mockLabelService.Verify(s => s.GetDisplayStringAsync("testorg", "ar24-1"), Times.Once);
        _mockLabelService.Verify(s => s.GetDisplayStringAsync("testorg", "ar24-2"), Times.Once);
    }

    [Fact]
    public async Task ReadInputFileAsync_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1"); // No files

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _service.ReadInputFileAsync("testorg/ar24-1", "nonexistent.xml"));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsEmptyList_WhenInputPathDoesNotExist()
    {
        // Arrange - Delete input path
        Directory.Delete(_testInputPath, recursive: true);

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        Assert.Empty(projects);
    }

    #endregion

    #region Organization Scanning Tests

    [Fact]
    public async Task GetProjectsAsync_SkipsHiddenDirectories()
    {
        // Arrange
        CreateTestProject("testorg", "ar24-1", "input.xml");
        CreateTestProject(".hidden", "ar24-2", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string org, string projId) => $"{projId}");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert - Should only have project from non-hidden organization
        Assert.Single(projects);
        Assert.Equal("testorg", projects.First().Organization);
    }

    [Fact]
    public async Task GetProjectsAsync_SkipsOrganizationsWithoutProjectsFolder()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_testInputPath, "org-no-projects"));
        CreateTestProject("org-with-projects", "ar24-1", "input.xml");

        _mockLabelService
            .Setup(s => s.GetDisplayStringAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("Test (ar24-1)");

        // Act
        var projects = await _service.GetProjectsAsync();

        // Assert
        Assert.Single(projects);
        Assert.Equal("org-with-projects", projects.First().Organization);
    }

    #endregion
}

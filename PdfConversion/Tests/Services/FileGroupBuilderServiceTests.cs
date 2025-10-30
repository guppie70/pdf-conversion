using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using System.Reflection;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Unit tests for FileGroupBuilderService
/// Tests file group building logic with actual file system operations using temp directories
/// </summary>
public class FileGroupBuilderServiceTests : IDisposable
{
    private readonly Mock<IProjectManagementService> _mockProjectService;
    private readonly Mock<ProjectMetadataService> _mockMetadataService;
    private readonly Mock<ILogger<FileGroupBuilderService>> _mockLogger;
    private readonly FileGroupBuilderService _service;
    private readonly string _testDataPath;
    private readonly string _testInputPath;
    private readonly string _testOutputPath;

    public FileGroupBuilderServiceTests()
    {
        // Create unique temp directory for each test run
        _testDataPath = Path.Combine(Path.GetTempPath(), $"test-file-group-{Guid.NewGuid()}");
        _testInputPath = Path.Combine(_testDataPath, "input");
        _testOutputPath = Path.Combine(_testDataPath, "output");

        Directory.CreateDirectory(_testInputPath);
        Directory.CreateDirectory(_testOutputPath);

        _mockProjectService = new Mock<IProjectManagementService>();
        _mockMetadataService = new Mock<ProjectMetadataService>(
            "/tmp/test-metadata.json",
            null);
        _mockLogger = new Mock<ILogger<FileGroupBuilderService>>();

        _service = new FileGroupBuilderService(
            _mockProjectService.Object,
            _mockMetadataService.Object,
            _mockLogger.Object);

        // Use reflection to set the private path fields for testing
        SetPrivateField("_inputBasePath", _testInputPath);
        SetPrivateField("_outputBasePath", _testOutputPath);
    }

    public void Dispose()
    {
        // Clean up test data
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(FileGroupBuilderService).GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(_service, value);
    }

    #region BuildXmlFileGroupsAsync Tests

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WithInputFiles_ReturnsGroupsWithRelativePaths()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        // Create test files
        CreateXmlFile("optiver", "ar24-1", "input.xml");
        CreateXmlFile("optiver", "ar24-2", "source.xml");
        CreateXmlFile("taxxor", "ar25-1", "data.xml");

        // Act
        var result = await _service.BuildXmlFileGroupsAsync(
            includeInputFiles: true,
            includeOutputFiles: false,
            onlyActiveProjects: true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count); // Only optiver projects (ar24-1, ar24-2) are active

        var optiverGroup1 = result.First(g => g.ProjectId == "ar24-1");
        Assert.Equal("optiver", optiverGroup1.Customer);
        Assert.Single(optiverGroup1.Files);
        Assert.Equal("input.xml", optiverGroup1.Files[0].FileName);
        Assert.Equal("optiver/ar24-1/input.xml", optiverGroup1.Files[0].FullPath);
    }

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WithOutputFiles_ReturnsGroupsWithOutputFiles()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        // Create output files
        CreateXmlFile("optiver", "ar24-1", "normalized.xml", isOutput: true);
        CreateXmlFile("optiver", "ar24-1", "hierarchy.xml", isOutput: true);

        // Act
        var result = await _service.BuildXmlFileGroupsAsync(
            includeInputFiles: false,
            includeOutputFiles: true,
            onlyActiveProjects: true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result); // Only ar24-1 has output files

        var group = result[0];
        Assert.Equal("optiver", group.Customer);
        Assert.Equal("ar24-1", group.ProjectId);
        Assert.Equal(2, group.Files.Count);
        Assert.Contains(group.Files, f => f.FileName == "normalized.xml");
        Assert.Contains(group.Files, f => f.FileName == "hierarchy.xml");
    }

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WithBothInputAndOutput_CombinesFiles()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        CreateXmlFile("optiver", "ar24-1", "input.xml");
        CreateXmlFile("optiver", "ar24-1", "normalized.xml", isOutput: true);

            // Act
            var result = await _service.BuildXmlFileGroupsAsync(
                includeInputFiles: true,
                includeOutputFiles: true,
                onlyActiveProjects: true);

            // Assert
            Assert.Single(result);
            var group = result[0];
            Assert.Equal(2, group.Files.Count);
    }

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WithOnlyActiveProjectsFalse_ReturnsAllProjects()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        CreateXmlFile("optiver", "ar24-1", "input.xml");
        CreateXmlFile("optiver", "ar24-2", "source.xml");
        CreateXmlFile("taxxor", "ar25-1", "data.xml");

            // Act
            var result = await _service.BuildXmlFileGroupsAsync(
                includeInputFiles: true,
                includeOutputFiles: false,
                onlyActiveProjects: false);

            // Assert
            Assert.Equal(3, result.Count); // All projects included
    }

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WithNoXmlFiles_ReturnsEmptyGroups()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

            // Create directories but no XML files
        CreateProjectDirectory("optiver", "ar24-1");

            // Act
            var result = await _service.BuildXmlFileGroupsAsync();

            // Assert
            Assert.Empty(result);
    }

    #endregion

    #region BuildDocumentFileGroupsAsync Tests

    [Fact]
    public async Task BuildDocumentFileGroupsAsync_WithPdfExtension_ReturnsAbsolutePaths()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        CreateDocumentFile("optiver", "ar24-1", "report.pdf");
        CreateDocumentFile("optiver", "ar24-2", "annual-report.pdf");

            // Act
            var result = await _service.BuildDocumentFileGroupsAsync(
                new[] { ".pdf" },
                onlyActiveProjects: true);

            // Assert
            Assert.Equal(2, result.Count);
            foreach (var group in result)
            {
                Assert.All(group.Files, file =>
                {
                    Assert.StartsWith("/app/data/input/", file.FullPath);
                    Assert.EndsWith(".pdf", file.FullPath);
                });
            }
    }

    [Fact]
    public async Task BuildDocumentFileGroupsAsync_WithMultipleExtensions_ReturnsAllMatchingFiles()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        CreateDocumentFile("optiver", "ar24-1", "report.pdf");
        CreateDocumentFile("optiver", "ar24-1", "notes.docx");
        CreateDocumentFile("optiver", "ar24-1", "legacy.doc");

            // Act
            var result = await _service.BuildDocumentFileGroupsAsync(
                new[] { ".pdf", ".docx", ".doc" },
                onlyActiveProjects: true);

            // Assert
            Assert.Single(result);
            var group = result[0];
            Assert.Equal(3, group.Files.Count);
            Assert.Contains(group.Files, f => f.FileName == "report.pdf");
            Assert.Contains(group.Files, f => f.FileName == "notes.docx");
            Assert.Contains(group.Files, f => f.FileName == "legacy.doc");
    }

    [Fact]
    public async Task BuildDocumentFileGroupsAsync_WithNullExtensions_ReturnsEmptyList()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        // Act
        var result = await _service.BuildDocumentFileGroupsAsync(null, onlyActiveProjects: true);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildDocumentFileGroupsAsync_WithEmptyExtensions_ReturnsEmptyList()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        // Act
        var result = await _service.BuildDocumentFileGroupsAsync(
            Array.Empty<string>(),
            onlyActiveProjects: true);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region BuildAllFileGroupsAsync Tests

    [Fact]
    public async Task BuildAllFileGroupsAsync_ReturnsAllFilesWithRelativePaths()
    {
        // Arrange
        var projects = CreateTestProjects();
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ReturnsAsync(projects);

        var activeProjects = CreateActiveProjectMetadata();
        _mockMetadataService.Setup(s => s.GetActiveProjects())
            .ReturnsAsync(activeProjects);

        CreateXmlFile("optiver", "ar24-1", "input.xml");
        CreateDocumentFile("optiver", "ar24-1", "report.pdf");
        CreateDocumentFile("optiver", "ar24-1", "notes.docx");

            // Act
            var result = await _service.BuildAllFileGroupsAsync(onlyActiveProjects: true);

            // Assert
            Assert.Single(result);
            var group = result[0];
            Assert.Equal(3, group.Files.Count);
            Assert.All(group.Files, file =>
            {
                Assert.Matches(@"^optiver/ar24-1/.+$", file.FullPath);
            });
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task BuildXmlFileGroupsAsync_WhenExceptionThrown_ReturnsEmptyList()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.BuildXmlFileGroupsAsync();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildDocumentFileGroupsAsync_WhenExceptionThrown_ReturnsEmptyList()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.BuildDocumentFileGroupsAsync(new[] { ".pdf" });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task BuildAllFileGroupsAsync_WhenExceptionThrown_ReturnsEmptyList()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.BuildAllFileGroupsAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Helper Methods

    private List<Project> CreateTestProjects()
    {
        return new List<Project>
        {
            new Project
            {
                Organization = "optiver",
                ProjectId = "ar24-1",
                Name = "Optiver AR 2024 Report 1",
                InputPath = "/app/data/input/optiver/projects/ar24-1",
                OutputPath = "/app/data/output/optiver/projects/ar24-1"
            },
            new Project
            {
                Organization = "optiver",
                ProjectId = "ar24-2",
                Name = "Optiver AR 2024 Report 2",
                InputPath = "/app/data/input/optiver/projects/ar24-2",
                OutputPath = "/app/data/output/optiver/projects/ar24-2"
            },
            new Project
            {
                Organization = "taxxor",
                ProjectId = "ar25-1",
                Name = "Taxxor AR 2025 Report 1",
                InputPath = "/app/data/input/taxxor/projects/ar25-1",
                OutputPath = "/app/data/output/taxxor/projects/ar25-1"
            }
        };
    }

    private Dictionary<string, Dictionary<string, ProjectMetadata>> CreateActiveProjectMetadata()
    {
        return new Dictionary<string, Dictionary<string, ProjectMetadata>>
        {
            ["optiver"] = new Dictionary<string, ProjectMetadata>
            {
                ["ar24-1"] = new ProjectMetadata
                {
                    Label = "Optiver AR 2024 Report 1",
                    Status = ProjectLifecycleStatus.Open,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                },
                ["ar24-2"] = new ProjectMetadata
                {
                    Label = "Optiver AR 2024 Report 2",
                    Status = ProjectLifecycleStatus.InProgress,
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                }
            }
        };
    }

    private void CreateProjectDirectory(string organization, string projectId)
    {
        var inputPath = Path.Combine(_testInputPath, organization, "projects", projectId);
        Directory.CreateDirectory(inputPath);

        var outputPath = Path.Combine(_testOutputPath, organization, "projects", projectId);
        Directory.CreateDirectory(outputPath);
    }

    private void CreateXmlFile(string organization, string projectId, string fileName, bool isOutput = false)
    {
        var basePath = isOutput ? _testOutputPath : _testInputPath;
        var filePath = Path.Combine(basePath, organization, "projects", projectId);
        Directory.CreateDirectory(filePath);

        var fullPath = Path.Combine(filePath, fileName);
        File.WriteAllText(fullPath, "<?xml version=\"1.0\"?><root></root>");
    }

    private void CreateDocumentFile(string organization, string projectId, string fileName)
    {
        var filePath = Path.Combine(_testInputPath, organization, "projects", projectId);
        Directory.CreateDirectory(filePath);

        var fullPath = Path.Combine(filePath, fileName);
        File.WriteAllBytes(fullPath, new byte[] { 0x25, 0x50, 0x44, 0x46 }); // PDF header
    }

    #endregion
}

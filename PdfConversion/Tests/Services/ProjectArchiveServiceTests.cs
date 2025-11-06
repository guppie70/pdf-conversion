using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Services;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Integration tests for ProjectArchiveService with hierarchy selection and manifest generation
/// </summary>
public class ProjectArchiveServiceTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly string _testInputPath;
    private readonly string _testOutputPath;
    private readonly ProjectArchiveService _service;
    private readonly Mock<ILogger<ProjectArchiveService>> _mockLogger;

    public ProjectArchiveServiceTests()
    {
        // Create unique temp directory for each test run
        _testDataPath = Path.Combine(Path.GetTempPath(), $"test-archive-{Guid.NewGuid()}");
        _testInputPath = Path.Combine(_testDataPath, "input");
        _testOutputPath = Path.Combine(_testDataPath, "output");

        Directory.CreateDirectory(_testInputPath);
        Directory.CreateDirectory(_testOutputPath);

        // Create mock logger
        _mockLogger = new Mock<ILogger<ProjectArchiveService>>();

        // Create service
        _service = new ProjectArchiveService(_mockLogger.Object);

        // Use reflection to set the private path fields for testing
        SetPrivateField("_inputBasePath", _testInputPath);
        SetPrivateField("_outputBasePath", _testOutputPath);
    }

    public void Dispose()
    {
        // Clean up test data
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, recursive: true);
        }
    }

    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(ProjectArchiveService).GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_service, value);
    }

    private void CreateTestProjectStructure(string customer, string projectId)
    {
        // Create input structure (metadata, images)
        var inputProjectPath = Path.Combine(_testInputPath, customer, "projects", projectId);
        var metadataPath = Path.Combine(inputProjectPath, "metadata");
        var imagesPath = Path.Combine(inputProjectPath, "images");

        Directory.CreateDirectory(metadataPath);
        Directory.CreateDirectory(imagesPath);

        // Create output structure (sections)
        var outputProjectPath = Path.Combine(_testOutputPath, customer, "projects", projectId);
        var dataPath = Path.Combine(outputProjectPath, "data");

        Directory.CreateDirectory(dataPath);
    }

    private void CreateHierarchyFile(string customer, string projectId, string fileName, string content = null)
    {
        var metadataPath = Path.Combine(_testInputPath, customer, "projects", projectId, "metadata");
        Directory.CreateDirectory(metadataPath);

        var hierarchyContent = content ?? "<?xml version=\"1.0\"?><hierarchy><item>Test</item></hierarchy>";
        File.WriteAllText(Path.Combine(metadataPath, fileName), hierarchyContent);
    }

    private void CreateSectionFile(string customer, string projectId, string fileName)
    {
        var dataPath = Path.Combine(_testOutputPath, customer, "projects", projectId, "data");
        Directory.CreateDirectory(dataPath);

        var sectionContent = $"<?xml version=\"1.0\"?><section><content>{fileName}</content></section>";
        File.WriteAllText(Path.Combine(dataPath, fileName), sectionContent);
    }

    private void CreateImageFile(string customer, string projectId, string relativePath)
    {
        var imagesPath = Path.Combine(_testInputPath, customer, "projects", projectId, "images");
        var fullPath = Path.Combine(imagesPath, relativePath);

        // Create subdirectories if needed
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        // Create dummy image file
        File.WriteAllText(fullPath, "dummy image content");
    }

    #region CreateProjectArchiveAsync Tests

    [Fact]
    public async Task CreateProjectArchiveAsync_WithValidHierarchy_CreatesZipWithManifest()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "ar24-1";
        var hierarchyFileName = "hierarchy-manual.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        CreateSectionFile(customer, projectId, "section-1.xml");
        CreateSectionFile(customer, projectId, "section-2.xml");
        CreateImageFile(customer, projectId, "image1.png");
        CreateImageFile(customer, projectId, "subfolder/image2.jpg");

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes);
        Assert.True(zipBytes.Length > 0);

        // Verify ZIP contents
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = archive.Entries.Select(e => e.FullName).ToList();

        // Check for manifest
        Assert.Contains("manifest.yml", entries);

        // Check for hierarchy
        Assert.Contains($"metadata/{hierarchyFileName}", entries);

        // Check for sections
        Assert.Contains("data/section-1.xml", entries);
        Assert.Contains("data/section-2.xml", entries);

        // Check for images
        Assert.Contains("images/image1.png", entries);
        Assert.Contains("images/subfolder/image2.jpg", entries);

        // Verify manifest content
        var manifestEntry = archive.GetEntry("manifest.yml");
        Assert.NotNull(manifestEntry);

        using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        Assert.Contains($"file: metadata/{hierarchyFileName}", manifestContent);
        Assert.Contains("count: 2", manifestContent); // 2 sections
        Assert.Contains("- data/section-1.xml", manifestContent);
        Assert.Contains("- data/section-2.xml", manifestContent);
        Assert.Contains("- images/image1.png", manifestContent);
        Assert.Contains("- images/subfolder/image2.jpg", manifestContent);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithMultipleHierarchies_IncludesOnlySelected()
    {
        // Arrange
        var customer = "acme";
        var projectId = "ar25-1";
        var selectedHierarchy = "hierarchy-manual.xml";
        var otherHierarchy1 = "hierarchy-rules.xml";
        var otherHierarchy2 = "hierarchy-ai.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, selectedHierarchy, "<?xml version=\"1.0\"?><hierarchy type=\"manual\"/>");
        CreateHierarchyFile(customer, projectId, otherHierarchy1, "<?xml version=\"1.0\"?><hierarchy type=\"rules\"/>");
        CreateHierarchyFile(customer, projectId, otherHierarchy2, "<?xml version=\"1.0\"?><hierarchy type=\"ai\"/>");
        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, selectedHierarchy);

        // Assert
        Assert.NotNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = archive.Entries.Select(e => e.FullName).ToList();

        // Should include ONLY the selected hierarchy
        Assert.Contains($"metadata/{selectedHierarchy}", entries);
        Assert.DoesNotContain($"metadata/{otherHierarchy1}", entries);
        Assert.DoesNotContain($"metadata/{otherHierarchy2}", entries);

        // Verify manifest references correct hierarchy
        var manifestEntry = archive.GetEntry("manifest.yml");
        using var manifestStream = manifestEntry!.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        Assert.Contains($"file: metadata/{selectedHierarchy}", manifestContent);
        Assert.DoesNotContain(otherHierarchy1, manifestContent);
        Assert.DoesNotContain(otherHierarchy2, manifestContent);

        // Verify the selected hierarchy has correct content
        var hierarchyEntry = archive.GetEntry($"metadata/{selectedHierarchy}");
        using var hierarchyStream = hierarchyEntry!.Open();
        using var hierarchyReader = new StreamReader(hierarchyStream);
        var hierarchyContent = await hierarchyReader.ReadToEndAsync();

        Assert.Contains("type=\"manual\"", hierarchyContent);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_GeneratesValidManifestYaml()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "test-123";
        var hierarchyFileName = "hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        CreateSectionFile(customer, projectId, "section-a.xml");
        CreateSectionFile(customer, projectId, "section-b.xml");
        CreateSectionFile(customer, projectId, "section-c.xml");
        CreateImageFile(customer, projectId, "chart.png");

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.yml");
        Assert.NotNull(manifestEntry);

        using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        // Verify structure
        Assert.Contains("# Taxxor TDM Package Manifest", manifestContent);
        Assert.Contains("# Generated by PDF Conversion Tool", manifestContent);

        // Verify hierarchy section
        Assert.Contains("hierarchy:", manifestContent);
        Assert.Contains($"  file: metadata/{hierarchyFileName}", manifestContent);

        // Verify sections section
        Assert.Contains("sections:", manifestContent);
        Assert.Contains("  count: 3", manifestContent);
        Assert.Contains("  files:", manifestContent);
        Assert.Contains("    - data/section-a.xml", manifestContent);
        Assert.Contains("    - data/section-b.xml", manifestContent);
        Assert.Contains("    - data/section-c.xml", manifestContent);

        // Verify images section
        Assert.Contains("images:", manifestContent);
        Assert.Contains("  count: 1", manifestContent);
        Assert.Contains("    - images/chart.png", manifestContent);

        // Verify sorting (sections should be alphabetically ordered)
        var sectionAIndex = manifestContent.IndexOf("data/section-a.xml");
        var sectionBIndex = manifestContent.IndexOf("data/section-b.xml");
        var sectionCIndex = manifestContent.IndexOf("data/section-c.xml");
        Assert.True(sectionAIndex < sectionBIndex);
        Assert.True(sectionBIndex < sectionCIndex);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithMissingHierarchy_LogsWarning()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "ar24-2";
        var nonExistentHierarchy = "missing-hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, nonExistentHierarchy);

        // Assert
        Assert.NotNull(zipBytes); // Should still create archive

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Hierarchy file not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        // Verify manifest shows "none" for hierarchy
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.yml");
        using var manifestStream = manifestEntry!.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        Assert.Contains("file: none", manifestContent);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithEmptySections_CreatesValidManifest()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "empty-project";
        var hierarchyFileName = "hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        // No section files created

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.yml");
        using var manifestStream = manifestEntry!.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        // Verify sections section shows empty
        Assert.Contains("sections:", manifestContent);
        Assert.Contains("  count: 0", manifestContent);
        Assert.Contains("  files:", manifestContent);
        Assert.Contains("    []", manifestContent);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithEmptyImages_CreatesValidManifest()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "no-images";
        var hierarchyFileName = "hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        CreateSectionFile(customer, projectId, "section-1.xml");
        // No image files created

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.yml");
        using var manifestStream = manifestEntry!.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        // Verify images section shows empty
        Assert.Contains("images:", manifestContent);
        Assert.Contains("  count: 0", manifestContent);
        Assert.Contains("  files:", manifestContent);
        Assert.Contains("    []", manifestContent);
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithMissingDirectories_CreatesEmptyZip()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "empty-project";
        var hierarchyFileName = "hierarchy.xml";

        // Don't create any structure - service will create empty ZIP

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes); // Service creates empty ZIP, not null
        Assert.True(zipBytes.Length > 0);

        // Verify ZIP contains only manifest (no files)
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var manifestEntry = archive.GetEntry("manifest.yml");
        Assert.NotNull(manifestEntry);

        // Verify manifest shows all empty
        using var manifestStream = manifestEntry.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        Assert.Contains("file: none", manifestContent); // No hierarchy
        Assert.Contains("count: 0", manifestContent); // No sections
    }

    [Fact]
    public async Task CreateProjectArchiveAsync_WithNestedImageDirectories_PreservesStructure()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "nested-images";
        var hierarchyFileName = "hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        CreateSectionFile(customer, projectId, "section-1.xml");
        CreateImageFile(customer, projectId, "root-image.png");
        CreateImageFile(customer, projectId, "charts/bar-chart.png");
        CreateImageFile(customer, projectId, "charts/pie-chart.png");
        CreateImageFile(customer, projectId, "logos/company-logo.svg");
        CreateImageFile(customer, projectId, "deep/nested/structure/image.jpg");

        // Act
        var zipBytes = await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert
        Assert.NotNull(zipBytes);

        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = archive.Entries.Select(e => e.FullName).ToList();

        // Verify all nested paths are preserved
        Assert.Contains("images/root-image.png", entries);
        Assert.Contains("images/charts/bar-chart.png", entries);
        Assert.Contains("images/charts/pie-chart.png", entries);
        Assert.Contains("images/logos/company-logo.svg", entries);
        Assert.Contains("images/deep/nested/structure/image.jpg", entries);

        // Verify manifest lists all images
        var manifestEntry = archive.GetEntry("manifest.yml");
        using var manifestStream = manifestEntry!.Open();
        using var reader = new StreamReader(manifestStream);
        var manifestContent = await reader.ReadToEndAsync();

        Assert.Contains("count: 5", manifestContent);
    }

    #endregion

    #region GetArchiveFilename Tests

    [Fact]
    public void GetArchiveFilename_ReturnsCorrectFormat()
    {
        // Arrange
        var customer = "acme";
        var projectId = "ar24-1";

        // Act
        var filename = _service.GetArchiveFilename(customer, projectId);

        // Assert
        Assert.Equal("acme-ar24-1.zip", filename);
    }

    [Fact]
    public void GetArchiveFilename_HandlesSpecialCharacters()
    {
        // Arrange
        var customer = "test_corp";
        var projectId = "ar-2024-Q1";

        // Act
        var filename = _service.GetArchiveFilename(customer, projectId);

        // Assert
        Assert.Equal("test_corp-ar-2024-Q1.zip", filename);
    }

    #endregion

    #region HasFilesToArchiveAsync Tests

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsTrueWhenSectionsAndHierarchyExist()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "complete-project";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, "hierarchy.xml");
        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.True(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsFalseWhenNoSections()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "no-sections";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, "hierarchy.xml");
        // No sections created

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.False(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsFalseWhenNoHierarchy()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "no-hierarchy";

        CreateTestProjectStructure(customer, projectId);
        CreateSectionFile(customer, projectId, "section-1.xml");
        // No hierarchy created

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.False(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsFalseWhenProjectDoesNotExist()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "nonexistent";

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.False(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsTrueWithHierarchyInMetadataFolder()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "metadata-hierarchy";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, "hierarchy-manual.xml"); // In metadata folder
        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.True(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_ReturnsTrueWithLegacyHierarchyInRoot()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "legacy-hierarchy";

        CreateTestProjectStructure(customer, projectId);

        // Create legacy hierarchy.xml in root input folder
        var inputProjectPath = Path.Combine(_testInputPath, customer, "projects", projectId);
        File.WriteAllText(Path.Combine(inputProjectPath, "hierarchy.xml"),
            "<?xml version=\"1.0\"?><hierarchy/>");

        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.True(hasFiles);
    }

    [Fact]
    public async Task HasFilesToArchiveAsync_WithInvalidPaths_ReturnsFalse()
    {
        // Arrange - Use invalid path characters (on Windows would throw, on Unix just returns false)
        var customer = "test|invalid";
        var projectId = "project?";

        // Act
        var hasFiles = await _service.HasFilesToArchiveAsync(customer, projectId);

        // Assert
        Assert.False(hasFiles);

        // Note: On Unix systems, invalid path characters don't throw exceptions,
        // they just result in paths that don't exist. The service handles this
        // gracefully by returning false without logging errors.
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task CreateProjectArchiveAsync_LogsInformationMessages()
    {
        // Arrange
        var customer = "testcorp";
        var projectId = "log-test";
        var hierarchyFileName = "hierarchy.xml";

        CreateTestProjectStructure(customer, projectId);
        CreateHierarchyFile(customer, projectId, hierarchyFileName);
        CreateSectionFile(customer, projectId, "section-1.xml");

        // Act
        await _service.CreateProjectArchiveAsync(customer, projectId, hierarchyFileName);

        // Assert - Verify logging calls
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating archive")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Created archive")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

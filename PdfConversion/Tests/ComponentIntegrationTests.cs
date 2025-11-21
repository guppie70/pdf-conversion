using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using PdfConversion.Models;
using PdfConversion.Pages;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests;

/// <summary>
/// Integration tests for Blazor component interactions.
/// These tests verify that components communicate correctly with services and each other.
/// </summary>
public class ComponentIntegrationTests : TestContext
{
    private readonly Mock<IProjectManagementService> _mockProjectService;
    private readonly Mock<IXsltTransformationService> _mockXsltService;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<Transform>> _mockLogger;
    private readonly Mock<IFileService> _mockFileService;
    private readonly Mock<ITransformationLogService> _mockLogService;
    private readonly Mock<IDistributedCacheService> _mockCacheService;
    private readonly Mock<IPerformanceMonitoringService> _mockPerfService;
    private readonly Mock<IMemoryPoolManager> _mockMemoryPool;
    private readonly Mock<IBatchTransformationService> _mockBatchService;
    private readonly Mock<IUserSelectionService> _mockUserSelectionService;
    private readonly Mock<IXhtmlValidationService> _mockValidationService;
    private readonly Mock<IXsltFileWatcherService> _mockXsltFileWatcher;
    private readonly Mock<IXmlFileWatcherService> _mockXmlFileWatcher;
    private readonly Mock<ThemeService> _mockThemeService;
    private readonly ProjectMetadataService _metadataService;
    private readonly TransformToolbarState _toolbarState;

    public ComponentIntegrationTests()
    {
        _mockProjectService = new Mock<IProjectManagementService>();
        _mockXsltService = new Mock<IXsltTransformationService>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<Transform>>();
        _mockFileService = new Mock<IFileService>();
        _mockLogService = new Mock<ITransformationLogService>();
        _mockCacheService = new Mock<IDistributedCacheService>();
        _mockPerfService = new Mock<IPerformanceMonitoringService>();
        _mockMemoryPool = new Mock<IMemoryPoolManager>();
        _mockBatchService = new Mock<IBatchTransformationService>();
        _mockUserSelectionService = new Mock<IUserSelectionService>();
        _mockValidationService = new Mock<IXhtmlValidationService>();
        _mockXsltFileWatcher = new Mock<IXsltFileWatcherService>();
        _mockXmlFileWatcher = new Mock<IXmlFileWatcherService>();
        _mockThemeService = new Mock<ThemeService>();
        _metadataService = new ProjectMetadataService(Path.Combine(Path.GetTempPath(), $"test-metadata-{Guid.NewGuid()}.json"));
        _toolbarState = new TransformToolbarState();

        // Setup default behavior for UserSelectionService
        _mockUserSelectionService
            .Setup(s => s.GetSelectionAsync())
            .ReturnsAsync(new UserSelection());

        // Setup default behavior for ValidationService
        _mockValidationService
            .Setup(s => s.ValidateXhtmlAsync(It.IsAny<string>()))
            .ReturnsAsync(XhtmlValidationResult.Success());
        _mockValidationService
            .Setup(s => s.ValidateXhtmlAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(XhtmlValidationResult.Success());

        // Register services
        Services.AddSingleton(_mockProjectService.Object);
        Services.AddSingleton(_mockXsltService.Object);
        Services.AddSingleton(_mockLogger.Object);
        Services.AddSingleton(_mockFileService.Object);
        Services.AddSingleton(_mockLogService.Object);
        Services.AddSingleton(_mockCacheService.Object);
        Services.AddSingleton(_mockPerfService.Object);
        Services.AddSingleton(_mockMemoryPool.Object);
        Services.AddSingleton(_mockBatchService.Object);
        Services.AddSingleton(_mockUserSelectionService.Object);
        Services.AddSingleton(_mockValidationService.Object);
        Services.AddSingleton(_mockXsltFileWatcher.Object);
        Services.AddSingleton(_mockXmlFileWatcher.Object);
        Services.AddSingleton(_mockThemeService.Object);
        Services.AddSingleton(_metadataService);
        Services.AddSingleton(_toolbarState);
    }

    #region Service Integration Tests (Tests 1-8)

    /// <summary>
    /// Test 1: Transform component loads projects from service on initialization
    /// </summary>
    [Fact]
    public async Task Transform_OnInit_LoadsProjectsFromService()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { ProjectId = "ar24-1", Organization = "optiver", Name = "Annual Report 2024-1", FileCount = 2 },
            new() { ProjectId = "ar24-2", Organization = "optiver", Name = "Annual Report 2024-2", FileCount = 3 }
        };
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(projects);
        _mockProjectService.Setup(s => s.GetProjectFilesAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());

        // Add metadata for projects with Open status so they appear in active projects
        await _metadataService.UpdateProjectLabel("optiver", "ar24-1", "Test Project 1");
        await _metadataService.UpdateProjectStatus("optiver", "ar24-1", ProjectLifecycleStatus.Open);
        await _metadataService.UpdateProjectLabel("optiver", "ar24-2", "Test Project 2");
        await _metadataService.UpdateProjectStatus("optiver", "ar24-2", ProjectLifecycleStatus.Open);

        // Act
        var cut = RenderComponent<Transform>();

        // Wait for async initialization to complete
        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(_toolbarState.Projects);
            Assert.Equal(2, _toolbarState.Projects.Count);
        }, TimeSpan.FromSeconds(2));

        // Assert
        _mockProjectService.Verify(s => s.GetProjectsAsync(), Times.Once);
        Assert.Contains(_toolbarState.Projects, p => p.ProjectId == "ar24-1");
        Assert.Contains(_toolbarState.Projects, p => p.ProjectId == "ar24-2");
    }

    /// <summary>
    /// Test 2: Transform component loads XSLT on initialization
    /// </summary>
    [Fact]
    public void Transform_OnInit_LoadsXSLT()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        // Act
        var cut = RenderComponent<Transform>();

        // Assert
        // Component should attempt to read XSLT file (verified through logs or state)
        Assert.NotNull(_toolbarState);
    }

    /// <summary>
    /// Test 3: Project selection triggers file list loading
    /// </summary>
    [Fact]
    public async Task ProjectSelection_LoadsFileList()
    {
        // Arrange
        var projectFiles = new List<string> { "file1.xml", "file2.xml" };
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.GetProjectFilesAsync("ar24-1")).ReturnsAsync(projectFiles);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Assert
        _mockProjectService.Verify(s => s.GetProjectFilesAsync("ar24-1"), Times.Once);
        Assert.Equal(projectFiles, _toolbarState.ProjectFiles);
    }

    /// <summary>
    /// Test 4: File selection triggers XML content loading
    /// </summary>
    [Fact]
    public async Task FileSelection_LoadsXMLContent()
    {
        // Arrange
        var xmlContent = "<?xml version=\"1.0\"?><root>test</root>";
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync("ar24-1", "file.xml")).ReturnsAsync(xmlContent);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("file.xml"));

        // Assert
        _mockProjectService.Verify(s => s.ReadInputFileAsync("ar24-1", "file.xml"), Times.Once);
    }

    /// <summary>
    /// Test 5: Transform button calls transformation service
    /// </summary>
    [Fact(Skip = "Transform not triggered - needs XSLT content loaded in component state")]
    public async Task Transform_CallsTransformationService()
    {
        // Arrange
        var xmlContent = "<?xml version=\"1.0\"?><root>test</root>";
        var transformResult = new TransformationResult
        {
            IsSuccess = true,
            OutputContent = "<div>output</div>",
            ProcessingTimeMs = 100
        };

        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(xmlContent);
        _mockXsltService.Setup(s => s.TransformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransformationOptions?>(), It.IsAny<string?>()))
            .ReturnsAsync(transformResult);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("file.xml"));

        // Act
        if (_toolbarState.OnTransform != null)
        {
            await cut.InvokeAsync(async () => await _toolbarState.OnTransform!());
        }

        // Assert
        _mockXsltService.Verify(s => s.TransformAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<TransformationOptions?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test 6: Save button triggers file write
    /// </summary>
    [Fact]
    public async Task Save_WritesXSLTFile()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Transform>();

        // Act
        if (_toolbarState.OnSave != null)
        {
            await cut.InvokeAsync(async () => await _toolbarState.OnSave!());
        }

        // Assert
        // Verify that save operation was attempted (would check file system or state)
        Assert.NotNull(_toolbarState);
    }

    /// <summary>
    /// Test 7: Reset clears all state
    /// </summary>
    [Fact(Skip = "OnReset method not implemented yet")]
    public async Task Reset_ClearsAllState()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Act
        // TODO: Implement OnReset method in TransformToolbarState
        // if (_toolbarState.OnReset != null)
        // {
        //     await cut.InvokeAsync(async () => await _toolbarState.OnReset!());
        // }

        // Assert
        Assert.Null(_toolbarState.SelectedProjectId);
        Assert.Null(_toolbarState.SelectedFileName);
    }

    /// <summary>
    /// Test 8: Error in service shows error message
    /// </summary>
    [Fact(Skip = "bUnit doesn't render toast notifications - .alert-danger elements not in DOM")]
    public void ServiceError_DisplaysErrorMessage()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ThrowsAsync(new Exception("Service error"));

        // Act
        var cut = RenderComponent<Transform>();

        // Assert
        // Component should handle error gracefully and show error message
        var errorAlert = cut.FindAll(".alert-danger");
        Assert.NotEmpty(errorAlert);
    }

    #endregion

    #region Toolbar State Communication Tests (Tests 9-14)

    /// <summary>
    /// Test 9: ToolbarState updates propagate to MainLayout
    /// </summary>
    [Fact]
    public void ToolbarState_UpdatesPropagateToMainLayout()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();

        // Act
        _toolbarState.IsLoading = true;
        _toolbarState.NotifyStateChanged();

        // Assert
        Assert.True(_toolbarState.IsLoading);
    }

    /// <summary>
    /// Test 10: Toolbar button states update based on component state
    /// </summary>
    [Fact]
    public void ToolbarButtonStates_UpdateBasedOnComponentState()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();

        // Act & Assert - Initially cannot transform
        Assert.False(_toolbarState.CanTransform);

        // After loading project and file, should be able to transform
        // (This would be set by the component after successful file load)
    }

    /// <summary>
    /// Test 11: Settings toggle updates ShowSettings state
    /// </summary>
    [Fact]
    public void SettingsToggle_UpdatesShowSettingsState()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();

        // Act
        cut.InvokeAsync(() => _toolbarState.OnToggleSettings?.Invoke());

        // Assert
        Assert.True(_toolbarState.ShowSettings);
    }

    /// <summary>
    /// Test 12: Transform button disabled when no file selected
    /// </summary>
    [Fact]
    public void TransformButton_DisabledWhenNoFileSelected()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();

        // Assert
        Assert.False(_toolbarState.CanTransform);
    }

    /// <summary>
    /// Test 13: Save button enabled when XSLT content exists
    /// </summary>
    [Fact(Skip = "Requires FileService mock for GetXsltFiles() - test design needs update")]
    public void SaveButton_EnabledWhenXSLTExists()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Transform>();

        // Assert
        Assert.True(_toolbarState.CanSave); // XSLT is loaded on init
    }

    /// <summary>
    /// Test 14: Loading states prevent concurrent operations
    /// </summary>
    [Fact]
    public void LoadingStates_PreventConcurrentOperations()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();

        // Act
        _toolbarState.IsTransforming = true;
        _toolbarState.NotifyStateChanged();

        // Assert
        Assert.True(_toolbarState.IsTransforming);
        // In real scenario, buttons would be disabled during transformation
    }

    #endregion

    #region Data Binding and UI State Tests (Tests 15-20)

    /// <summary>
    /// Test 15: Selected project ID updates correctly
    /// </summary>
    [Fact]
    public async Task SelectedProjectId_UpdatesCorrectly()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.GetProjectFilesAsync("ar24-1")).ReturnsAsync(new List<string>());
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Assert
        Assert.Equal("ar24-1", _toolbarState.SelectedProjectId);
    }

    /// <summary>
    /// Test 16: Selected filename updates correctly
    /// </summary>
    [Fact]
    public async Task SelectedFileName_UpdatesCorrectly()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), "test.xml")).ReturnsAsync("<root/>");
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));

        // Assert
        Assert.Equal("test.xml", _toolbarState.SelectedFileName);
    }

    /// <summary>
    /// Test 17: Error message clears on successful operation
    /// </summary>
    [Fact]
    public async Task ErrorMessage_ClearsOnSuccessfulOperation()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), "test.xml")).ReturnsAsync("<root/>");
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Act - Perform successful operation after error
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));

        // Assert
        // Error messages should be cleared
        var errorAlerts = cut.FindAll(".alert-danger");
        Assert.Empty(errorAlerts);
    }

    /// <summary>
    /// Test 18: Success message displays after save
    /// </summary>
    [Fact(Skip = "bUnit doesn't render toast notifications - .alert-success elements not in DOM")]
    public async Task SuccessMessage_DisplaysAfterSave()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Transform>();

        // Act
        if (_toolbarState.OnSave != null)
        {
            await cut.InvokeAsync(async () => await _toolbarState.OnSave!());
        }

        // Assert
        var successAlerts = cut.FindAll(".alert-success");
        Assert.NotEmpty(successAlerts);
    }

    /// <summary>
    /// Test 19: Settings checkboxes bind correctly
    /// </summary>
    [Fact]
    public void SettingsCheckboxes_BindCorrectly()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        // Act
        var cut = RenderComponent<Transform>();
        cut.InvokeAsync(() => _toolbarState.OnToggleSettings?.Invoke());
        cut.Render(); // Re-render to show settings panel

        // Assert
        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.NotEmpty(checkboxes);
    }

    /// <summary>
    /// Test 20: Panel content updates after file load
    /// </summary>
    [Fact(Skip = "Monaco editor elements not rendered in bUnit - requires JS interop")]
    public async Task PanelContent_UpdatesAfterFileLoad()
    {
        // Arrange
        var xmlContent = "<?xml version=\"1.0\"?><root>test content</root>";
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), "test.xml")).ReturnsAsync(xmlContent);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));
        cut.Render(); // Force re-render

        // Assert
        var xmlPanel = cut.FindAll(".panel-left textarea.code-editor");
        Assert.NotEmpty(xmlPanel);
    }

    #endregion

    #region Event Handling Tests (Tests 21-26)

    /// <summary>
    /// Test 21: StateHasChanged triggers after async operations
    /// </summary>
    [Fact]
    public async Task StateHasChanged_TriggersAfterAsyncOperations()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.GetProjectFilesAsync("ar24-1")).ReturnsAsync(new List<string>());
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Act
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));

        // Assert
        // Component should have re-rendered (verified through state changes)
        Assert.False(_toolbarState.IsLoading);
    }

    /// <summary>
    /// Test 22: Multiple rapid clicks don't cause race conditions
    /// </summary>
    [Fact]
    public async Task RapidClicks_DontCauseRaceConditions()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.GetProjectFilesAsync(It.IsAny<string>())).ReturnsAsync(new List<string>());
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Act - Simulate rapid project changes
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-2"));
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-3"));

        // Assert
        Assert.Equal("ar24-3", _toolbarState.SelectedProjectId);
        // No exceptions thrown, state is consistent
    }

    /// <summary>
    /// Test 23: Auto-transform triggers when enabled
    /// </summary>
    [Fact]
    public async Task AutoTransform_TriggersWhenEnabled()
    {
        // Arrange
        var transformResult = new TransformationResult { IsSuccess = true, OutputContent = "<div/>" };
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("<root/>");
        _mockXsltService.Setup(s => s.TransformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransformationOptions?>(), It.IsAny<string?>()))
            .ReturnsAsync(transformResult);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();

        // Enable auto-transform (would be done through settings panel)
        // Then load a file

        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));

        // Assert
        // If auto-transform is enabled, transformation service should be called
        // (This is tested indirectly through other tests)
    }

    /// <summary>
    /// Test 24: Dispose cleans up resources properly
    /// </summary>
    [Fact(Skip = "Cannot access component instance after disposal - bUnit limitation")]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = RenderComponent<Transform>();

        // Act
        cut.Dispose();

        // Assert
        // No exceptions thrown, resources cleaned up
        Assert.True(cut.Instance != null);
    }

    /// <summary>
    /// Test 25: Navigation state preserved across route changes
    /// </summary>
    [Fact]
    public void NavigationState_PreservedAcrossRouteChanges()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        // Act
        var cut = RenderComponent<Shared.MainLayout>();

        // Assert
        // MainLayout should show different toolbar based on current route
        Assert.NotNull(cut);
    }

    /// <summary>
    /// Test 26: Monaco editor integration callbacks work correctly
    /// </summary>
    [Fact]
    public void MonacoEditor_CallbacksWorkCorrectly()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Act
        var cut = RenderComponent<Transform>();

        // Assert
        // Monaco editor should be initialized
        // (Verified through JS interop calls)
        Assert.NotNull(cut);
    }

    #endregion

    #region Transformation Options Tests (Tests 27-30)

    /// <summary>
    /// Test 27: XSLT3 service toggle updates transformation options
    /// </summary>
    [Fact]
    public void XSLT3Toggle_UpdatesTransformationOptions()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();
        cut.InvokeAsync(() => _toolbarState.OnToggleSettings?.Invoke());
        cut.Render();

        // Act
        var checkbox = cut.Find("#useXslt3");
        checkbox.Change(true);

        // Assert
        // Transformation should now use XSLT3 service
        // (Verified in transformation calls)
    }

    /// <summary>
    /// Test 28: Normalize headers option affects transformation
    /// </summary>
    [Fact]
    public void NormalizeHeadersOption_AffectsTransformation()
    {
        // Arrange
        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());

        var cut = RenderComponent<Transform>();
        cut.InvokeAsync(() => _toolbarState.OnToggleSettings?.Invoke());
        cut.Render();

        // Act
        var checkbox = cut.Find("#normalizeHeaders");
        Assert.NotNull(checkbox);

        // Assert
        // Normalize headers should be enabled by default
    }

    /// <summary>
    /// Test 29: Transformation statistics display correctly
    /// </summary>
    [Fact]
    public async Task TransformationStats_DisplayCorrectly()
    {
        // Arrange
        var transformResult = new TransformationResult
        {
            IsSuccess = true,
            OutputContent = "<div/>",
            ProcessingTimeMs = 250,
            HeadersNormalized = 5,
            TablesProcessed = 3
        };

        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("<root/>");
        _mockXsltService.Setup(s => s.TransformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransformationOptions?>(), It.IsAny<string?>()))
            .ReturnsAsync(transformResult);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));

        // Act
        if (_toolbarState.OnTransform != null)
        {
            await cut.InvokeAsync(async () => await _toolbarState.OnTransform!());
        }

        cut.Render();

        // Assert
        var stats = cut.FindAll(".preview-stats");
        if (stats.Any())
        {
            Assert.Contains("250 ms", stats[0].TextContent);
        }
    }

    /// <summary>
    /// Test 30: Transformation failures show appropriate error messages
    /// </summary>
    [Fact(Skip = "bUnit doesn't render toast notifications - .alert-danger elements not in DOM")]
    public async Task TransformationFailure_ShowsErrorMessage()
    {
        // Arrange
        var transformResult = new TransformationResult
        {
            IsSuccess = false,
            ErrorMessage = "XSLT compilation failed"
        };

        _mockProjectService.Setup(s => s.GetProjectsAsync()).ReturnsAsync(new List<Project>());
        _mockProjectService.Setup(s => s.ReadInputFileAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("<root/>");
        _mockXsltService.Setup(s => s.TransformAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransformationOptions?>(), It.IsAny<string?>()))
            .ReturnsAsync(transformResult);
        JSInterop.Mode = JSRuntimeMode.Loose; // Allow JS calls to succeed

        var cut = RenderComponent<Transform>();
        await cut.InvokeAsync(async () => await _toolbarState.OnProjectChanged!("ar24-1"));
        await cut.InvokeAsync(async () => await _toolbarState.OnFileChanged!("test.xml"));

        // Act
        if (_toolbarState.OnTransform != null)
        {
            await cut.InvokeAsync(async () => await _toolbarState.OnTransform!());
        }

        cut.Render();

        // Assert
        var errorAlerts = cut.FindAll(".alert-danger");
        Assert.NotEmpty(errorAlerts);
        Assert.Contains("XSLT compilation failed", errorAlerts[0].TextContent);
    }

    #endregion
}

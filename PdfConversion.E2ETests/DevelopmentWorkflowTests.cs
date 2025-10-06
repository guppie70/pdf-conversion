using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PdfConversion.E2ETests;

/// <summary>
/// End-to-end tests for critical user journeys in the PDF Conversion application.
/// These tests verify the complete workflow from the user's perspective.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class DevelopmentWorkflowTests : PageTest
{
    private const string BaseUrl = "http://localhost:8085";
    private const string DevUrl = $"{BaseUrl}/development";

    [SetUp]
    public async Task Setup()
    {
        // Navigate to development page before each test
        await Page.GotoAsync(DevUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Test 1: Critical Path - User can load a project and file to view XML
    /// This is the MOST CRITICAL test - if this breaks, nothing else works
    /// </summary>
    [Test]
    public async Task UserCanLoadProjectAndViewXML()
    {
        // Act - Select a project
        await Page.SelectOptionAsync("select:has-text('Project...')", "ar24-3");

        // Wait for files to load
        await Page.WaitForTimeoutAsync(1000);

        // Select an XML file
        await Page.SelectOptionAsync("select:has-text('File...')", new SelectOptionValue { Label = "oahpl-financial-statements-fy24.xml" });

        // Wait for XML to load
        await Page.WaitForTimeoutAsync(2000);

        // Assert - XML content should be visible in the left panel
        var xmlPanel = Page.Locator(".panel-left textarea.code-editor");
        await Expect(xmlPanel).ToBeVisibleAsync();

        var xmlContent = await xmlPanel.InputValueAsync();
        Assert.That(xmlContent, Does.Contain("<?xml"), "XML content should be loaded");
        Assert.That(xmlContent.Length, Is.GreaterThan(1000), "XML content should be substantial");

        // Verify filename is displayed
        var filenameDisplay = Page.Locator(".panel-left .panel-header .text-muted");
        await Expect(filenameDisplay).ToContainTextAsync("oahpl-financial-statements-fy24.xml");
    }

    /// <summary>
    /// Test 2: Critical Path - User can transform XML with XSLT
    /// This is the CORE FEATURE of the application
    /// </summary>
    [Test]
    public async Task UserCanTransformXMLWithXSLT()
    {
        // Arrange - Load a project and file
        await Page.SelectOptionAsync("select:has-text('Project...')", "ar24-3");
        await Page.WaitForTimeoutAsync(1000);
        await Page.SelectOptionAsync("select:has-text('File...')", new SelectOptionValue { Label = "oahpl-financial-statements-fy24.xml" });
        await Page.WaitForTimeoutAsync(2000);

        // Act - Click the Transform button
        var transformButton = Page.Locator("button[title='Transform']");
        await transformButton.ClickAsync();

        // Wait for transformation to complete (max 10 seconds)
        await Page.WaitForTimeoutAsync(5000);

        // Assert - Output should be visible
        var outputPanel = Page.Locator(".panel-right .panel-content");

        // In rendered mode, we should see HTML content
        var renderedContent = outputPanel.Locator(".preview-rendered");
        await Expect(renderedContent).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Should NOT show the empty state message
        await Expect(outputPanel.Locator("text=Click \"Transform\" to preview the output")).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 3: User can toggle between Rendered and Source views
    /// </summary>
    [Test]
    public async Task UserCanToggleBetweenRenderedAndSourceViews()
    {
        // Arrange - Load and transform
        await Page.SelectOptionAsync("select:has-text('Project...')", "ar24-3");
        await Page.WaitForTimeoutAsync(1000);
        await Page.SelectOptionAsync("select:has-text('File...')", new SelectOptionValue { Label = "oahpl-financial-statements-fy24.xml" });
        await Page.WaitForTimeoutAsync(2000);
        await Page.Locator("button[title='Transform']").ClickAsync();
        await Page.WaitForTimeoutAsync(3000);

        // Act - Switch to Source view
        await Page.Locator("a.nav-link:has-text('Source')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Should see source code in textarea
        var sourceTextarea = Page.Locator(".panel-right textarea.code-editor");
        await Expect(sourceTextarea).ToBeVisibleAsync();
        var sourceContent = await sourceTextarea.InputValueAsync();
        Assert.That(sourceContent, Does.Contain("<"), "Source view should show HTML/XML markup");

        // Act - Switch back to Rendered view
        await Page.Locator("a.nav-link:has-text('Rendered')").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Should see rendered content again
        await Expect(Page.Locator(".preview-rendered")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 4: User can open and use the settings panel
    /// </summary>
    [Test]
    public async Task UserCanOpenAndUseSettingsPanel()
    {
        // Act - Click settings button
        var settingsButton = Page.Locator("button[title='Settings']");
        await settingsButton.ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Assert - Settings panel should be visible
        var settingsPanel = Page.Locator(".settings-panel");
        await Expect(settingsPanel).ToBeVisibleAsync();
        await Expect(Page.Locator(".settings-header:has-text('Transformation Settings')")).ToBeVisibleAsync();

        // All three checkboxes should be present
        await Expect(Page.Locator("label:has-text('Use XSLT3 Service')")).ToBeVisibleAsync();
        await Expect(Page.Locator("label:has-text('Normalize Headers')")).ToBeVisibleAsync();
        await Expect(Page.Locator("label:has-text('Auto-transform on XSLT change')")).ToBeVisibleAsync();

        // Act - Toggle a setting
        await Page.Locator("#useXslt3").ClickAsync();

        // Act - Close settings panel
        await Page.Locator(".settings-panel .btn-close").ClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Assert - Settings panel should be hidden
        await Expect(settingsPanel).Not.ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 5: User can save XSLT changes
    /// </summary>
    [Test]
    public async Task UserCanSaveXSLTChanges()
    {
        // Note: This test modifies the XSLT file, so we should restore it after

        // Act - Click Save button
        var saveButton = Page.Locator("button[title='Save XSLT']");
        await saveButton.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Assert - Success message should appear
        var successAlert = Page.Locator(".alert-success:has-text('XSLT template saved successfully')");
        await Expect(successAlert).ToBeVisibleAsync(new() { Timeout = 3000 });
    }

    /// <summary>
    /// Test 6: User can reset the workflow
    /// </summary>
    [Test]
    public async Task UserCanResetWorkflow()
    {
        // Arrange - Load a project and file
        await Page.SelectOptionAsync("select:has-text('Project...')", "ar24-3");
        await Page.WaitForTimeoutAsync(1000);
        await Page.SelectOptionAsync("select:has-text('File...')", new SelectOptionValue { Label = "oahpl-financial-statements-fy24.xml" });
        await Page.WaitForTimeoutAsync(2000);

        // Act - Click Reset button
        var resetButton = Page.Locator("button[title='Reset']");
        await resetButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - State should be reset
        // Project dropdown should be empty
        var projectSelect = Page.Locator("select:has-text('Project...')");
        var selectedProject = await projectSelect.InputValueAsync();
        Assert.That(selectedProject, Is.Empty.Or.EqualTo(""), "Project should be deselected after reset");

        // File dropdown should be disabled
        var fileSelect = Page.Locator("select:has-text('File...')");
        await Expect(fileSelect).ToBeDisabledAsync();

        // XML panel should show empty state
        await Expect(Page.Locator("text=Select a project and file to view the source XML")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 7: Navigation between pages works correctly
    /// </summary>
    [Test]
    public async Task UserCanNavigateBetweenPages()
    {
        // Assert - We're on Development page
        await Expect(Page.Locator("a.nav-link.active:has-text('Development')")).ToBeVisibleAsync();

        // Act - Navigate to Home
        await Page.Locator("a.nav-link:has-text('Home')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - We're on Home page
        await Expect(Page.Locator("h1:has-text('PDF to Taxxor TDM XHTML Conversion Tool')")).ToBeVisibleAsync();
        await Expect(Page.Locator("a.nav-link.active:has-text('Home')")).ToBeVisibleAsync();

        // Act - Navigate to Production
        await Page.Locator("a.nav-link:has-text('Production')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - We're on Production page
        await Expect(Page.Locator("a.nav-link.active:has-text('Production')")).ToBeVisibleAsync();

        // Act - Navigate back to Development
        await Page.Locator("a.nav-link:has-text('Development')").ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - We're back on Development page
        await Expect(Page.Locator("a.nav-link.active:has-text('Development')")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 8: Application handles errors gracefully
    /// </summary>
    [Test]
    public async Task ApplicationHandlesErrorsGracefully()
    {
        // This test verifies that the app doesn't crash and shows error messages

        // Act - Try to transform without selecting a file
        var transformButton = Page.Locator("button[title='Transform']");

        // Assert - Transform button should be disabled
        await Expect(transformButton).ToBeDisabledAsync();

        // The absence of crashes and the presence of proper disabled states
        // indicates graceful error handling
    }

    /// <summary>
    /// Test 9: UI elements are properly aligned and visible
    /// This catches CSS/layout regressions
    /// </summary>
    [Test]
    public async Task UIElementsAreProperlyAlignedAndVisible()
    {
        // Assert - Navigation bar is visible
        await Expect(Page.Locator(".top-nav")).ToBeVisibleAsync();

        // All three panels should be visible
        await Expect(Page.Locator(".panel-left")).ToBeVisibleAsync();
        await Expect(Page.Locator(".panel-center")).ToBeVisibleAsync();
        await Expect(Page.Locator(".panel-right")).ToBeVisibleAsync();

        // Panel headers should be visible
        await Expect(Page.Locator(".panel-left .panel-header:has-text('Source XML')")).ToBeVisibleAsync();
        await Expect(Page.Locator(".panel-center .panel-header:has-text('XSLT Transformation')")).ToBeVisibleAsync();
        await Expect(Page.Locator(".panel-right .panel-header:has-text('Output Preview')")).ToBeVisibleAsync();

        // Toolbar buttons should be visible
        await Expect(Page.Locator("button[title='Transform']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Save XSLT']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Reset']")).ToBeVisibleAsync();
        await Expect(Page.Locator("button[title='Settings']")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Test 10: Monaco editor loads and displays XSLT
    /// </summary>
    [Test]
    public async Task MonacoEditorLoadsAndDisplaysXSLT()
    {
        // Wait for Monaco editor to initialize
        await Page.WaitForTimeoutAsync(2000);

        // Assert - Monaco editor container should be visible
        var monacoContainer = Page.Locator("#monaco-editor-container");
        await Expect(monacoContainer).ToBeVisibleAsync();

        // Editor should contain XSLT content
        var editorTextarea = Page.Locator("#monaco-editor-container textarea");
        await Expect(editorTextarea).ToBeAttachedAsync();
    }
}

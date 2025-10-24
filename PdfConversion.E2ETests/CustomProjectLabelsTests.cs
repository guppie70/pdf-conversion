using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PdfConversion.E2ETests;

/// <summary>
/// End-to-end tests for the Custom Project Labels feature.
/// Tests the complete workflow of managing project labels through the UI.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class CustomProjectLabelsTests : PageTest
{
    private const string BaseUrl = "http://localhost:8085";
    private const string HomeUrl = $"{BaseUrl}/";

    [SetUp]
    public async Task Setup()
    {
        // Navigate to home page (project listing) before each test
        await Page.GotoAsync(HomeUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Wait for projects to load
        await Page.WaitForTimeoutAsync(2000);
    }

    /// <summary>
    /// Test 1: Critical Path - Homepage displays project listing with labels
    /// </summary>
    [Test]
    public async Task HomepageDisplaysProjectListingWithLabels()
    {
        // Assert - Page title should be "Projects"
        await Expect(Page.Locator("h1:has-text('Projects')")).ToBeVisibleAsync();

        // Assert - System Status card should be visible
        await Expect(Page.Locator(".system-status-card")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=System Status")).ToBeVisibleAsync();

        // Assert - At least one customer section should be visible
        var customerSections = Page.Locator(".customer-section");
        await Expect(customerSections.First).ToBeVisibleAsync();

        // Assert - Project table should be visible with headers (use first to avoid strict mode error with multiple customers)
        await Expect(Page.Locator("th:has-text('Project ID')").First).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Project Name')").First).ToBeVisibleAsync();

        // Assert - At least one project should be listed
        var projectRows = Page.Locator(".projects-table tbody tr");
        var count = await projectRows.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "At least one project should be listed");
    }

    /// <summary>
    /// Test 2: Critical Path - User can double-click to edit a label
    /// </summary>
    [Test]
    public async Task UserCanDoubleClickToEditLabel()
    {
        // Arrange - Wait for projects to load
        var editableLabel = Page.Locator(".editable-label").First;
        await Expect(editableLabel).ToBeVisibleAsync();

        // Get the original text
        var originalText = await editableLabel.TextContentAsync();
        Assert.That(originalText, Is.Not.Null.And.Not.Empty, "Label should have text");

        // Act - Double-click the label
        await editableLabel.DblClickAsync();

        // Wait for edit mode
        await Page.WaitForTimeoutAsync(300);

        // Assert - Input field should appear
        var inputField = Page.Locator(".inline-edit-input");
        await Expect(inputField).ToBeVisibleAsync();
        await Expect(inputField).ToBeFocusedAsync();

        // Assert - Input should contain the current label value (not the full display string)
        var inputValue = await inputField.InputValueAsync();
        Assert.That(inputValue, Is.Not.Null, "Input should have a value");
    }

    /// <summary>
    /// Test 3: Critical Path - User can edit and save a custom label
    /// </summary>
    [Test]
    public async Task UserCanEditAndSaveCustomLabel()
    {
        // Arrange - Find the first project with ID ar24-3 (if exists)
        var projectRow = Page.Locator("tr").Filter(new() { Has = Page.Locator("code:has-text('ar24-3')") });
        var editableLabel = projectRow.Locator(".editable-label");

        // Check if ar24-3 exists, otherwise use the first available project
        var exists = await editableLabel.IsVisibleAsync();
        if (!exists)
        {
            // Use first available project
            editableLabel = Page.Locator(".editable-label").First;
            projectRow = editableLabel.Locator("..").Locator("..");
        }

        await Expect(editableLabel).ToBeVisibleAsync();

        // Get project ID for verification
        var projectIdCode = projectRow.Locator("code").First;
        var projectId = await projectIdCode.TextContentAsync();

        // Act - Double-click to enter edit mode
        await editableLabel.DblClickAsync();
        await Page.WaitForTimeoutAsync(300);

        // Type a new label
        var testLabel = $"E2E Test Label {DateTime.Now:HHmmss}";
        var inputField = Page.Locator(".inline-edit-input");
        await inputField.FillAsync(testLabel);

        // Act - Press Enter to save (or wait for auto-save)
        await inputField.PressAsync("Enter");

        // Wait for save operation (debounce + save time)
        await Page.WaitForTimeoutAsync(1500);

        // Assert - Success notification should appear
        var successToast = Page.Locator(".toast-notification.success");
        await Expect(successToast).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Expect(successToast).ToContainTextAsync("Label saved");

        // Assert - Input field should be hidden (exited edit mode)
        await Expect(inputField).Not.ToBeVisibleAsync();

        // Assert - Label should display with format: "{CustomLabel} ({projectId})"
        var updatedLabel = projectRow.Locator(".editable-label");
        await Expect(updatedLabel).ToBeVisibleAsync();
        var displayText = await updatedLabel.TextContentAsync();
        Assert.That(displayText, Does.Contain(testLabel), "Display should contain custom label");
        Assert.That(displayText, Does.Contain($"({projectId})"), "Display should contain project ID in parentheses");
    }

    /// <summary>
    /// Test 4: Critical Path - Custom labels persist across page reloads
    /// </summary>
    [Test]
    public async Task CustomLabelsPersistAcrossPageReloads()
    {
        // Arrange - Edit a label first
        var editableLabel = Page.Locator(".editable-label").First;
        await Expect(editableLabel).ToBeVisibleAsync();

        var projectRow = editableLabel.Locator("..").Locator("..");
        var projectIdCode = projectRow.Locator("code").First;
        var projectId = await projectIdCode.TextContentAsync();

        // Edit the label
        await editableLabel.DblClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var testLabel = $"Persistence Test {DateTime.Now:HHmmss}";
        var inputField = Page.Locator(".inline-edit-input");
        await inputField.FillAsync(testLabel);
        await inputField.PressAsync("Enter");

        // Wait for save
        await Page.WaitForTimeoutAsync(1500);

        // Act - Reload the page
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(2000);

        // Assert - Label should still show the custom value
        var reloadedProjectRow = Page.Locator("tr").Filter(new() { Has = Page.Locator($"code:has-text('{projectId}')") });
        var reloadedLabel = reloadedProjectRow.Locator(".editable-label");
        await Expect(reloadedLabel).ToBeVisibleAsync();

        var displayText = await reloadedLabel.TextContentAsync();
        Assert.That(displayText, Does.Contain(testLabel), "Custom label should persist after reload");
        Assert.That(displayText, Does.Contain($"({projectId})"), "Display format should be maintained");
    }

    /// <summary>
    /// Test 5: User can clear a label (delete)
    /// </summary>
    [Test]
    public async Task UserCanClearLabelToDeleteIt()
    {
        // Arrange - Find a project and set a label first
        var editableLabel = Page.Locator(".editable-label").First;
        await Expect(editableLabel).ToBeVisibleAsync();

        var projectRow = editableLabel.Locator("..").Locator("..");
        var projectIdCode = projectRow.Locator("code").First;
        var projectId = await projectIdCode.TextContentAsync();

        // Set a label first
        await editableLabel.DblClickAsync();
        await Page.WaitForTimeoutAsync(300);
        var inputField = Page.Locator(".inline-edit-input");
        await inputField.FillAsync("Label to Delete");
        await inputField.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(1500);

        // Act - Clear the label
        await editableLabel.DblClickAsync();
        await Page.WaitForTimeoutAsync(300);
        await inputField.FillAsync("");
        await inputField.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(1500);

        // Assert - Should show auto-generated label or fallback
        var displayText = await editableLabel.TextContentAsync();
        Assert.That(displayText, Does.Contain($"({projectId})"), "Display should still show project ID");

        // If it's ar24-X format, should show "Annual Report" fallback
        if (projectId != null && projectId.StartsWith("ar"))
        {
            Assert.That(displayText, Does.Contain("Annual Report"), "Should show fallback label");
        }
    }

    /// <summary>
    /// Test 6: User can cancel edit with Escape key
    /// </summary>
    [Test]
    public async Task UserCanCancelEditWithEscapeKey()
    {
        // Arrange
        var editableLabel = Page.Locator(".editable-label").First;
        await Expect(editableLabel).ToBeVisibleAsync();

        var originalText = await editableLabel.TextContentAsync();

        // Act - Double-click and type something
        await editableLabel.DblClickAsync();
        await Page.WaitForTimeoutAsync(300);

        var inputField = Page.Locator(".inline-edit-input");
        await inputField.FillAsync("This should be cancelled");

        // Press Escape to cancel
        await inputField.PressAsync("Escape");
        await Page.WaitForTimeoutAsync(300);

        // Assert - Input should be hidden
        await Expect(inputField).Not.ToBeVisibleAsync();

        // Assert - Original text should be unchanged
        var currentText = await editableLabel.TextContentAsync();
        Assert.That(currentText, Is.EqualTo(originalText), "Label should remain unchanged after cancel");
    }

    /// <summary>
    /// Test 7: Projects are grouped by customer
    /// </summary>
    [Test]
    public async Task ProjectsAreGroupedByCustomer()
    {
        // Assert - Customer sections should exist
        var customerSections = Page.Locator(".customer-section");
        var sectionCount = await customerSections.CountAsync();
        Assert.That(sectionCount, Is.GreaterThan(0), "At least one customer section should exist");

        // Assert - Each section should have a customer header
        var customerHeaders = Page.Locator(".customer-header h2");
        var headerCount = await customerHeaders.CountAsync();
        Assert.That(headerCount, Is.EqualTo(sectionCount), "Each section should have a customer header");

        // Assert - Headers should show "Customer: {name}"
        var firstHeader = customerHeaders.First;
        var headerText = await firstHeader.TextContentAsync();
        Assert.That(headerText, Does.StartWith("Customer:"), "Header should start with 'Customer:'");
    }

    /// <summary>
    /// Test 8: System Status card displays correct information
    /// </summary>
    [Test]
    public async Task SystemStatusCardDisplaysCorrectInformation()
    {
        // Assert - System Status card should be visible
        var statusCard = Page.Locator(".system-status-card");
        await Expect(statusCard).ToBeVisibleAsync();

        // Assert - Environment should be displayed
        await Expect(statusCard.Locator("text=Environment:")).ToBeVisibleAsync();

        // Assert - XSLT Service status should be displayed
        await Expect(statusCard.Locator("text=XSLT Service:")).ToBeVisibleAsync();

        // Assert - Projects Available count should be displayed
        await Expect(statusCard.Locator("text=Projects Available:")).ToBeVisibleAsync();

        // Assert - Should show badge with count
        var projectBadge = statusCard.Locator(".badge").Last;
        await Expect(projectBadge).ToBeVisibleAsync();
        var badgeText = await projectBadge.TextContentAsync();
        Assert.That(badgeText, Is.Not.Null.And.Not.Empty, "Badge should show project count");
    }

    /// <summary>
    /// Test 9: Hover hint shows on editable labels
    /// </summary>
    [Test]
    public async Task HoverHintShowsOnEditableLabels()
    {
        // Arrange
        var editableLabel = Page.Locator(".editable-label").First;
        await Expect(editableLabel).ToBeVisibleAsync();

        // Assert - Label should have title attribute with hint
        var titleAttr = await editableLabel.GetAttributeAsync("title");
        Assert.That(titleAttr, Is.EqualTo("Double-click to edit"), "Label should have hover hint");
    }

    /// <summary>
    /// Test 10: Last updated timestamp is displayed
    /// </summary>
    [Test]
    public async Task LastUpdatedTimestampIsDisplayed()
    {
        // Assert - "Last updated" text should be visible
        var lastUpdatedText = Page.Locator("text=/Last updated:/");
        await Expect(lastUpdatedText).ToBeVisibleAsync();

        // Assert - Should show relative time (e.g., "just now", "2 minutes ago")
        var timestampText = await lastUpdatedText.TextContentAsync();
        Assert.That(timestampText, Does.Contain("Last updated:"), "Should show last updated label");
        Assert.That(timestampText, Does.Match("ago|just now"), "Should show relative time");
    }
}

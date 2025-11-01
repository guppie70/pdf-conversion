using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace PdfConversion.E2ETests;

/// <summary>
/// End-to-end tests for Manual Mode functionality in the Generate Hierarchy page.
/// Tests the complete user workflow for building hierarchies manually.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ManualModeTests : PageTest
{
    private const string BaseUrl = "http://localhost:8085";
    private const string HierarchyPageUrl = $"{BaseUrl}/generate-hierarchy";

    [SetUp]
    public async Task Setup()
    {
        // Navigate to generate hierarchy page before each test
        await Page.GotoAsync(HierarchyPageUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Test: User can switch to Manual Mode and see the mode-specific UI
    /// </summary>
    [Test]
    public async Task UserCanSwitchToManualMode()
    {
        // Act - Click Manual Mode radio button
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Manual Mode specific elements should be visible
        await Expect(Page.Locator("button:has-text('Start Fresh')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Indent →')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('← Outdent')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('✕ Exclude')")).ToBeVisibleAsync();
        await Expect(Page.Locator("button:has-text('Include All')")).ToBeVisibleAsync();

        // Buttons should be disabled when no selection
        await Expect(Page.Locator("button:has-text('Indent →')")).ToBeDisabledAsync();
        await Expect(Page.Locator("button:has-text('← Outdent')")).ToBeDisabledAsync();
        await Expect(Page.Locator("button:has-text('✕ Exclude')")).ToBeDisabledAsync();
    }

    /// <summary>
    /// Test: User can load a source XML and see headers in flat list
    /// This is a critical test - if this fails, Manual Mode cannot function
    /// </summary>
    [Test]
    public async Task UserCanLoadSourceXmlAndSeeHeadersInFlatList()
    {
        // Arrange - Switch to Manual Mode
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Act - Select a project and source XML
        // Note: This assumes a test project exists
        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1) // Skip if no files available
        {
            // Select first available file (skip placeholder option)
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            // Assert - Headers should be visible in tree
            var treeItems = Page.Locator(".hierarchy-tree-item");
            var treeItemCount = await treeItems.CountAsync();

            Assert.That(treeItemCount, Is.GreaterThan(0), "Should display headers in tree");

            // All items should be at level 0 initially (flat list)
            var firstItem = treeItems.First;
            var indentLevel = await firstItem.GetAttributeAsync("data-indent-level");
            Assert.That(indentLevel, Is.EqualTo("0"), "Headers should start at level 0");
        }
    }

    /// <summary>
    /// Test: User can select a header item in the tree
    /// </summary>
    [Test]
    public async Task UserCanSelectHeaderItem()
    {
        // Arrange - Switch to Manual Mode and load headers
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            // Act - Click on first tree item
            var firstItem = Page.Locator(".hierarchy-tree-item").First;
            await firstItem.ClickAsync();
            await Page.WaitForTimeoutAsync(300);

            // Assert - Item should be selected (has selected class)
            await Expect(firstItem).ToHaveClassAsync(new Regex("selected"));

            // Buttons should be enabled/disabled appropriately
            // First item cannot be indented (no previous sibling)
            await Expect(Page.Locator("button:has-text('Indent →')")).ToBeDisabledAsync();

            // First item can be excluded
            await Expect(Page.Locator("button:has-text('✕ Exclude')")).ToBeEnabledAsync();
        }
    }

    /// <summary>
    /// Test: User can indent a header item
    /// </summary>
    [Test]
    public async Task UserCanIndentHeaderItem()
    {
        // Arrange - Switch to Manual Mode, load headers, select second item
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 2)
            {
                // Select second item (first item cannot be indented)
                var secondItem = treeItems.Nth(1);
                await secondItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                // Act - Click Indent button
                var indentButton = Page.Locator("button:has-text('Indent →')");
                await Expect(indentButton).ToBeEnabledAsync();
                await indentButton.ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Assert - Item should now be indented (level 1)
                var indentLevel = await secondItem.GetAttributeAsync("data-indent-level");
                Assert.That(indentLevel, Is.EqualTo("1"), "Item should be indented to level 1");
            }
        }
    }

    /// <summary>
    /// Test: User can use keyboard shortcut (Tab) to indent
    /// </summary>
    [Test]
    public async Task UserCanUseTabKeyToIndent()
    {
        // Arrange
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 2)
            {
                var secondItem = treeItems.Nth(1);
                await secondItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                // Act - Press Tab key
                await Page.Keyboard.PressAsync("Tab");
                await Page.WaitForTimeoutAsync(500);

                // Assert - Item should be indented
                var indentLevel = await secondItem.GetAttributeAsync("data-indent-level");
                Assert.That(indentLevel, Is.EqualTo("1"), "Tab key should indent item");
            }
        }
    }

    /// <summary>
    /// Test: User can outdent a header item
    /// </summary>
    [Test]
    public async Task UserCanOutdentHeaderItem()
    {
        // Arrange - Indent an item first
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 2)
            {
                var secondItem = treeItems.Nth(1);
                await secondItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                // Indent first
                await Page.Locator("button:has-text('Indent →')").ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Act - Outdent
                var outdentButton = Page.Locator("button:has-text('← Outdent')");
                await Expect(outdentButton).ToBeEnabledAsync();
                await outdentButton.ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Assert - Item should be back to level 0
                var indentLevel = await secondItem.GetAttributeAsync("data-indent-level");
                Assert.That(indentLevel, Is.EqualTo("0"), "Item should be outdented back to level 0");
            }
        }
    }

    /// <summary>
    /// Test: User can exclude a header from hierarchy
    /// </summary>
    [Test]
    public async Task UserCanExcludeHeaderItem()
    {
        // Arrange
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 1)
            {
                var firstItem = treeItems.First;
                var itemTitle = await firstItem.Locator(".item-title").TextContentAsync();

                // Act - Exclude item
                await firstItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                await Page.Locator("button:has-text('✕ Exclude')").ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Assert - Item should be excluded (has excluded class or is hidden)
                // Note: Implementation may vary - item might be hidden or grayed out
                var updatedTreeItems = Page.Locator(".hierarchy-tree-item:not(.excluded)");
                var updatedCount = await updatedTreeItems.CountAsync();

                Assert.That(updatedCount, Is.LessThan(itemCount), "Item count should decrease after exclusion");
            }
        }
    }

    /// <summary>
    /// Test: User can save manual hierarchy
    /// </summary>
    [Test]
    public async Task UserCanSaveManualHierarchy()
    {
        // Arrange
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 2)
            {
                // Make a change - indent second item
                var secondItem = treeItems.Nth(1);
                await secondItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);
                await Page.Locator("button:has-text('Indent →')").ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Act - Click Save Hierarchy button
                var saveButton = Page.Locator("button:has-text('Save Hierarchy')");
                await Expect(saveButton).ToBeVisibleAsync();
                await saveButton.ClickAsync();
                await Page.WaitForTimeoutAsync(1000);

                // Assert - Success message should appear (toast or alert)
                var successToast = Page.Locator(".toast-success, .alert-success");
                await Expect(successToast).ToBeVisibleAsync(new() { Timeout = 3000 });
            }
        }
    }

    /// <summary>
    /// Test: User can reset hierarchy with "Include All" button
    /// </summary>
    [Test]
    public async Task UserCanResetHierarchyWithIncludeAll()
    {
        // Arrange - Make some changes first
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 2)
            {
                // Indent second item
                var secondItem = treeItems.Nth(1);
                await secondItem.ClickAsync();
                await Page.WaitForTimeoutAsync(300);
                await Page.Locator("button:has-text('Indent →')").ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Verify item is indented
                var indentLevel = await secondItem.GetAttributeAsync("data-indent-level");
                Assert.That(indentLevel, Is.EqualTo("1"));

                // Act - Click "Include All" to reset
                await Page.Locator("button:has-text('Include All')").ClickAsync();
                await Page.WaitForTimeoutAsync(500);

                // Assert - All items should be back to level 0
                var updatedTreeItems = Page.Locator(".hierarchy-tree-item");
                var firstItemLevel = await updatedTreeItems.First.GetAttributeAsync("data-indent-level");
                var secondItemLevel = await updatedTreeItems.Nth(1).GetAttributeAsync("data-indent-level");

                Assert.That(firstItemLevel, Is.EqualTo("0"));
                Assert.That(secondItemLevel, Is.EqualTo("0"));
            }
        }
    }

    /// <summary>
    /// Test: Application handles multi-select with Shift+Click
    /// </summary>
    [Test]
    public async Task UserCanMultiSelectWithShiftClick()
    {
        // Arrange
        await Page.Locator("input[value='Manual']").ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var projectSelector = Page.Locator("select[aria-label='Select project']").First;
        await projectSelector.SelectOptionAsync(new SelectOptionValue { Label = "test-docling-conversion" });
        await Page.WaitForTimeoutAsync(1000);

        var fileSelector = Page.Locator("select[aria-label='Select file path']").First;
        var fileOptions = await fileSelector.Locator("option").AllAsync();

        if (fileOptions.Count > 1)
        {
            await fileSelector.SelectOptionAsync(new SelectOptionValue { Index = 1 });
            await Page.WaitForTimeoutAsync(2000);

            var treeItems = Page.Locator(".hierarchy-tree-item");
            var itemCount = await treeItems.CountAsync();

            if (itemCount >= 3)
            {
                // Act - Select first item
                await treeItems.First.ClickAsync();
                await Page.WaitForTimeoutAsync(300);

                // Shift+Click third item (should select range)
                await treeItems.Nth(2).ClickAsync(new LocatorClickOptions { Modifiers = new[] { KeyboardModifier.Shift } });
                await Page.WaitForTimeoutAsync(300);

                // Assert - Multiple items should be selected
                var selectedItems = Page.Locator(".hierarchy-tree-item.selected");
                var selectedCount = await selectedItems.CountAsync();

                Assert.That(selectedCount, Is.GreaterThanOrEqualTo(2), "Range selection should select multiple items");
            }
        }
    }
}

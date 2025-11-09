using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
// using PdfConversion.Configuration; // TODO: Fix this namespace
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Integration;

/// <summary>
/// Integration tests for Manual Mode workflow.
/// Tests the complete workflow of building hierarchies manually including:
/// - Indent/outdent operations with validation
/// - Document order preservation
/// - Save/load functionality
/// </summary>
public class ManualModeWorkflowTests
{
    private readonly IManualHierarchyBuilder _manualHierarchyBuilder;

    public ManualModeWorkflowTests()
    {
        var conversionSettings = Options.Create(new ConversionSettings
        {
            IdPostfixEnabled = false
        });
        _manualHierarchyBuilder = new ManualHierarchyBuilder(
            Mock.Of<ILogger<ManualHierarchyBuilder>>(),
            conversionSettings);
    }

    [Fact]
    public void ManualHierarchyBuilder_IndentWithChildren_MovesChildrenTogether()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 0, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 1, Level = "h3" }, // Child of Header 2
            new DocumentHeader { OriginalOrder = 4, Title = "Header 4", IndentLevel = 0, Level = "h1" }
        };

        // Act - Indent Header 2 (which has Header 3 as child)
        var (success, error) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 2 });

        // Assert
        Assert.True(success, error);
        Assert.Equal(1, headers[1].IndentLevel); // Header 2 indented to level 1
        Assert.Equal(2, headers[2].IndentLevel); // Header 3 (child) also indented to level 2
    }

    [Fact]
    public void ManualHierarchyBuilder_OutdentWithChildren_MovesChildrenTogether()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 1, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 2, Level = "h3" }, // Child of Header 2
            new DocumentHeader { OriginalOrder = 4, Title = "Header 4", IndentLevel = 0, Level = "h1" }
        };

        // Act - Outdent Header 2 (which has Header 3 as child)
        var (success, error) = _manualHierarchyBuilder.OutdentItems(headers, new List<int> { 2 });

        // Assert
        Assert.True(success, error);
        Assert.Equal(0, headers[1].IndentLevel); // Header 2 outdented to level 0
        Assert.Equal(1, headers[2].IndentLevel); // Header 3 (child) also outdented to level 1
    }

    [Fact]
    public void ManualHierarchyBuilder_ConvertToHierarchy_MaintainsDocumentOrder()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "First", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Second", IndentLevel = 1, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Third", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 4, Title = "Fourth", IndentLevel = 1, Level = "h2" }
        };

        // Act
        var hierarchy = _manualHierarchyBuilder.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Two root items

        // Collect all items in tree order
        var allItems = new List<HierarchyItem>();
        void CollectItems(List<HierarchyItem> items)
        {
            foreach (var item in items)
            {
                allItems.Add(item);
                if (item.SubItems != null) CollectItems(item.SubItems);
            }
        }
        CollectItems(hierarchy);

        // Verify order is preserved (1, 2, 3, 4)
        Assert.Equal("First", allItems[0].LinkName);
        Assert.Equal("Second", allItems[1].LinkName);
        Assert.Equal("Third", allItems[2].LinkName);
        Assert.Equal("Fourth", allItems[3].LinkName);
    }

    [Fact]
    public void ManualHierarchyBuilder_MultipleIndentOperations_ValidatesHierarchyGaps()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 0, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 0, Level = "h3" }
        };

        // Act - Indent Header 2 to level 1
        var (success1, _) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 2 });
        Assert.True(success1);
        Assert.Equal(1, headers[1].IndentLevel);

        // Indent Header 3 to level 1 (previous sibling is at level 1, so max allowed is 2)
        var (success2, _) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 3 });
        Assert.True(success2);
        Assert.Equal(1, headers[2].IndentLevel);

        // Indent Header 3 again to level 2 (still valid - previous sibling at level 1, max is 2)
        var (success3, _) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 3 });
        Assert.True(success3);
        Assert.Equal(2, headers[2].IndentLevel);

        // Now indent Header 3 again - should fail (would be level 3, but previous at level 1, max is 2)
        var (success4, error4) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 3 });

        // Assert
        Assert.False(success4);
        Assert.Contains("would create hierarchy gap", error4);
    }

    [Fact]
    public void ManualHierarchyBuilder_ConvertToHierarchy_PreservesSequentialOrder()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "A", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "B", IndentLevel = 1, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "C", IndentLevel = 2, Level = "h3" },
            new DocumentHeader { OriginalOrder = 4, Title = "D", IndentLevel = 0, Level = "h1" }
        };

        // Act
        var hierarchy = _manualHierarchyBuilder.ConvertToHierarchy(headers);

        // Assert - Check SequentialOrder is preserved
        var itemA = hierarchy[0];
        Assert.Equal(1, itemA.SequentialOrder);

        var itemB = itemA.SubItems![0];
        Assert.Equal(2, itemB.SequentialOrder);

        var itemC = itemB.SubItems![0];
        Assert.Equal(3, itemC.SequentialOrder);

        var itemD = hierarchy[1];
        Assert.Equal(4, itemD.SequentialOrder);
    }

    [Fact]
    public void ManualHierarchyBuilder_ExcludeItems_DoesNotAppearInHierarchy()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 1, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 1, Level = "h3" },
            new DocumentHeader { OriginalOrder = 4, Title = "Header 4", IndentLevel = 0, Level = "h1" }
        };

        // Act - Exclude Header 2
        _manualHierarchyBuilder.ExcludeItems(headers, new List<int> { 2 });
        var hierarchy = _manualHierarchyBuilder.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Header 1 and Header 4
        Assert.DoesNotContain(hierarchy, h => h.LinkName == "Header 2");

        // Header 3 should still appear (not excluded)
        var header1 = hierarchy[0];
        Assert.Single(header1.SubItems!);
        Assert.Equal("Header 3", header1.SubItems[0].LinkName);
    }

    [Fact]
    public void ManualHierarchyBuilder_IncludeAllItems_ResetsEverything()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 2, Level = "h2", IsExcluded = true },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 3, Level = "h3", IsExcluded = true }
        };

        // Act
        _manualHierarchyBuilder.IncludeAllItems(headers);

        // Assert
        Assert.All(headers, h =>
        {
            Assert.Equal(0, h.IndentLevel);
            Assert.False(h.IsExcluded);
        });
    }

    [Fact]
    public void ManualHierarchyBuilder_ComplexWorkflow_BuildNestedHierarchy()
    {
        // Arrange - Start with flat list
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Introduction", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Background", IndentLevel = 0, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "History", IndentLevel = 0, Level = "h3" },
            new DocumentHeader { OriginalOrder = 4, Title = "Main Content", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 5, Title = "Section 1", IndentLevel = 0, Level = "h2" },
            new DocumentHeader { OriginalOrder = 6, Title = "Subsection 1.1", IndentLevel = 0, Level = "h3" }
        };

        // Act - Build hierarchy step by step
        // Introduction
        //   Background
        //     History
        // Main Content
        //   Section 1
        //     Subsection 1.1

        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 2 }); // Background under Introduction
        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 3 }); // History under Background
        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 3 }); // History indented again

        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 5 }); // Section 1 under Main Content
        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 6 }); // Subsection under Section 1
        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 6 }); // Subsection indented again

        var hierarchy = _manualHierarchyBuilder.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Introduction and Main Content

        var intro = hierarchy[0];
        Assert.Equal("Introduction", intro.LinkName);
        Assert.Single(intro.SubItems!);
        Assert.Equal("Background", intro.SubItems[0].LinkName);
        Assert.Single(intro.SubItems[0].SubItems!);
        Assert.Equal("History", intro.SubItems[0].SubItems[0].LinkName);

        var mainContent = hierarchy[1];
        Assert.Equal("Main Content", mainContent.LinkName);
        Assert.Single(mainContent.SubItems!);
        Assert.Equal("Section 1", mainContent.SubItems[0].LinkName);
        Assert.Single(mainContent.SubItems[0].SubItems!);
        Assert.Equal("Subsection 1.1", mainContent.SubItems[0].SubItems[0].LinkName);
    }

    [Fact]
    public void ManualHierarchyBuilder_IndentOutdent_Roundtrip_ReturnsToOriginal()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 0, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 0, Level = "h3" }
        };

        // Act - Indent and then outdent
        var (indentSuccess, _) = _manualHierarchyBuilder.IndentItems(headers, new List<int> { 2 });
        Assert.True(indentSuccess);
        Assert.Equal(1, headers[1].IndentLevel);

        var (outdentSuccess, _) = _manualHierarchyBuilder.OutdentItems(headers, new List<int> { 2 });
        Assert.True(outdentSuccess);

        // Assert - Should be back to original state
        Assert.Equal(0, headers[1].IndentLevel);
    }

    [Fact]
    public void ManualHierarchyBuilder_CanIndent_ReturnsCorrectState()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 0, Level = "h2" }
        };

        // Act & Assert
        Assert.False(_manualHierarchyBuilder.CanIndentItems(headers, new List<int> { 1 })); // First item cannot indent
        Assert.True(_manualHierarchyBuilder.CanIndentItems(headers, new List<int> { 2 })); // Second item can indent

        // After indenting Header 2 to level 1
        _manualHierarchyBuilder.IndentItems(headers, new List<int> { 2 });

        // Previous sibling (Header 1) is at level 0, so max allowed for Header 2 is level 1
        // Header 2 is already at level 1, so indenting again would make it level 2 which exceeds max
        Assert.False(_manualHierarchyBuilder.CanIndentItems(headers, new List<int> { 2 })); // Cannot indent further (would create gap)
    }

    [Fact]
    public void ManualHierarchyBuilder_CanOutdent_ReturnsCorrectState()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 1, Level = "h2" }
        };

        // Act & Assert
        Assert.False(_manualHierarchyBuilder.CanOutdentItems(headers, new List<int> { 1 })); // Root level cannot outdent
        Assert.True(_manualHierarchyBuilder.CanOutdentItems(headers, new List<int> { 2 })); // Level 1 can outdent

        // After outdenting Header 2
        _manualHierarchyBuilder.OutdentItems(headers, new List<int> { 2 });
        Assert.False(_manualHierarchyBuilder.CanOutdentItems(headers, new List<int> { 2 })); // Now at root, cannot outdent
    }

    [Fact]
    public void ManualHierarchyBuilder_ConvertToHierarchy_HandlesDeepNesting()
    {
        // Arrange - Create deeply nested structure (5 levels)
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "L0", IndentLevel = 0, Level = "h1" },
            new DocumentHeader { OriginalOrder = 2, Title = "L1", IndentLevel = 1, Level = "h2" },
            new DocumentHeader { OriginalOrder = 3, Title = "L2", IndentLevel = 2, Level = "h3" },
            new DocumentHeader { OriginalOrder = 4, Title = "L3", IndentLevel = 3, Level = "h4" },
            new DocumentHeader { OriginalOrder = 5, Title = "L4", IndentLevel = 4, Level = "h5" }
        };

        // Act
        var hierarchy = _manualHierarchyBuilder.ConvertToHierarchy(headers);

        // Assert
        Assert.Single(hierarchy);

        var current = hierarchy[0];
        Assert.Equal("L0", current.LinkName);

        for (int i = 1; i <= 4; i++)
        {
            Assert.Single(current.SubItems!);
            current = current.SubItems[0];
            Assert.Equal($"L{i}", current.LinkName);
        }
    }
}

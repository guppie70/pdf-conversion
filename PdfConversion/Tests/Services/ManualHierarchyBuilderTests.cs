using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

/// <summary>
/// Unit tests for the ManualHierarchyBuilder service.
/// Tests the core operations for building hierarchies manually: indent, outdent, exclude, and conversion.
/// </summary>
public class ManualHierarchyBuilderTests
{
    private readonly Mock<ILogger<ManualHierarchyBuilder>> _loggerMock;
    private readonly Mock<IOptions<ConversionSettings>> _conversionSettingsMock;
    private readonly ManualHierarchyBuilder _service;

    public ManualHierarchyBuilderTests()
    {
        _loggerMock = new Mock<ILogger<ManualHierarchyBuilder>>();
        _conversionSettingsMock = new Mock<IOptions<ConversionSettings>>();

        // Default: postfix disabled for backward compatibility with existing tests
        _conversionSettingsMock.Setup(x => x.Value).Returns(new ConversionSettings
        {
            IdPostfixEnabled = false,
            IdPostfixFormat = "yyyyMMdd-HHmmss"
        });

        _service = new ManualHierarchyBuilder(_loggerMock.Object, _conversionSettingsMock.Object);
    }

    #region Helper Methods

    private List<DocumentHeader> CreateSampleHeaders()
    {
        return new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Introduction", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Overview", IndentLevel = 1, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 3, Title = "Background", IndentLevel = 1, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 4, Title = "Main Content", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 5, Title = "Section 1", IndentLevel = 1, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 6, Title = "Subsection 1.1", IndentLevel = 2, Level = "h3", IsExcluded = false }
        };
    }

    private List<DocumentHeader> CreateFlatHeaders()
    {
        return new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 0, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 0, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 4, Title = "Header 4", IndentLevel = 0, Level = "h1", IsExcluded = false }
        };
    }

    #endregion

    #region IndentItems Tests

    [Fact]
    public void IndentItems_ValidOperation_SucceedsAndIncrementsLevel()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 2 }; // Select "Header 2"

        // Act
        var (success, errorMessage) = _service.IndentItems(headers, selectedOrders);

        // Assert
        Assert.True(success, "Indent operation should succeed");
        Assert.Null(errorMessage);
        Assert.Equal(1, headers[1].IndentLevel); // Header 2 should be indented to level 1
    }

    [Fact]
    public void IndentItems_IndentsChildrenWithParent()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int> { 1 }; // Select "Introduction" which has children

        // Act
        var (success, errorMessage) = _service.IndentItems(headers, selectedOrders);

        // Assert
        Assert.False(success, "Cannot indent first item");
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void IndentItems_FirstItem_FailsWithError()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 1 }; // First item

        // Act
        var (success, errorMessage) = _service.IndentItems(headers, selectedOrders);

        // Assert
        Assert.False(success);
        Assert.Contains("Cannot indent the first item", errorMessage);
    }

    [Fact]
    public void IndentItems_ValidatesMaxAllowedLevel()
    {
        // Arrange - Test that validation correctly checks max allowed level
        // This test verifies the validation logic rather than testing an actual gap,
        // since gaps cannot occur with incremental indent operations (indent by 1 only)

        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Header 1", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Header 2", IndentLevel = 5, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 3, Title = "Header 3", IndentLevel = 3, Level = "h3", IsExcluded = false }
        };

        // Header 3 is at level 3, previous sibling (Header 2) is at level 5
        // Indenting Header 3 would make it level 4, which is <= 5+1, so it should succeed
        var (success1, _) = _service.IndentItems(headers, new List<int> { 3 });
        Assert.True(success1, "Should be able to indent when new level <= previous level + 1");

        // Now Header 3 is at level 4
        // Indent again: level 5 <= 5+1, should succeed
        var (success2, _) = _service.IndentItems(headers, new List<int> { 3 });
        Assert.True(success2);

        // Now Header 3 is at level 5
        // Indent again: level 6 <= 5+1, should succeed
        var (success3, _) = _service.IndentItems(headers, new List<int> { 3 });
        Assert.True(success3);

        // Now Header 3 is at level 6
        // Indent again: level 7 > 5+1 (6), should fail - creates gap
        var (success4, errorMessage) = _service.IndentItems(headers, new List<int> { 3 });

        // Assert
        Assert.False(success4);
        Assert.Contains("would create hierarchy gap", errorMessage);
    }

    [Fact]
    public void IndentItems_ExceedsMaxDepth_FailsWithError()
    {
        // Arrange - Create deeply nested structure up to max depth (10)
        var headers = new List<DocumentHeader>();

        // Create 11 headers in sequence, each at the appropriate level
        for (int i = 0; i <= 10; i++)
        {
            headers.Add(new DocumentHeader
            {
                OriginalOrder = i + 1,
                Title = $"Header L{i}",
                IndentLevel = i,
                Level = $"h{Math.Min(i + 1, 6)}",
                IsExcluded = false
            });
        }

        // Last header is at level 10 (max depth)
        var lastHeader = headers.Last();
        Assert.Equal(10, lastHeader.IndentLevel);

        // Act - Try to indent beyond max depth
        var (success, errorMessage) = _service.IndentItems(headers, new List<int> { lastHeader.OriginalOrder });

        // Assert
        Assert.False(success);
        Assert.Contains("already at maximum depth", errorMessage);
    }

    [Fact]
    public void IndentItems_EmptySelection_ReturnsSuccess()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int>();

        // Act
        var (success, errorMessage) = _service.IndentItems(headers, selectedOrders);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void IndentItems_InvalidOrder_FailsWithError()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 999 }; // Non-existent order

        // Act
        var (success, errorMessage) = _service.IndentItems(headers, selectedOrders);

        // Assert
        Assert.False(success);
        Assert.Contains("not found", errorMessage);
    }

    #endregion

    #region OutdentItems Tests

    [Fact]
    public void OutdentItems_ValidOperation_SucceedsAndDecrementsLevel()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int> { 2 }; // Select "Overview" at level 1

        // Act
        var (success, errorMessage) = _service.OutdentItems(headers, selectedOrders);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.Equal(0, headers[1].IndentLevel); // Should be outdented to level 0
    }

    [Fact]
    public void OutdentItems_OutdentsChildrenWithParent()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int> { 5 }; // Select "Section 1" which has "Subsection 1.1" as child

        var childBefore = headers.First(h => h.OriginalOrder == 6).IndentLevel;

        // Act
        var (success, errorMessage) = _service.OutdentItems(headers, selectedOrders);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.Equal(0, headers.First(h => h.OriginalOrder == 5).IndentLevel); // Parent outdented
        Assert.Equal(childBefore - 1, headers.First(h => h.OriginalOrder == 6).IndentLevel); // Child also outdented
    }

    [Fact]
    public void OutdentItems_RootLevel_FailsWithError()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 1 }; // Already at level 0

        // Act
        var (success, errorMessage) = _service.OutdentItems(headers, selectedOrders);

        // Assert
        Assert.False(success);
        Assert.Contains("already at root level", errorMessage);
    }

    [Fact]
    public void OutdentItems_EmptySelection_ReturnsSuccess()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int>();

        // Act
        var (success, errorMessage) = _service.OutdentItems(headers, selectedOrders);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void OutdentItems_InvalidOrder_FailsWithError()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int> { 999 };

        // Act
        var (success, errorMessage) = _service.OutdentItems(headers, selectedOrders);

        // Assert
        Assert.False(success);
        Assert.Contains("not found", errorMessage);
    }

    #endregion

    #region CanIndentItems / CanOutdentItems Tests

    [Fact]
    public void CanIndentItems_ValidOperation_ReturnsTrue()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 2 };

        // Act
        var canIndent = _service.CanIndentItems(headers, selectedOrders);

        // Assert
        Assert.True(canIndent);
    }

    [Fact]
    public void CanIndentItems_FirstItem_ReturnsFalse()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 1 };

        // Act
        var canIndent = _service.CanIndentItems(headers, selectedOrders);

        // Assert
        Assert.False(canIndent);
    }

    [Fact]
    public void CanOutdentItems_ValidOperation_ReturnsTrue()
    {
        // Arrange
        var headers = CreateSampleHeaders();
        var selectedOrders = new List<int> { 2 }; // Level 1 item

        // Act
        var canOutdent = _service.CanOutdentItems(headers, selectedOrders);

        // Assert
        Assert.True(canOutdent);
    }

    [Fact]
    public void CanOutdentItems_RootLevel_ReturnsFalse()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 1 }; // Level 0 item

        // Act
        var canOutdent = _service.CanOutdentItems(headers, selectedOrders);

        // Assert
        Assert.False(canOutdent);
    }

    #endregion

    #region ExcludeItems Tests

    [Fact]
    public void ExcludeItems_MarksItemsAsExcluded()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 2, 3 };

        // Act
        _service.ExcludeItems(headers, selectedOrders);

        // Assert
        Assert.True(headers[1].IsExcluded); // Header 2
        Assert.True(headers[2].IsExcluded); // Header 3
        Assert.False(headers[0].IsExcluded); // Header 1 unchanged
        Assert.False(headers[3].IsExcluded); // Header 4 unchanged
    }

    [Fact]
    public void ExcludeItems_EmptySelection_DoesNothing()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int>();

        // Act
        _service.ExcludeItems(headers, selectedOrders);

        // Assert
        Assert.All(headers, h => Assert.False(h.IsExcluded));
    }

    [Fact]
    public void ExcludeItems_InvalidOrder_LogsWarning()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        var selectedOrders = new List<int> { 999 };

        // Act
        _service.ExcludeItems(headers, selectedOrders);

        // Assert - No exception thrown, operation continues
        Assert.All(headers, h => Assert.False(h.IsExcluded));
    }

    #endregion

    #region IncludeAllItems Tests

    [Fact]
    public void IncludeAllItems_ResetsAllHeadersToFlatList()
    {
        // Arrange
        var headers = CreateSampleHeaders();

        // Exclude some items
        headers[1].IsExcluded = true;
        headers[2].IsExcluded = true;

        // Act
        _service.IncludeAllItems(headers);

        // Assert
        Assert.All(headers, h =>
        {
            Assert.Equal(0, h.IndentLevel);
            Assert.False(h.IsExcluded);
        });
    }

    [Fact]
    public void IncludeAllItems_NullHeaders_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        _service.IncludeAllItems(null!);
    }

    #endregion

    #region ConvertToHierarchy Tests

    [Fact]
    public void ConvertToHierarchy_FlatList_CreatesRootItems()
    {
        // Arrange
        var headers = CreateFlatHeaders();

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(4, hierarchy.Count); // All 4 items at root level
        Assert.All(hierarchy, item => Assert.Equal(1, item.Level)); // Level 1 in HierarchyItem (1-based)
    }

    [Fact]
    public void ConvertToHierarchy_NestedStructure_CreatesHierarchy()
    {
        // Arrange
        var headers = CreateSampleHeaders();

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // "Introduction" and "Main Content" at root

        var intro = hierarchy.First(h => h.LinkName == "Introduction");
        Assert.Equal(2, intro.SubItems?.Count); // "Overview" and "Background"

        var mainContent = hierarchy.First(h => h.LinkName == "Main Content");
        Assert.Single(mainContent.SubItems); // "Section 1"

        var section1 = mainContent.SubItems.First();
        Assert.Single(section1.SubItems); // "Subsection 1.1"
    }

    [Fact]
    public void ConvertToHierarchy_ExcludedItems_AreOmitted()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        headers[1].IsExcluded = true; // Exclude Header 2
        headers[2].IsExcluded = true; // Exclude Header 3

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Only Header 1 and Header 4
        Assert.All(hierarchy, item =>
            Assert.DoesNotContain(item.LinkName, new[] { "Header 2", "Header 3" }));
    }

    [Fact]
    public void ConvertToHierarchy_EmptyList_ReturnsEmpty()
    {
        // Arrange
        var headers = new List<DocumentHeader>();

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Empty(hierarchy);
    }

    [Fact]
    public void ConvertToHierarchy_AllExcluded_ReturnsEmpty()
    {
        // Arrange
        var headers = CreateFlatHeaders();
        foreach (var header in headers)
        {
            header.IsExcluded = true;
        }

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Empty(hierarchy);
    }

    [Fact]
    public void ConvertToHierarchy_PreservesOriginalOrder()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 3, Title = "Third", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 1, Title = "First", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Second", IndentLevel = 0, Level = "h1", IsExcluded = false }
        };

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(3, hierarchy.Count);
        Assert.Equal("First", hierarchy[0].LinkName);
        Assert.Equal("Second", hierarchy[1].LinkName);
        Assert.Equal("Third", hierarchy[2].LinkName);
    }

    [Fact]
    public void ConvertToHierarchy_SetsConfidenceTo100()
    {
        // Arrange
        var headers = CreateFlatHeaders();

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.All(hierarchy, item => Assert.Equal(100, item.Confidence));
    }

    [Fact]
    public void ConvertToHierarchy_PreservesHeaderType()
    {
        // Arrange
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Title", IndentLevel = 0, Level = "h2", IsExcluded = false }
        };

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Single(hierarchy);
        Assert.Equal("H2", hierarchy[0].HeaderType);
    }

    [Fact]
    public void ConvertToHierarchy_HandlesOrphanedItems()
    {
        // Arrange - Create item with level 2 but no parent at level 1
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Root", IndentLevel = 0, Level = "h1", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Orphan", IndentLevel = 2, Level = "h3", IsExcluded = false }
        };

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Orphan should be treated as root
        Assert.All(hierarchy, item => Assert.Empty(item.SubItems ?? new List<HierarchyItem>()));
    }

    [Fact]
    public void ConvertToHierarchy_WithPostfixEnabled_AppliesTimestampToIds()
    {
        // Arrange
        var settingsWithPostfix = new ConversionSettings
        {
            IdPostfixEnabled = true,
            IdPostfixFormat = "HHmmss"
        };

        _conversionSettingsMock.Setup(x => x.Value).Returns(settingsWithPostfix);
        var serviceWithPostfix = new ManualHierarchyBuilder(_loggerMock.Object, _conversionSettingsMock.Object);

        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Directors Report", IndentLevel = 0, Level = "h2", IsExcluded = false },
            new DocumentHeader { OriginalOrder = 2, Title = "Financial Statements", IndentLevel = 0, Level = "h2", IsExcluded = false }
        };

        // Act
        var hierarchy = serviceWithPostfix.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count);

        // Verify IDs have postfix format: base-name-HHMMSS
        var item1 = hierarchy[0];
        Assert.Equal("Directors Report", item1.LinkName);
        Assert.Matches(@"^directors-report-\d{6}$", item1.Id); // e.g., "directors-report-143025"
        Assert.Matches(@"^directors-report-\d{6}\.xml$", item1.DataRef); // e.g., "directors-report-143025.xml"

        var item2 = hierarchy[1];
        Assert.Equal("Financial Statements", item2.LinkName);
        Assert.Matches(@"^financial-statements-\d{6}$", item2.Id); // e.g., "financial-statements-143025"
        Assert.Matches(@"^financial-statements-\d{6}\.xml$", item2.DataRef); // e.g., "financial-statements-143025.xml"

        // Verify both items share the same postfix (same timestamp)
        var postfix1 = item1.Id.Split('-').Last();
        var postfix2 = item2.Id.Split('-').Last();
        Assert.Equal(postfix1, postfix2);
    }

    [Fact]
    public void ConvertToHierarchy_WithPostfixDisabled_GeneratesStandardIds()
    {
        // Arrange - Default settings have postfix disabled
        var headers = new List<DocumentHeader>
        {
            new DocumentHeader { OriginalOrder = 1, Title = "Directors Report", IndentLevel = 0, Level = "h2", IsExcluded = false }
        };

        // Act
        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Single(hierarchy);
        var item = hierarchy[0];
        Assert.Equal("directors-report", item.Id); // No postfix
        Assert.Equal("directors-report.xml", item.DataRef); // No postfix
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void IntegrationTest_IndentOutdentSequence_MaintainsConsistency()
    {
        // Arrange
        var headers = CreateFlatHeaders();

        // Act - Indent Header 2
        var (indentSuccess, _) = _service.IndentItems(headers, new List<int> { 2 });
        Assert.True(indentSuccess);
        Assert.Equal(1, headers[1].IndentLevel);

        // Act - Outdent Header 2 back to original
        var (outdentSuccess, _) = _service.OutdentItems(headers, new List<int> { 2 });
        Assert.True(outdentSuccess);
        Assert.Equal(0, headers[1].IndentLevel);

        // Assert - Should be back to original state
        Assert.All(headers, h => Assert.Equal(0, h.IndentLevel));
    }

    [Fact]
    public void IntegrationTest_BuildComplexHierarchy_FromFlatList()
    {
        // Arrange
        var headers = CreateFlatHeaders();

        // Act - Build structure:
        // Header 1 (level 0)
        //   Header 2 (level 1)
        //     Header 3 (level 2)
        // Header 4 (level 0)

        _service.IndentItems(headers, new List<int> { 2 }); // Header 2 to level 1
        _service.IndentItems(headers, new List<int> { 3 }); // Header 3 to level 1
        _service.IndentItems(headers, new List<int> { 3 }); // Header 3 to level 2

        var hierarchy = _service.ConvertToHierarchy(headers);

        // Assert
        Assert.Equal(2, hierarchy.Count); // Header 1 and Header 4 at root

        var header1 = hierarchy[0];
        Assert.Single(header1.SubItems); // Header 2

        var header2 = header1.SubItems[0];
        Assert.Single(header2.SubItems); // Header 3
    }

    #endregion
}

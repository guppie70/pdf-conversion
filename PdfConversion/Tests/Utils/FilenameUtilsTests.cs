using PdfConversion.Utils;
using Xunit;

namespace PdfConversion.Tests.Utils;

/// <summary>
/// Tests for FilenameUtils utility methods
/// Validates filename normalization and customer display name formatting
/// </summary>
public class FilenameUtilsTests
{
    [Theory]
    [InlineData("optiver", "Optiver")]
    [InlineData("antea-group", "Antea Group")]
    [InlineData("test", "Test")]
    [InlineData("multi-word-customer", "Multi Word Customer")]
    [InlineData("UPPERCASE", "Uppercase")]
    [InlineData("single", "Single")]
    [InlineData("three-word-name", "Three Word Name")]
    [InlineData("company-name-long", "Company Name Long")]
    public void FormatCustomerDisplayName_ValidCustomerNames_ReturnsFormattedName(
        string input,
        string expected)
    {
        // Act
        var result = FilenameUtils.FormatCustomerDisplayName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public void FormatCustomerDisplayName_NullOrEmpty_ReturnsUnknown(
        string? input,
        string expected)
    {
        // Act
        var result = FilenameUtils.FormatCustomerDisplayName(input!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("My Document", "my-document")]
    [InlineData("Project 2024", "project-2024")]
    [InlineData("  Extra  Spaces  ", "extra-spaces")]
    [InlineData("UPPERCASE TEXT", "uppercase-text")]
    [InlineData("Mixed-Case_File", "mixed-case_file")]
    public void NormalizeFileName_ValidInput_ReturnsNormalizedString(
        string input,
        string expected)
    {
        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "unnamed")]
    [InlineData("", "unnamed")]
    [InlineData("   ", "unnamed")]
    public void NormalizeFileName_NullOrEmpty_ReturnsUnnamed(
        string? input,
        string expected)
    {
        // Act
        var result = FilenameUtils.NormalizeFileName(input!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithInvalidCharacters_RemovesThem()
    {
        // Arrange - Use slash which is universally invalid on Unix-like systems
        var input = "file/name/with/slashes.txt";
        var expected = "filenamewithslashes.txt";

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithMultipleHyphens_CollapsesToSingle()
    {
        // Arrange
        var input = "file---with---many---hyphens";
        var expected = "file-with-many-hyphens";

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithLeadingTrailingHyphens_TrimsThem()
    {
        // Arrange
        var input = "-filename-";
        var expected = "filename";

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_VeryLongInput_TruncatesTo50Characters()
    {
        // Arrange
        var input = new string('a', 100);
        var expected = new string('a', 50);

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
        Assert.Equal(50, result.Length);
    }

    [Fact]
    public void NormalizeFileName_LongInputWithHyphenAtBoundary_TrimsTrailingHyphen()
    {
        // Arrange
        // Create 49 chars + hyphen + more chars (will be truncated at 50, then hyphen trimmed)
        var input = new string('a', 49) + "-" + new string('b', 20);
        var expected = new string('a', 49); // Hyphen at position 50 should be trimmed

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
        Assert.False(result.EndsWith("-"), "Result should not end with a hyphen");
    }

    [Fact]
    public void NormalizeFileName_OnlyInvalidCharacters_ReturnsUnnamed()
    {
        // Arrange - Use slashes which are invalid on Unix-like systems
        var input = "///";
        var expected = "unnamed";

        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatCustomerDisplayName_WithHyphenAndMixedCase_ConvertsCorrectly()
    {
        // Arrange
        var input = "ANTEA-GROUP";
        var expected = "Antea Group";

        // Act
        var result = FilenameUtils.FormatCustomerDisplayName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatCustomerDisplayName_WithMultipleHyphens_ConvertsAllToSpaces()
    {
        // Arrange
        var input = "multi-word-company-name";
        var expected = "Multi Word Company Name";

        // Act
        var result = FilenameUtils.FormatCustomerDisplayName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatCustomerDisplayName_SingleCharacter_CapitalizesCorrectly()
    {
        // Arrange
        var input = "a";
        var expected = "A";

        // Act
        var result = FilenameUtils.FormatCustomerDisplayName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Lead auditor's independence declaration", "lead-auditors-independence-declaration")]
    [InlineData("Director's report", "directors-report")]
    [InlineData("Directors' report", "directors-report")]
    [InlineData("Company's Policy", "companys-policy")]
    [InlineData("It's a test", "its-a-test")]
    public void NormalizeFileName_WithApostrophes_RemovesThem(
        string input,
        string expected)
    {
        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Section (1)", "section-1")]
    [InlineData("Item [A]", "item-a")]
    [InlineData("Note: Important!", "note-important")]
    [InlineData("Q&A Session", "q&a-session")]
    [InlineData("Price: $100.00", "price-10000")]
    public void NormalizeFileName_WithPunctuation_RemovesIt(
        string input,
        string expected)
    {
        // Act
        var result = FilenameUtils.NormalizeFileName(input);

        // Assert
        Assert.Equal(expected, result);
    }

    #region Postfix Tests

    [Fact]
    public void NormalizeFileName_WithPostfix_AppendsCorrectly()
    {
        // Arrange
        var input = "Directors Report";
        var postfix = "20250107-143025";
        var expected = "directors-report-20250107-143025";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithNullPostfix_BehavesAsNormal()
    {
        // Arrange
        var input = "Directors Report";
        string? postfix = null;
        var expected = "directors-report";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithEmptyPostfix_BehavesAsNormal()
    {
        // Arrange
        var input = "Directors Report";
        var postfix = "";
        var expected = "directors-report";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithWhitespacePostfix_BehavesAsNormal()
    {
        // Arrange
        var input = "Directors Report";
        var postfix = "   ";
        var expected = "directors-report";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Statement of financial position", "20250107-143025", "statement-of-financial-position-20250107-143025")]
    [InlineData("Note 1", "20250107-143025", "note-1-20250107-143025")]
    [InlineData("Auditor's Report", "20250108-090000", "auditors-report-20250108-090000")]
    public void NormalizeFileName_WithPostfix_HandlesVariousInputs(
        string input,
        string postfix,
        string expected)
    {
        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeFileName_WithPostfixAndLongInput_TruncatesBeforePostfix()
    {
        // Arrange
        var input = new string('a', 100);
        var postfix = "20250107-143025";
        var expectedPrefix = new string('a', 50);
        var expected = $"{expectedPrefix}-{postfix}";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
        Assert.True(result.EndsWith($"-{postfix}"), "Result should end with postfix");
        Assert.StartsWith(expectedPrefix, result);
    }

    [Fact]
    public void NormalizeFileName_WithPostfixAndSpecialCharacters_NormalizesAndAppendsPostfix()
    {
        // Arrange
        var input = "Director's Report (2024)!";
        var postfix = "20250107-143025";
        var expected = "directors-report-2024-20250107-143025";

        // Act
        var result = FilenameUtils.NormalizeFileName(input, postfix);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region EnsureUniqueId Tests

    [Fact]
    public void EnsureUniqueId_WithUniqueId_ReturnsOriginal()
    {
        // Arrange
        var usedIds = new HashSet<string>();
        var baseId = "directors-report-143025";

        // Act
        var result = FilenameUtils.EnsureUniqueId(baseId, usedIds);

        // Assert
        Assert.Equal("directors-report-143025", result);
        Assert.Contains("directors-report-143025", usedIds);
    }

    [Fact]
    public void EnsureUniqueId_WithDuplicateId_AppendsCounter()
    {
        // Arrange
        var usedIds = new HashSet<string> { "lorem-ipsum-143025" };
        var baseId = "lorem-ipsum-143025";

        // Act
        var result = FilenameUtils.EnsureUniqueId(baseId, usedIds);

        // Assert
        Assert.Equal("lorem-ipsum-143025-2", result);
        Assert.Contains("lorem-ipsum-143025-2", usedIds);
    }

    [Fact]
    public void EnsureUniqueId_WithMultipleDuplicates_IncrementsCounter()
    {
        // Arrange
        var usedIds = new HashSet<string>
        {
            "lorem-ipsum-143025",
            "lorem-ipsum-143025-2"
        };
        var baseId = "lorem-ipsum-143025";

        // Act
        var result = FilenameUtils.EnsureUniqueId(baseId, usedIds);

        // Assert
        Assert.Equal("lorem-ipsum-143025-3", result);
        Assert.Contains("lorem-ipsum-143025-3", usedIds);
    }

    [Fact]
    public void EnsureUniqueId_WithNullBaseId_ThrowsException()
    {
        // Arrange
        var usedIds = new HashSet<string>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            FilenameUtils.EnsureUniqueId(null!, usedIds));
    }

    [Fact]
    public void EnsureUniqueId_WithEmptyBaseId_ThrowsException()
    {
        // Arrange
        var usedIds = new HashSet<string>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            FilenameUtils.EnsureUniqueId("", usedIds));
    }

    [Fact]
    public void EnsureUniqueId_WithNullUsedIds_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            FilenameUtils.EnsureUniqueId("test-id", null!));
    }

    [Fact]
    public void EnsureUniqueId_SequentialCalls_TracksAllIds()
    {
        // Arrange
        var usedIds = new HashSet<string>();

        // Act
        var result1 = FilenameUtils.EnsureUniqueId("section-143025", usedIds);
        var result2 = FilenameUtils.EnsureUniqueId("section-143025", usedIds);
        var result3 = FilenameUtils.EnsureUniqueId("section-143025", usedIds);
        var result4 = FilenameUtils.EnsureUniqueId("other-143025", usedIds);

        // Assert
        Assert.Equal("section-143025", result1);
        Assert.Equal("section-143025-2", result2);
        Assert.Equal("section-143025-3", result3);
        Assert.Equal("other-143025", result4);
        Assert.Equal(4, usedIds.Count);
    }

    [Fact]
    public void EnsureUniqueId_WithExistingCounterPattern_HandlesCorrectly()
    {
        // Arrange
        var usedIds = new HashSet<string>
        {
            "report-143025",
            "report-143025-2"
        };
        var baseId = "report-143025";

        // Act
        var result = FilenameUtils.EnsureUniqueId(baseId, usedIds);

        // Assert
        Assert.Equal("report-143025-3", result);
    }

    #endregion
}

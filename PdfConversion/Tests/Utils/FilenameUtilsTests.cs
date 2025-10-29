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
}

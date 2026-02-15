using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

public class HeaderCapsFixerTests
{
    [Theory]
    [InlineData("STRATEGY & ACTIVITIES", "Strategy & Activities")]
    [InlineData("REPORT OF THE BOARD OF DIRECTORS", "Report of the Board of Directors")]
    [InlineData("NOTES TO THE FINANCIAL STATEMENTS", "Notes to the Financial Statements")]
    [InlineData("KEY FIGURES 2024", "Key Figures 2024")]
    [InlineData("A BRIEF HISTORY OF TIME", "A Brief History of Time")]
    [InlineData("THE END", "The End")]
    [InlineData("OF", "Of")]
    public void ToSmartTitleCase_ConvertsCorrectly(string input, string expected)
    {
        var result = HeaderCapsHelper.ToSmartTitleCase(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("STRATEGY & ACTIVITIES", true)]
    [InlineData("STRATEGY & ACTIVITIES 2024", true)]
    [InlineData("Strategy & Activities", false)]
    [InlineData("REPORT of THE BOARD", true)]
    [InlineData("Hello World", false)]
    [InlineData("123 456", false)]
    [InlineData("", false)]
    [InlineData("A", true)]
    public void IsAllCaps_DetectsCorrectly(string input, bool expected)
    {
        var result = HeaderCapsHelper.IsAllCaps(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("strategy & Activities", "Strategy & Activities")]
    [InlineData("the Board Report", "The Board Report")]
    [InlineData("Strategy & Activities", "Strategy & Activities")]  // already uppercase
    [InlineData("123 numbers first", "123 numbers first")]  // starts with number, no change
    [InlineData("", "")]  // empty
    public void CapitalizeFirstLetter_CapitalizesCorrectly(string input, string expected)
    {
        var result = HeaderCapsHelper.CapitalizeFirstLetter(input);
        Assert.Equal(expected, result);
    }
}

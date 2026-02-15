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
    [InlineData("123 numbers first", "123 Numbers first")]  // first alpha char 'n' is lowercase -> capitalize
    [InlineData("", "")]  // empty
    [InlineData("4.2\tfinancial Assets & Financial Liabilities", "4.2\tFinancial Assets & Financial Liabilities")]  // number prefix with tab
    [InlineData("4.3 financial Assets at Fvoci", "4.3 Financial Assets at Fvoci")]  // number prefix with space
    public void CapitalizeFirstLetter_CapitalizesCorrectly(string input, string expected)
    {
        var result = HeaderCapsHelper.CapitalizeFirstLetter(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("strategy & Activities", true)]
    [InlineData("Strategy & Activities", false)]
    [InlineData("4.2\tfinancial Assets", true)]   // first alpha char is lowercase
    [InlineData("4.2\tFinancial Assets", false)]   // first alpha char is uppercase
    [InlineData("123 456", false)]                  // no alpha chars
    [InlineData("", false)]
    public void StartsWithLowercase_DetectsCorrectly(string input, bool expected)
    {
        var result = HeaderCapsHelper.StartsWithLowercase(input);
        Assert.Equal(expected, result);
    }
}

using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests;

/// <summary>
/// Unit tests for XhtmlValidationService
/// </summary>
public class XhtmlValidationServiceTests
{
    private readonly Mock<ILogger<XhtmlValidationService>> _mockLogger;
    private readonly XhtmlValidationService _service;

    public XhtmlValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<XhtmlValidationService>>();
        _service = new XhtmlValidationService(_mockLogger.Object);
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithValidElements_ReturnsSuccess()
    {
        // Arrange
        var validXhtml = @"
            <html>
                <head>
                    <title>Test Document</title>
                </head>
                <body>
                    <h1>Title</h1>
                    <p>This is a paragraph with <strong>bold</strong> and <em>italic</em> text.</p>
                    <table>
                        <thead>
                            <tr>
                                <th>Header</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td>Data</td>
                            </tr>
                        </tbody>
                    </table>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(validXhtml, enableSchemaValidation: false);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.TotalIssues);
        Assert.Equal(0, result.TotalOccurrences);
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithInvalidElements_ReturnsIssues()
    {
        // Arrange
        var invalidXhtml = @"
            <html>
                <body>
                    <h1>Title</h1>
                    <Table>Invalid uppercase table</Table>
                    <CustomElement>Not a valid HTML element</CustomElement>
                    <AnotherCustom>Another invalid element</AnotherCustom>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(invalidXhtml, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);

        // Should find 3 distinct issues: Table (uppercase), CustomElement, AnotherCustom
        var invalidElementIssues = result.Issues.Where(i => i.Type == ValidationIssueType.InvalidElement).ToList();
        Assert.Contains(invalidElementIssues, i => i.ElementName == "Table");
        Assert.Contains(invalidElementIssues, i => i.ElementName == "CustomElement");
        Assert.Contains(invalidElementIssues, i => i.ElementName == "AnotherCustom");

        // Should also find uppercase issue for Table
        var uppercaseIssues = result.Issues.Where(i => i.Type == ValidationIssueType.UppercaseInElementName).ToList();
        Assert.Contains(uppercaseIssues, i => i.ElementName == "Table");
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithUppercaseElements_ReturnsUppercaseIssues()
    {
        // Arrange
        var uppercaseXhtml = @"
            <html>
                <Body>
                    <H1>Title</H1>
                    <P>Paragraph</P>
                    <DIV>Division</DIV>
                </Body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(uppercaseXhtml, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);

        // Should find uppercase issues for Body, H1, P, DIV
        var uppercaseIssues = result.Issues.Where(i => i.Type == ValidationIssueType.UppercaseInElementName).ToList();
        Assert.Contains(uppercaseIssues, i => i.ElementName == "Body");
        Assert.Contains(uppercaseIssues, i => i.ElementName == "H1");
        Assert.Contains(uppercaseIssues, i => i.ElementName == "P");
        Assert.Contains(uppercaseIssues, i => i.ElementName == "DIV");
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithMultipleOccurrences_TracksOccurrenceCount()
    {
        // Arrange
        var xhtmlWithDuplicates = @"
            <html>
                <body>
                    <Table>Table 1</Table>
                    <Table>Table 2</Table>
                    <Table>Table 3</Table>
                    <CustomElement>Element 1</CustomElement>
                    <CustomElement>Element 2</CustomElement>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(xhtmlWithDuplicates, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);

        // Find the Table issue
        var tableIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "Table");
        Assert.NotNull(tableIssue);
        Assert.Equal(3, tableIssue.OccurrenceCount);

        // Find the CustomElement issue
        var customIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "CustomElement");
        Assert.NotNull(customIssue);
        Assert.Equal(2, customIssue.OccurrenceCount);
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithMultipleOccurrences_StoresFirst5XPaths()
    {
        // Arrange
        var xhtmlWithManyOccurrences = @"
            <html>
                <body>
                    <div><CustomElement>1</CustomElement></div>
                    <div><CustomElement>2</CustomElement></div>
                    <div><CustomElement>3</CustomElement></div>
                    <div><CustomElement>4</CustomElement></div>
                    <div><CustomElement>5</CustomElement></div>
                    <div><CustomElement>6</CustomElement></div>
                    <div><CustomElement>7</CustomElement></div>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(xhtmlWithManyOccurrences, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);

        var customIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "CustomElement");
        Assert.NotNull(customIssue);
        Assert.Equal(7, customIssue.OccurrenceCount);

        // Should store only first 5 XPaths
        Assert.Equal(5, customIssue.XPaths.Count);

        // Verify XPath format
        Assert.All(customIssue.XPaths, xpath =>
        {
            Assert.StartsWith("/html[1]/body[1]/div[", xpath);
            Assert.EndsWith("]/CustomElement[1]", xpath);
        });
    }

    [Fact]
    public async Task ValidateXhtmlAsync_GeneratesCorrectXPath()
    {
        // Arrange
        var xhtml = @"
            <html>
                <body>
                    <section>
                        <div>
                            <CustomElement>Test</CustomElement>
                        </div>
                    </section>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(xhtml, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);

        var customIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "CustomElement");
        Assert.NotNull(customIssue);
        Assert.Single(customIssue.XPaths);

        // Verify exact XPath
        var xpath = customIssue.XPaths[0];
        Assert.Equal("/html[1]/body[1]/section[1]/div[1]/CustomElement[1]", xpath);
    }

    [Fact]
    public async Task ValidateXhtmlAsync_WithAllValidHtml5Elements_ReturnsSuccess()
    {
        // Arrange - test a sampling of valid HTML5 elements
        var validHtml5 = @"
            <html>
                <head>
                    <title>Test</title>
                    <meta/>
                    <link/>
                    <style></style>
                </head>
                <body>
                    <header><h1>Title</h1></header>
                    <nav><a href=""#"">Link</a></nav>
                    <main>
                        <article>
                            <section>
                                <p>Text with <strong>bold</strong>, <em>italic</em>, <code>code</code></p>
                                <blockquote>Quote</blockquote>
                                <ul><li>Item</li></ul>
                                <ol><li>Item</li></ol>
                                <table>
                                    <thead><tr><th>Header</th></tr></thead>
                                    <tbody><tr><td>Data</td></tr></tbody>
                                </table>
                            </section>
                        </article>
                    </main>
                    <footer>Footer</footer>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(validHtml5, enableSchemaValidation: false);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task ValidateXhtmlAsync_IssueDescription_IsCorrect()
    {
        // Arrange
        var xhtml = @"
            <html>
                <body>
                    <Table>Invalid</Table>
                    <CustomElement>Invalid</CustomElement>
                </body>
            </html>
        ";

        // Act
        // Skip schema validation in tests (external URL loading fails in test environment)
        var result = await _service.ValidateXhtmlAsync(xhtml, enableSchemaValidation: false);

        // Assert
        Assert.False(result.IsValid);

        var tableInvalidIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "Table");
        Assert.NotNull(tableInvalidIssue);
        Assert.Equal("'Table' is not a valid HTML element", tableInvalidIssue.Description);

        var tableUppercaseIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.UppercaseInElementName && i.ElementName == "Table");
        Assert.NotNull(tableUppercaseIssue);
        Assert.Equal("'Table' contains uppercase characters (HTML elements must be lowercase)", tableUppercaseIssue.Description);

        var customIssue = result.Issues.FirstOrDefault(i =>
            i.Type == ValidationIssueType.InvalidElement && i.ElementName == "CustomElement");
        Assert.NotNull(customIssue);
        Assert.Equal("'CustomElement' is not a valid HTML element", customIssue.Description);
    }
}

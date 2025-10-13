using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

public class ContentExtractionServiceTests
{
    private readonly Mock<ILogger<ContentExtractionService>> _mockLogger;
    private readonly ContentExtractionService _service;
    private static readonly XNamespace XhtmlNs = "http://www.w3.org/1999/xhtml";

    public ContentExtractionServiceTests()
    {
        _mockLogger = new Mock<ILogger<ContentExtractionService>>();
        _service = new ContentExtractionService(_mockLogger.Object);
    }

    [Fact]
    public void ExtractContent_WithNullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var header = new XElement(XhtmlNs + "h2", "Test Header");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.ExtractContent(null!, header));
    }

    [Fact]
    public void ExtractContent_WithNullStartHeader_ThrowsArgumentNullException()
    {
        // Arrange
        var doc = CreateSampleDocument();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.ExtractContent(doc, null!));
    }

    [Fact]
    public void ExtractContent_WithStartHeaderNotInDocument_ThrowsArgumentException()
    {
        // Arrange
        var doc = CreateSampleDocument();
        var externalHeader = new XElement(XhtmlNs + "h2", "External Header");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.ExtractContent(doc, externalHeader));
        Assert.Contains("not found in the provided document", exception.Message);
    }

    [Fact]
    public void ExtractContent_WithEndHeaderNotInDocument_ThrowsArgumentException()
    {
        // Arrange
        var doc = CreateSampleDocument();
        var startHeader = doc.Descendants(XhtmlNs + "h2").First();
        var externalEndHeader = new XElement(XhtmlNs + "h3", "External Header");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.ExtractContent(doc, startHeader, externalEndHeader));
        Assert.Contains("not found in the provided document", exception.Message);
    }

    [Fact]
    public void ExtractContent_BetweenH2Headers_ExtractsCorrectContent()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h1", "Main Title"),
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "p", "Paragraph 1"),
                    new XElement(XhtmlNs + "p", "Paragraph 2"),
                    new XElement(XhtmlNs + "h2", "Section 2"),
                    new XElement(XhtmlNs + "p", "Paragraph 3"))));

        var startHeader = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var result = _service.ExtractContent(doc, startHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Equal(3, elements.Count); // h2 + 2 paragraphs
        Assert.Equal("Section 1", elements[0].Value);
        Assert.Equal("Paragraph 1", elements[1].Value);
        Assert.Equal("Paragraph 2", elements[2].Value);
    }

    [Fact]
    public void ExtractContent_FromH3ToEndOfDocument_ExtractsAllRemaining()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section"),
                    new XElement(XhtmlNs + "h3", "Subsection"),
                    new XElement(XhtmlNs + "p", "Paragraph 1"),
                    new XElement(XhtmlNs + "p", "Paragraph 2"),
                    new XElement(XhtmlNs + "p", "Paragraph 3"))));

        var startHeader = doc.Descendants(XhtmlNs + "h3").First();

        // Act
        var result = _service.ExtractContent(doc, startHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Equal(4, elements.Count); // h3 + 3 paragraphs
        Assert.Equal("Subsection", elements[0].Value);
        Assert.Equal("Paragraph 3", elements[3].Value);
    }

    [Fact]
    public void ExtractContent_WithNestedElements_PreservesStructure()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section"),
                    new XElement(XhtmlNs + "table",
                        new XElement(XhtmlNs + "tr",
                            new XElement(XhtmlNs + "td", "Cell 1"),
                            new XElement(XhtmlNs + "td", "Cell 2"))),
                    new XElement(XhtmlNs + "ul",
                        new XElement(XhtmlNs + "li", "Item 1"),
                        new XElement(XhtmlNs + "li", "Item 2")),
                    new XElement(XhtmlNs + "h2", "Next Section"))));

        var startHeader = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var result = _service.ExtractContent(doc, startHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Equal(3, elements.Count); // h2, table, ul

        // Verify table structure
        var table = elements[1];
        Assert.Equal("table", table.Name.LocalName);
        Assert.Single(table.Descendants(XhtmlNs + "tr"));
        Assert.Equal(2, table.Descendants(XhtmlNs + "td").Count());

        // Verify list structure
        var list = elements[2];
        Assert.Equal("ul", list.Name.LocalName);
        Assert.Equal(2, list.Descendants(XhtmlNs + "li").Count());
    }

    [Fact]
    public void ExtractContent_WithUserSpecifiedEndHeader_StopsAtEndHeader()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "p", "Paragraph 1"),
                    new XElement(XhtmlNs + "h3", "Subsection"),
                    new XElement(XhtmlNs + "p", "Paragraph 2"),
                    new XElement(XhtmlNs + "h2", "Section 2"),
                    new XElement(XhtmlNs + "p", "Paragraph 3"))));

        var startHeader = doc.Descendants(XhtmlNs + "h2").First();
        var endHeader = doc.Descendants(XhtmlNs + "h3").First();

        // Act
        var result = _service.ExtractContent(doc, startHeader, endHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Equal(2, elements.Count); // h2 + paragraph 1 (stops before h3)
        Assert.Equal("Section 1", elements[0].Value);
        Assert.Equal("Paragraph 1", elements[1].Value);
    }

    [Fact]
    public void ExtractContent_SingleHeaderAtEnd_ReturnsJustHeader()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "p", "Paragraph"),
                    new XElement(XhtmlNs + "h2", "Last Section"))));

        var lastHeader = doc.Descendants(XhtmlNs + "h2").Last();

        // Act
        var result = _service.ExtractContent(doc, lastHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Single(elements); // Just the header
        Assert.Equal("Last Section", elements[0].Value);
    }

    [Fact]
    public void ExtractContent_HeaderWithNoContentBetween_ReturnsJustHeader()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "h2", "Section 2"),
                    new XElement(XhtmlNs + "p", "Paragraph"))));

        var firstHeader = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var result = _service.ExtractContent(doc, firstHeader);

        // Assert
        Assert.NotNull(result);
        var body = result.Root?.Element(XhtmlNs + "body");
        Assert.NotNull(body);

        var elements = body.Elements().ToList();
        Assert.Single(elements); // Just the header
        Assert.Equal("Section 1", elements[0].Value);
    }

    [Fact]
    public void FindNextHeader_WithSameLevelHeader_ReturnsNextHeader()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "p", "Paragraph"),
                    new XElement(XhtmlNs + "h2", "Section 2"))));

        var firstHeader = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var nextHeader = _service.FindNextHeader(doc, firstHeader);

        // Assert
        Assert.NotNull(nextHeader);
        Assert.Equal("Section 2", nextHeader.Value);
    }

    [Fact]
    public void FindNextHeader_WithHigherLevelHeader_ReturnsHigherHeader()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h3", "Subsection"),
                    new XElement(XhtmlNs + "p", "Paragraph"),
                    new XElement(XhtmlNs + "h2", "Main Section"))));

        var h3Header = doc.Descendants(XhtmlNs + "h3").First();

        // Act
        var nextHeader = _service.FindNextHeader(doc, h3Header);

        // Assert
        Assert.NotNull(nextHeader);
        Assert.Equal("Main Section", nextHeader.Value);
        Assert.Equal("h2", nextHeader.Name.LocalName);
    }

    [Fact]
    public void FindNextHeader_WithLowerLevelHeaders_SkipsThemAndFindsCorrectOne()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "h3", "Subsection 1"),
                    new XElement(XhtmlNs + "h4", "Sub-subsection"),
                    new XElement(XhtmlNs + "h3", "Subsection 2"),
                    new XElement(XhtmlNs + "h2", "Section 2"))));

        var firstH2 = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var nextHeader = _service.FindNextHeader(doc, firstH2);

        // Assert
        Assert.NotNull(nextHeader);
        Assert.Equal("Section 2", nextHeader.Value);
        Assert.Equal("h2", nextHeader.Name.LocalName);
    }

    [Fact]
    public void FindNextHeader_NoNextHeaderFound_ReturnsNull()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section"),
                    new XElement(XhtmlNs + "h3", "Subsection"),
                    new XElement(XhtmlNs + "p", "Paragraph"))));

        var h2Header = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var nextHeader = _service.FindNextHeader(doc, h2Header);

        // Assert
        Assert.Null(nextHeader);
    }

    [Fact]
    public void FindNextHeader_WithNullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var header = new XElement(XhtmlNs + "h2", "Test");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.FindNextHeader(null!, header));
    }

    [Fact]
    public void FindNextHeader_WithNullStartHeader_ThrowsArgumentNullException()
    {
        // Arrange
        var doc = CreateSampleDocument();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.FindNextHeader(doc, null!));
    }

    [Fact]
    public void ExtractContent_CreatesProperXhtmlStructure()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Section"),
                    new XElement(XhtmlNs + "p", "Paragraph"))));

        var header = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var result = _service.ExtractContent(doc, header);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Declaration);
        Assert.Equal("UTF-8", result.Declaration.Encoding);

        var root = result.Root;
        Assert.NotNull(root);
        Assert.Equal("html", root.Name.LocalName);
        Assert.Equal(XhtmlNs.NamespaceName, root.Name.NamespaceName);

        var head = root.Element(XhtmlNs + "head");
        Assert.NotNull(head);

        var meta = head.Element(XhtmlNs + "meta");
        Assert.NotNull(meta);
        Assert.Equal("UTF-8", meta.Attribute("charset")?.Value);

        var body = root.Element(XhtmlNs + "body");
        Assert.NotNull(body);
    }

    [Fact]
    public void ExtractContent_PreservesElementAttributes()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2",
                        new XAttribute("id", "section-1"),
                        new XAttribute("class", "main-header"),
                        "Section 1"),
                    new XElement(XhtmlNs + "p",
                        new XAttribute("class", "intro"),
                        "Paragraph"),
                    new XElement(XhtmlNs + "h2", "Section 2"))));

        var header = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        var result = _service.ExtractContent(doc, header);

        // Assert
        var body = result.Root?.Element(XhtmlNs + "body");
        var extractedHeader = body?.Element(XhtmlNs + "h2");

        Assert.NotNull(extractedHeader);
        Assert.Equal("section-1", extractedHeader.Attribute("id")?.Value);
        Assert.Equal("main-header", extractedHeader.Attribute("class")?.Value);

        var extractedParagraph = body?.Element(XhtmlNs + "p");
        Assert.NotNull(extractedParagraph);
        Assert.Equal("intro", extractedParagraph.Attribute("class")?.Value);
    }

    [Fact]
    public void ExtractContent_LogsExtractionDetails()
    {
        // Arrange
        var doc = new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h2", "Test Section"),
                    new XElement(XhtmlNs + "p", "Paragraph"))));

        var header = doc.Descendants(XhtmlNs + "h2").First();

        // Act
        _service.ExtractContent(doc, header);

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting content extraction")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted") && v.ToString()!.Contains("elements")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private XDocument CreateSampleDocument()
    {
        return new XDocument(
            new XElement(XhtmlNs + "html",
                new XElement(XhtmlNs + "body",
                    new XElement(XhtmlNs + "h1", "Title"),
                    new XElement(XhtmlNs + "h2", "Section 1"),
                    new XElement(XhtmlNs + "p", "Content"),
                    new XElement(XhtmlNs + "h2", "Section 2"))));
    }
}

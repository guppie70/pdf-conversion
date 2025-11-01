using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Services;

public class HeaderNormalizationServiceTests
{
    private readonly Mock<ILogger<HeaderNormalizationService>> _loggerMock;
    private readonly HeaderNormalizationService _service;

    public HeaderNormalizationServiceTests()
    {
        _loggerMock = new Mock<ILogger<HeaderNormalizationService>>();
        _service = new HeaderNormalizationService(_loggerMock.Object);
    }

    [Fact]
    public void NormalizeHeaders_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.NormalizeHeaders(null!));
    }

    [Fact]
    public void CalculateShiftAmount_NullContent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.CalculateShiftAmount(null!));
    }

    [Fact]
    public void CalculateShiftAmount_NoHeaders_ReturnsZero()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <p>Some paragraph</p>
                <p>Another paragraph</p>
            </div>");

        // Act
        var shift = _service.CalculateShiftAmount(content);

        // Assert
        Assert.Equal(0, shift);
    }

    [Fact]
    public void CalculateShiftAmount_FirstHeaderIsH1_ReturnsZero()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h1>First Header</h1>
                <p>Some content</p>
                <h2>Second Header</h2>
            </div>");

        // Act
        var shift = _service.CalculateShiftAmount(content);

        // Assert
        Assert.Equal(0, shift);
    }

    [Fact]
    public void CalculateShiftAmount_FirstHeaderIsH3_ReturnsNegativeTwo()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Market Risk</h3>
                <p>Introduction...</p>
                <h4>Interest Rate Risk</h4>
            </div>");

        // Act
        var shift = _service.CalculateShiftAmount(content);

        // Assert
        Assert.Equal(-2, shift);
    }

    [Fact]
    public void CalculateShiftAmount_FirstHeaderIsH5_ReturnsNegativeFour()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h5>Deep Header</h5>
                <p>Content</p>
            </div>");

        // Act
        var shift = _service.CalculateShiftAmount(content);

        // Assert
        Assert.Equal(-4, shift);
    }

    [Fact]
    public void NormalizeHeaders_FirstHeaderIsH1_NoChange()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h1>First Header</h1>
                <p>Some content</p>
                <h2>Second Header</h2>
                <h3>Third Header</h3>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("h3", headers[2].Name.LocalName);
    }

    [Fact]
    public void NormalizeHeaders_H3ToH1_ShiftsAllHeaders()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Market Risk</h3>
                <p>Introduction...</p>
                <h4>Interest Rate Risk</h4>
                <p>Details...</p>
                <h4>Currency Risk</h4>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal(3, headers.Count);
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("Market Risk", headers[0].Value);
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("Interest Rate Risk", headers[1].Value);
        Assert.Equal("h2", headers[2].Name.LocalName);
        Assert.Equal("Currency Risk", headers[2].Value);
    }

    [Fact]
    public void NormalizeHeaders_H5ToH1_ShiftsAllHeaders()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h5>Deep Header</h5>
                <p>Content</p>
                <h6>Deeper Header</h6>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal(2, headers.Count);
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("Deep Header", headers[0].Value);
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("Deeper Header", headers[1].Value);
    }

    [Fact]
    public void NormalizeHeaders_MultipleSameLevelHeaders_FirstBecomesH1_OthersH2()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Financial Results</h3>
                <p>Some content</p>
                <h3>Revenue Details</h3>
                <p>More content</p>
                <h3>Expenses</h3>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal(3, headers.Count);

        // First h3 becomes h1
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("Financial Results", headers[0].Value);

        // Second h3 becomes h2 (same-level sibling)
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("Revenue Details", headers[1].Value);

        // Third h3 becomes h2 (same-level sibling)
        Assert.Equal("h2", headers[2].Name.LocalName);
        Assert.Equal("Expenses", headers[2].Value);
    }

    [Fact]
    public void NormalizeHeaders_MultipleHeaderLevels_MaintainsHierarchy()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Level 3</h3>
                <h4>Level 4</h4>
                <h5>Level 5</h5>
                <h4>Level 4 Again</h4>
                <h3>Level 3 Again</h3>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal(5, headers.Count);

        // First h3 becomes h1
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("Level 3", headers[0].Value);

        // h4 under first h3 becomes h2 (nested under h1)
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("Level 4", headers[1].Value);

        // h5 under h4 becomes h3 (nested under h2)
        Assert.Equal("h3", headers[2].Name.LocalName);
        Assert.Equal("Level 5", headers[2].Value);

        // Second h4 becomes h2 (relative to last output level)
        Assert.Equal("h2", headers[3].Name.LocalName);
        Assert.Equal("Level 4 Again", headers[3].Value);

        // Second h3 becomes h2 (same-level sibling of first h3)
        Assert.Equal("h2", headers[4].Name.LocalName);
        Assert.Equal("Level 3 Again", headers[4].Value);
    }

    [Fact]
    public void NormalizeHeaders_SameLevelWithNestedChildren_MaintainsRelativeHierarchy()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>First Section</h3>
                <h4>Subsection 1A</h4>
                <p>Content</p>
                <h3>Second Section</h3>
                <h4>Subsection 2A</h4>
                <h5>Deep subsection 2A-1</h5>
                <h3>Third Section</h3>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal(6, headers.Count);

        // First h3 becomes h1
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("First Section", headers[0].Value);

        // h4 under first h3 becomes h2
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("Subsection 1A", headers[1].Value);

        // Second h3 becomes h2 (same-level sibling)
        Assert.Equal("h2", headers[2].Name.LocalName);
        Assert.Equal("Second Section", headers[2].Value);

        // h4 under second h3 becomes h3 (nested under h2)
        Assert.Equal("h3", headers[3].Name.LocalName);
        Assert.Equal("Subsection 2A", headers[3].Value);

        // h5 under h4 becomes h4 (nested under h3)
        Assert.Equal("h4", headers[4].Name.LocalName);
        Assert.Equal("Deep subsection 2A-1", headers[4].Value);

        // Third h3 becomes h2 (same-level sibling)
        Assert.Equal("h2", headers[5].Name.LocalName);
        Assert.Equal("Third Section", headers[5].Value);
    }

    [Fact]
    public void NormalizeHeaders_ClampsToH1_WhenShiftWouldGoBelowH1()
    {
        // Arrange - Hypothetical scenario where shift would create h0 or negative
        var content = XDocument.Parse(@"
            <div>
                <h1>Already H1</h1>
                <h2>H2</h2>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert - All headers should remain valid (h1-h6)
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.All(headers, h =>
        {
            var level = int.Parse(h.Name.LocalName.Substring(1));
            Assert.InRange(level, 1, 6);
        });
    }

    [Fact]
    public void NormalizeHeaders_ClampsToH6_WhenShiftWouldGoAboveH6()
    {
        // Arrange - This would require a positive shift, which shouldn't happen normally
        // But testing the clamping logic for completeness
        var content = XDocument.Parse(@"
            <div>
                <h6>H6</h6>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert - Header should remain h1 after normalization (h6 -> h1 with shift -5)
        var header = result.Descendants().First(e => e.Name.LocalName.StartsWith("h"));
        Assert.Equal("h1", header.Name.LocalName);
    }

    [Fact]
    public void NormalizeHeaders_PreservesAttributes()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3 id='main-header' class='section-header' data-level='3'>Market Risk</h3>
                <p>Content</p>
                <h4 id='sub-header' class='subsection-header'>Interest Rate Risk</h4>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var h1 = result.Descendants().First(e => e.Name.LocalName == "h1");
        Assert.Equal("main-header", h1.Attribute("id")?.Value);
        Assert.Equal("section-header", h1.Attribute("class")?.Value);
        Assert.Equal("3", h1.Attribute("data-level")?.Value);

        var h2 = result.Descendants().First(e => e.Name.LocalName == "h2");
        Assert.Equal("sub-header", h2.Attribute("id")?.Value);
        Assert.Equal("subsection-header", h2.Attribute("class")?.Value);
    }

    [Fact]
    public void NormalizeHeaders_PreservesNamespace()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div xmlns='http://www.w3.org/1999/xhtml'>
                <h3>Market Risk</h3>
                <h4>Interest Rate Risk</h4>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var headers = result.Descendants()
            .Where(e => e.Name.LocalName.StartsWith("h"))
            .ToList();

        Assert.All(headers, h =>
        {
            Assert.Equal("http://www.w3.org/1999/xhtml", h.Name.NamespaceName);
        });
    }

    [Fact]
    public void NormalizeHeaders_NoHeaders_ReturnsOriginalContent()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <p>Just paragraphs</p>
                <p>No headers here</p>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        Assert.Equal(content.ToString(), result.ToString());
    }

    [Fact]
    public void NormalizeHeaders_DoesNotModifyOriginal()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Market Risk</h3>
                <h4>Interest Rate Risk</h4>
            </div>");

        var originalXml = content.ToString();

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        Assert.Equal(originalXml, content.ToString());
        Assert.NotEqual(content.ToString(), result.ToString());
    }

    [Fact]
    public void NormalizeHeaders_ComplexNestedStructure_PreservesStructure()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Main Section</h3>
                <div class='subsection'>
                    <h4>Subsection</h4>
                    <p>Content in <strong>bold</strong></p>
                    <ul>
                        <li>List item</li>
                    </ul>
                    <h5>Deep subsection</h5>
                </div>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        // Verify structure is preserved
        var divs = result.Descendants("div").ToList();
        Assert.Equal(2, divs.Count);

        var ul = result.Descendants("ul").FirstOrDefault();
        Assert.NotNull(ul);

        var strong = result.Descendants("strong").FirstOrDefault();
        Assert.NotNull(strong);
        Assert.Equal("bold", strong.Value);

        // Verify headers were normalized
        var headers = result.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        Assert.Equal("h1", headers[0].Name.LocalName);
        Assert.Equal("h2", headers[1].Name.LocalName);
        Assert.Equal("h3", headers[2].Name.LocalName);
    }

    [Fact]
    public void NormalizeHeaders_PreservesChildElements()
    {
        // Arrange
        var content = XDocument.Parse(@"
            <div>
                <h3>Market <em>Risk</em> Analysis</h3>
                <h4>Interest <strong>Rate</strong> Risk</h4>
            </div>");

        // Act
        var result = _service.NormalizeHeaders(content);

        // Assert
        var h1 = result.Descendants("h1").FirstOrDefault();
        Assert.NotNull(h1);
        var em = h1.Descendants("em").FirstOrDefault();
        Assert.NotNull(em);
        Assert.Equal("Risk", em.Value);

        var h2 = result.Descendants("h2").FirstOrDefault();
        Assert.NotNull(h2);
        var strong = h2.Descendants("strong").FirstOrDefault();
        Assert.NotNull(strong);
        Assert.Equal("Rate", strong.Value);
    }
}

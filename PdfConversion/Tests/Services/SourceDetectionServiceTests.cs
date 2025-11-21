using Xunit;
using PdfConversion.Services;
using System.IO;

public class SourceDetectionServiceTests
{
    private readonly ISourceDetectionService _service;

    public SourceDetectionServiceTests()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<SourceDetectionService>();
        _service = new SourceDetectionService(logger);
    }

    [Fact]
    public void GetXsltPathForSource_AdobeSource_ReturnsAdobePath()
    {
        var result = _service.GetXsltPathForSource("adobe.xml");
        Assert.Equal("/app/xslt/adobe/transformation.xslt", result);
    }

    [Fact]
    public void GetXsltPathForSource_DoclingSource_ReturnsDoclingPath()
    {
        var result = _service.GetXsltPathForSource("docling-pdf.source.xml");
        Assert.Equal("/app/xslt/docling/transformation.xslt", result);
    }

    [Fact]
    public void GetMatchingHierarchyName_AdobeXml_ReturnsCorrectName()
    {
        var result = _service.GetMatchingHierarchyName("adobe.xml");
        Assert.Equal("hierarchy-adobe.xml", result);
    }

    [Fact]
    public void GetMatchingHierarchyName_DoclingPdfXhtml_ReturnsCorrectName()
    {
        var result = _service.GetMatchingHierarchyName("docling-pdf.xhtml");
        Assert.Equal("hierarchy-pdf-xhtml.xml", result);
    }
}

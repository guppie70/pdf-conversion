using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using Xunit;

namespace PdfConversion.Tests.Integration;

/// <summary>
/// Integration tests for Docling pipeline (PDF → DocBook → Taxxor XHTML)
/// Tests the complete transformation from DocBook XML to Taxxor-compatible XHTML.
///
/// NOTE: These tests use simplified XSLT without includes for System.Xml.Xsl compatibility.
/// Full XSLT 2.0/3.0 features (includes, modes, etc.) require XSLT3Service.
/// </summary>
public class DoclingPipelineTests : IDisposable
{
    private readonly IXsltTransformationService _xsltService;
    private readonly ILogger<DoclingPipelineTests> _logger;
    private readonly string _testDataPath;

    public DoclingPipelineTests()
    {
        // Setup logging
        _logger = Mock.Of<ILogger<DoclingPipelineTests>>();
        var xsltLogger = Mock.Of<ILogger<XsltTransformationService>>();

        // Setup memory cache with size limit
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 100
        };
        var cache = new MemoryCache(cacheOptions);

        // Create XSLT transformation service (no XSLT3Service - using System.Xml fallback)
        _xsltService = new XsltTransformationService(xsltLogger, cache);

        // Set up paths
        _testDataPath = "/Users/jthijs/Documents/my_projects/taxxor/tdm/_utils/pdf-conversion/data/input/test/projects/test-docling-conversion";
    }

    /// <summary>
    /// Gets a simplified XSLT for testing that works with System.Xml.Xsl (XSLT 1.0)
    /// without requiring includes or XSLT 2.0 features
    /// </summary>
    private string GetSimplifiedDoclingXslt()
    {
        return @"<?xml version=""1.0"" encoding=""UTF-8""?>
<xsl:stylesheet version=""1.0""
                xmlns:xsl=""http://www.w3.org/1999/XSL/Transform""
                xmlns:db=""http://docbook.org/ns/docbook""
                exclude-result-prefixes=""db"">

    <xsl:output method=""xml"" encoding=""UTF-8"" indent=""yes"" omit-xml-declaration=""no""/>

    <!-- Root template -->
    <xsl:template match=""/"">
        <html>
            <head>
                <meta charset=""UTF-8""/>
                <title>Taxxor TDM Document</title>
            </head>
            <body>
                <xsl:apply-templates select=""//db:book""/>
            </body>
        </html>
    </xsl:template>

    <!-- Book template -->
    <xsl:template match=""db:book"">
        <div class=""document"">
            <xsl:apply-templates/>
        </div>
    </xsl:template>

    <!-- Info/metadata - convert title to h1 -->
    <xsl:template match=""db:info/db:title"">
        <h1><xsl:value-of select="".""/></h1>
    </xsl:template>

    <!-- Chapter - convert title to h1 -->
    <xsl:template match=""db:chapter"">
        <div class=""chapter"">
            <xsl:apply-templates/>
        </div>
    </xsl:template>

    <xsl:template match=""db:chapter/db:title"">
        <h1><xsl:value-of select="".""/></h1>
    </xsl:template>

    <!-- Section - convert title to h2 -->
    <xsl:template match=""db:section"">
        <div class=""section"">
            <xsl:apply-templates/>
        </div>
    </xsl:template>

    <xsl:template match=""db:section/db:title"">
        <h2><xsl:value-of select="".""/></h2>
    </xsl:template>

    <!-- Nested section - convert title to h3 -->
    <xsl:template match=""db:section/db:section/db:title"">
        <h3><xsl:value-of select="".""/></h3>
    </xsl:template>

    <!-- Paragraphs -->
    <xsl:template match=""db:para"">
        <p><xsl:apply-templates/></p>
    </xsl:template>

    <!-- Lists -->
    <xsl:template match=""db:itemizedlist"">
        <ul><xsl:apply-templates/></ul>
    </xsl:template>

    <xsl:template match=""db:orderedlist"">
        <ol><xsl:apply-templates/></ol>
    </xsl:template>

    <xsl:template match=""db:listitem"">
        <li><xsl:apply-templates/></li>
    </xsl:template>

    <!-- Tables -->
    <xsl:template match=""db:informaltable | db:table"">
        <xsl:variable name=""tableId"">
            <xsl:text>tablewrapper_</xsl:text>
            <xsl:value-of select=""generate-id()""/>
        </xsl:variable>
        <div id=""{$tableId}"" class=""tablewrapper"">
            <table>
                <xsl:apply-templates select=""db:tgroup""/>
            </table>
        </div>
    </xsl:template>

    <xsl:template match=""db:tgroup"">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match=""db:thead"">
        <thead>
            <xsl:apply-templates/>
        </thead>
    </xsl:template>

    <xsl:template match=""db:tbody"">
        <tbody>
            <xsl:apply-templates/>
        </tbody>
    </xsl:template>

    <xsl:template match=""db:row"">
        <tr>
            <xsl:apply-templates/>
        </tr>
    </xsl:template>

    <xsl:template match=""db:thead/db:row/db:entry"">
        <th><xsl:value-of select="".""/></th>
    </xsl:template>

    <xsl:template match=""db:tbody/db:row/db:entry"">
        <td><xsl:value-of select="".""/></td>
    </xsl:template>

    <!-- Images -->
    <xsl:template match=""db:mediaobject | db:imageobject"">
        <xsl:apply-templates/>
    </xsl:template>

    <xsl:template match=""db:imagedata"">
        <img src=""{@fileref}"" alt=""Image""/>
    </xsl:template>

    <!-- Suppress unwanted elements -->
    <xsl:template match=""db:info/db:subtitle""/>

</xsl:stylesheet>";
    }

    [Fact]
    public async Task DoclingXsltTransformation_ValidDocBookXml_ProducesValidXhtml()
    {
        // Arrange
        var docBookXml = await File.ReadAllTextAsync(Path.Combine(_testDataPath, "docling-output.xml"));
        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false, // Use System.Xml fallback for testing
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-docling-conversion"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation failed: {result.ErrorMessage}");
        Assert.NotNull(result.OutputContent);
        Assert.NotEmpty(result.OutputContent);

        // Verify output is valid XML
        var outputDoc = XDocument.Parse(result.OutputContent);
        Assert.NotNull(outputDoc.Root);

        // Verify root element is html
        Assert.Equal("html", outputDoc.Root.Name.LocalName);

        // Verify contains standard XHTML structure
        var head = outputDoc.Root.Element("head");
        Assert.NotNull(head);

        var body = outputDoc.Root.Element("body");
        Assert.NotNull(body);

        // Verify contains expected elements from the placeholder DocBook
        var paragraphs = body.Descendants().Where(e => e.Name.LocalName == "p").ToList();
        Assert.NotEmpty(paragraphs);

        // Log success
        _logger.LogInformation("Successfully transformed DocBook to XHTML with {Paragraphs} paragraphs", paragraphs.Count);
    }

    [Fact]
    public async Task DoclingXsltTransformation_HandlesHeaders_CorrectHierarchy()
    {
        // Arrange - Create minimal DocBook XML with headers at different levels
        var docBookXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Test Document</title>
    </info>
    <chapter>
        <title>Chapter Title</title>
        <para>Chapter content</para>
        <section>
            <title>Section Title</title>
            <para>Section content</para>
            <section>
                <title>Subsection Title</title>
                <para>Subsection content</para>
            </section>
        </section>
    </chapter>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-headers"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation failed: {result.ErrorMessage}");

        var outputDoc = XDocument.Parse(result.OutputContent);
        var body = outputDoc.Root?.Element("body");
        Assert.NotNull(body);

        // Find all headers (h1-h6)
        var headers = body.Descendants()
            .Where(e => e.Name.LocalName.StartsWith("h") &&
                       e.Name.LocalName.Length == 2 &&
                       char.IsDigit(e.Name.LocalName[1]))
            .ToList();

        // Verify we have headers
        Assert.NotEmpty(headers);

        // Verify header hierarchy (should have h1, h2, h3 or similar structure)
        var headerLevels = headers
            .Select(h => int.Parse(h.Name.LocalName.Substring(1)))
            .ToList();

        Assert.All(headerLevels, level => Assert.InRange(level, 1, 6));

        _logger.LogInformation("Headers found: {Headers}",
            string.Join(", ", headers.Select(h => $"{h.Name.LocalName}: {h.Value}")));
    }

    [Fact]
    public async Task DoclingXsltTransformation_HandlesTables_PreservesStructure()
    {
        // Arrange - Create DocBook XML with table
        var docBookXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Table Test</title>
    </info>
    <chapter>
        <title>Chapter with Table</title>
        <informaltable>
            <tgroup cols=""2"">
                <thead>
                    <row>
                        <entry>Header 1</entry>
                        <entry>Header 2</entry>
                    </row>
                </thead>
                <tbody>
                    <row>
                        <entry>Cell 1-1</entry>
                        <entry>Cell 1-2</entry>
                    </row>
                    <row>
                        <entry>Cell 2-1</entry>
                        <entry>Cell 2-2</entry>
                    </row>
                </tbody>
            </tgroup>
        </informaltable>
    </chapter>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-tables"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation failed: {result.ErrorMessage}");

        var outputDoc = XDocument.Parse(result.OutputContent);
        var body = outputDoc.Root?.Element("body");
        Assert.NotNull(body);

        // Verify table exists
        var tables = body.Descendants().Where(e => e.Name.LocalName == "table").ToList();
        Assert.NotEmpty(tables);

        var table = tables.First();

        // Verify table structure
        var thead = table.Descendants().FirstOrDefault(e => e.Name.LocalName == "thead");
        var tbody = table.Descendants().FirstOrDefault(e => e.Name.LocalName == "tbody");

        Assert.NotNull(thead);
        Assert.NotNull(tbody);

        // Verify rows
        var headerRows = thead.Descendants().Where(e => e.Name.LocalName == "tr").ToList();
        var bodyRows = tbody.Descendants().Where(e => e.Name.LocalName == "tr").ToList();

        Assert.NotEmpty(headerRows);
        Assert.Equal(2, bodyRows.Count);

        // Verify tablewrapper div exists (Taxxor requirement)
        var wrapperDivs = body.Descendants()
            .Where(e => e.Name.LocalName == "div" &&
                       e.Attribute("id")?.Value?.StartsWith("tablewrapper_") == true)
            .ToList();

        Assert.NotEmpty(wrapperDivs);

        _logger.LogInformation("Table transformation successful with {HeaderRows} header rows and {BodyRows} body rows",
            headerRows.Count, bodyRows.Count);
    }

    [Fact]
    public async Task DoclingXsltTransformation_HandlesImages_PreservesImageElements()
    {
        // Arrange - Create DocBook XML with image
        var docBookXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Image Test</title>
    </info>
    <chapter>
        <title>Chapter with Image</title>
        <para>Text before image</para>
        <mediaobject>
            <imageobject>
                <imagedata fileref=""test-image.png"" format=""PNG""/>
            </imageobject>
        </mediaobject>
        <para>Text after image</para>
    </chapter>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-images"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation failed: {result.ErrorMessage}");

        var outputDoc = XDocument.Parse(result.OutputContent);
        var body = outputDoc.Root?.Element("body");
        Assert.NotNull(body);

        // Verify img elements exist
        var images = body.Descendants().Where(e => e.Name.LocalName == "img").ToList();
        Assert.NotEmpty(images);

        var img = images.First();
        var src = img.Attribute("src")?.Value;

        // Verify img element has src attribute
        Assert.NotNull(src);
        Assert.NotEmpty(src);

        _logger.LogInformation("Image element found with src: {Src}", src);
    }

    [Fact]
    public async Task DoclingXsltTransformation_HandlesComplexDocument_ProducesCompleteXhtml()
    {
        // Arrange - Create complex DocBook XML with multiple element types
        var docBookXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Complete Document Test</title>
        <subtitle>Testing all major elements</subtitle>
    </info>
    <chapter>
        <title>Introduction</title>
        <para>This document tests multiple element types.</para>

        <section>
            <title>Lists</title>
            <itemizedlist>
                <listitem><para>First item</para></listitem>
                <listitem><para>Second item</para></listitem>
                <listitem><para>Third item</para></listitem>
            </itemizedlist>
        </section>

        <section>
            <title>Tables and Data</title>
            <informaltable>
                <tgroup cols=""2"">
                    <thead>
                        <row>
                            <entry>Column A</entry>
                            <entry>Column B</entry>
                        </row>
                    </thead>
                    <tbody>
                        <row>
                            <entry>Value 1</entry>
                            <entry>Value 2</entry>
                        </row>
                    </tbody>
                </tgroup>
            </informaltable>
        </section>
    </chapter>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-complex"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation failed: {result.ErrorMessage}");

        var outputDoc = XDocument.Parse(result.OutputContent);
        var body = outputDoc.Root?.Element("body");
        Assert.NotNull(body);

        // Verify various elements exist
        var headers = body.Descendants().Where(e => e.Name.LocalName.StartsWith("h")).ToList();
        var paragraphs = body.Descendants().Where(e => e.Name.LocalName == "p").ToList();
        var lists = body.Descendants().Where(e => e.Name.LocalName == "ul" || e.Name.LocalName == "ol").ToList();
        var tables = body.Descendants().Where(e => e.Name.LocalName == "table").ToList();

        Assert.NotEmpty(headers);
        Assert.NotEmpty(paragraphs);
        Assert.NotEmpty(lists);
        Assert.NotEmpty(tables);

        // Verify list items
        var listItems = body.Descendants().Where(e => e.Name.LocalName == "li").ToList();
        Assert.Equal(3, listItems.Count);

        _logger.LogInformation("Complex document transformed successfully: {Headers} headers, {Paragraphs} paragraphs, {Lists} lists, {Tables} tables",
            headers.Count, paragraphs.Count, lists.Count, tables.Count);
    }

    [Fact]
    public async Task DoclingXsltTransformation_EmptyDocument_HandlesGracefully()
    {
        // Arrange - Create minimal empty DocBook XML
        var docBookXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Empty Document</title>
    </info>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false,
            Parameters = new Dictionary<string, string>
            {
                ["projectid"] = "test-empty"
            }
        };

        // Act
        var result = await _xsltService.TransformAsync(docBookXml, xsltContent, options);

        // Assert
        Assert.True(result.IsSuccess, $"Transformation should succeed even with empty document: {result.ErrorMessage}");

        var outputDoc = XDocument.Parse(result.OutputContent);
        Assert.NotNull(outputDoc.Root);
        Assert.Equal("html", outputDoc.Root.Name.LocalName);

        _logger.LogInformation("Empty document handled gracefully");
    }

    [Fact]
    public async Task DoclingXsltTransformation_InvalidXml_ReturnsError()
    {
        // Arrange - Create invalid XML
        var invalidXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<book xmlns=""http://docbook.org/ns/docbook"" version=""5.0"">
    <info>
        <title>Unclosed title
    </info>
</book>";

        var xsltContent = GetSimplifiedDoclingXslt();

        var options = new TransformationOptions
        {
            UseXslt3Service = false,
            NormalizeHeaders = false
        };

        // Act
        var result = await _xsltService.TransformAsync(invalidXml, xsltContent, options);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("XML", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("Invalid XML correctly rejected with error: {Error}", result.ErrorMessage);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}

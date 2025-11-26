namespace PdfConversion.Services;

/// <summary>
/// Service for detecting source file patterns and determining appropriate XSLT stylesheets.
/// Supports multiple source files (Adobe XML, Docling outputs) within a single project.
/// </summary>
public interface ISourceDetectionService
{
    /// <summary>
    /// Determines the XSLT transformation path based on the source filename.
    /// </summary>
    /// <param name="sourceFileName">The source XML filename (e.g., "adobe.xml", "docling-pdf.source.xml")</param>
    /// <returns>Full path to the appropriate XSLT stylesheet (e.g., "/app/xslt/adobe/transformation.xslt")</returns>
    string GetXsltPathForSource(string sourceFileName);

    /// <summary>
    /// Generates the matching hierarchy filename based on the normalized XML filename.
    /// </summary>
    /// <param name="normalizedXmlName">The normalized XML filename (e.g., "adobe.xml", "docling-pdf.xml", "docling-pdf.xhtml")</param>
    /// <returns>Matching hierarchy filename (e.g., "hierarchy-adobe.xml", "hierarchy-pdf-xml.xml", "hierarchy-pdf-xhtml.xml")</returns>
    string GetMatchingHierarchyName(string normalizedXmlName);

    /// <summary>
    /// Generates the normalized XML filename based on the source XML filename.
    /// </summary>
    /// <param name="sourceFileName">The source XML filename (e.g., "adobe.xml", "docling-pdf.source.xml")</param>
    /// <returns>Normalized XML filename (e.g., "adobe.xml", "docling-pdf.xml", "docling-pdf.xhtml")</returns>
    string GetNormalizedXmlName(string sourceFileName);
}

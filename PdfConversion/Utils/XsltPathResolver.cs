namespace PdfConversion.Utils;

/// <summary>
/// Utility for resolving XSLT transformation file paths based on source XML filenames.
/// Centralizes the logic for detecting whether to use Docling or Adobe XSLT pipelines.
/// </summary>
public static class XsltPathResolver
{
    /// <summary>
    /// Resolves the full XSLT transformation file path based on the source XML filename.
    /// </summary>
    /// <param name="sourceXmlFileName">The source XML filename (e.g., "docling-output.xml", "input.xml")</param>
    /// <returns>Full path to the XSLT transformation file (e.g., "/app/xslt/docling/transformation.xslt")</returns>
    /// <exception cref="ArgumentException">Thrown when sourceXmlFileName is null or empty</exception>
    /// <remarks>
    /// Detection logic:
    /// - If filename contains "docling" (case-insensitive) → Uses Docling pipeline
    /// - Otherwise → Uses Adobe pipeline
    /// </remarks>
    public static string GetTransformationPath(string sourceXmlFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceXmlFileName))
            throw new ArgumentException("Source XML filename cannot be null or empty", nameof(sourceXmlFileName));

        return sourceXmlFileName.Contains("docling", StringComparison.OrdinalIgnoreCase)
            ? "/app/xslt/docling/transformation.xslt"
            : "/app/xslt/adobe/transformation.xslt";
    }

    /// <summary>
    /// Resolves the relative XSLT transformation file path based on the source XML filename.
    /// </summary>
    /// <param name="sourceXmlFileName">The source XML filename (e.g., "docling-output.xml", "input.xml")</param>
    /// <returns>Relative path to the XSLT transformation file (e.g., "docling/transformation.xslt")</returns>
    /// <exception cref="ArgumentException">Thrown when sourceXmlFileName is null or empty</exception>
    public static string GetRelativeTransformationPath(string sourceXmlFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceXmlFileName))
            throw new ArgumentException("Source XML filename cannot be null or empty", nameof(sourceXmlFileName));

        return sourceXmlFileName.Contains("docling", StringComparison.OrdinalIgnoreCase)
            ? "docling/transformation.xslt"
            : "adobe/transformation.xslt";
    }
}

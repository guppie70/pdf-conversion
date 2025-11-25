using Microsoft.Extensions.Logging;

namespace PdfConversion.Services;

/// <summary>
/// Implementation of source file detection and XSLT path resolution.
/// Handles both Adobe and Docling source files, detecting workstream from filename patterns.
/// </summary>
public class SourceDetectionService : ISourceDetectionService
{
    private readonly ILogger<SourceDetectionService> _logger;

    public SourceDetectionService(ILogger<SourceDetectionService> logger)
    {
        _logger = logger;
    }

    public string GetXsltPathForSource(string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source filename cannot be null or empty", nameof(sourceFileName));
        }

        // Extract just the filename if it includes a path
        var fileName = Path.GetFileName(sourceFileName);
        var lowerFileName = fileName.ToLowerInvariant();

        _logger.LogInformation("Detecting XSLT for source file: '{FileName}' (from path: '{FullPath}')", fileName, sourceFileName);

        // Detect workstream from filename
        if (lowerFileName.StartsWith("adobe"))
        {
            _logger.LogInformation("Detected Adobe workstream from source: {SourceFileName}", fileName);
            return "/app/xslt/adobe/transformation.xslt";
        }
        else if (lowerFileName.StartsWith("docling"))
        {
            _logger.LogInformation("Detected Docling workstream from source: {SourceFileName}", fileName);
            return "/app/xslt/docling/transformation.xslt";
        }
        else
        {
            // Default to Adobe for backward compatibility
            _logger.LogWarning("Could not detect workstream from source: {SourceFileName}, defaulting to Adobe", fileName);
            return "/app/xslt/adobe/transformation.xslt";
        }
    }

    public string GetMatchingHierarchyName(string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source filename cannot be null or empty", nameof(sourceFileName));
        }

        // For files like "adobe.xml" → "hierarchy-adobe.xml"
        // For files like "docling-pdf.xhtml" → "hierarchy-pdf-xhtml.xml"
        // For files like "docling-pdf.source.xml" → "hierarchy-pdf-source.xml"

        // Get base name with dots converted to hyphens
        var baseName = GetBaseNameWithExtensionsAsHyphens(sourceFileName);

        // Extract the portion after the workstream prefix
        string suffix;
        if (baseName.StartsWith("adobe", StringComparison.OrdinalIgnoreCase))
        {
            suffix = "adobe";
        }
        else if (baseName.StartsWith("docling-", StringComparison.OrdinalIgnoreCase))
        {
            // Remove "docling-" prefix and use the rest
            suffix = baseName.Substring(8); // "docling-".Length = 8
        }
        else
        {
            // Fallback: use entire base name
            suffix = baseName;
        }

        var hierarchyName = $"hierarchy-{suffix}.xml";
        _logger.LogDebug("Generated hierarchy name: {HierarchyName} for source: {SourceFileName}",
            hierarchyName, sourceFileName);

        return hierarchyName;
    }

    public string GetNormalizedXmlName(string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source filename cannot be null or empty", nameof(sourceFileName));
        }

        // For files like "adobe.xml" → "normalized-adobe.xml"
        // For files like "docling-pdf.source.xml" → "normalized-pdf-source.xml"

        // Get base name with dots converted to hyphens
        var baseName = GetBaseNameWithExtensionsAsHyphens(sourceFileName);

        // Extract the portion after the workstream prefix
        string suffix;
        if (baseName.StartsWith("adobe", StringComparison.OrdinalIgnoreCase))
        {
            suffix = "adobe";
        }
        else if (baseName.StartsWith("docling-", StringComparison.OrdinalIgnoreCase))
        {
            // Remove "docling-" prefix and use the rest
            suffix = baseName.Substring(8); // "docling-".Length = 8
        }
        else
        {
            // Fallback: use entire base name
            suffix = baseName;
        }

        var normalizedName = $"normalized-{suffix}.xml";
        _logger.LogDebug("Generated normalized XML name: {NormalizedName} for source: {SourceFileName}",
            normalizedName, sourceFileName);

        return normalizedName;
    }

    /// <summary>
    /// Converts a source filename to a base name suitable for hierarchy/normalized naming.
    /// Converts all dots to hyphens and removes the final .xml extension only.
    /// Examples:
    /// - "adobe.xml" → "adobe"
    /// - "docling-pdf.xhtml" → "docling-pdf-xhtml"
    /// - "docling-pdf.source.xml" → "docling-pdf-source"
    /// </summary>
    /// <param name="fileName">The filename to process</param>
    /// <returns>Base name with all dots converted to hyphens except final .xml removed</returns>
    private static string GetBaseNameWithExtensionsAsHyphens(string fileName)
    {
        // Get just the filename without path
        var name = Path.GetFileName(fileName);

        // Only remove the final .xml extension (not .xhtml)
        // This treats .xhtml as part of the identifier
        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }

        // Convert all remaining dots to hyphens
        // This handles: "pdf.xhtml" → "pdf-xhtml", "pdf.source" → "pdf-source"
        name = name.Replace('.', '-');

        return name;
    }
}

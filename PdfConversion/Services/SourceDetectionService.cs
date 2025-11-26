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

    public string GetMatchingHierarchyName(string normalizedXmlName)
    {
        if (string.IsNullOrWhiteSpace(normalizedXmlName))
        {
            throw new ArgumentException("Normalized XML filename cannot be null or empty", nameof(normalizedXmlName));
        }

        // adobe.xml → hierarchy-adobe.xml
        // docling-pdf.xml → hierarchy-pdf-xml.xml
        // docling-pdf.xhtml → hierarchy-pdf-xhtml.xml
        // docling-word.xml → hierarchy-word-xml.xml
        // docling-word.xhtml → hierarchy-word-xhtml.xml

        var fileName = Path.GetFileName(normalizedXmlName);
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        if (nameWithoutExtension.Equals("adobe", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Generated hierarchy name: hierarchy-adobe.xml for normalized: {NormalizedXmlName}", normalizedXmlName);
            return "hierarchy-adobe.xml";
        }

        if (nameWithoutExtension.StartsWith("docling-pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (extension.Equals(".xhtml", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Generated hierarchy name: hierarchy-pdf-xhtml.xml for normalized: {NormalizedXmlName}", normalizedXmlName);
                return "hierarchy-pdf-xhtml.xml";
            }
            _logger.LogDebug("Generated hierarchy name: hierarchy-pdf-xml.xml for normalized: {NormalizedXmlName}", normalizedXmlName);
            return "hierarchy-pdf-xml.xml";
        }

        if (nameWithoutExtension.StartsWith("docling-word", StringComparison.OrdinalIgnoreCase))
        {
            if (extension.Equals(".xhtml", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Generated hierarchy name: hierarchy-word-xhtml.xml for normalized: {NormalizedXmlName}", normalizedXmlName);
                return "hierarchy-word-xhtml.xml";
            }
            _logger.LogDebug("Generated hierarchy name: hierarchy-word-xml.xml for normalized: {NormalizedXmlName}", normalizedXmlName);
            return "hierarchy-word-xml.xml";
        }

        throw new ArgumentException($"Unknown normalized XML filename pattern: {fileName}");
    }

    public string GetNormalizedXmlName(string sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source filename cannot be null or empty", nameof(sourceFileName));
        }

        // adobe.xml → adobe.xml
        // docling-pdf.source.xml → docling-pdf.xml
        // docling-pdf.source.html → docling-pdf.xhtml
        // docling-word.source.xml → docling-word.xml
        // docling-word.source.html → docling-word.xhtml

        var fileName = Path.GetFileName(sourceFileName);

        if (fileName.Equals("adobe.xml", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Normalized XML name: adobe.xml for source: {SourceFileName}", sourceFileName);
            return "adobe.xml";
        }

        if (fileName.EndsWith(".source.html", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedName = fileName.Replace(".source.html", ".xhtml", StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Generated normalized XML name: {NormalizedName} for source: {SourceFileName}",
                normalizedName, sourceFileName);
            return normalizedName;
        }

        if (fileName.EndsWith(".source.xml", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedName = fileName.Replace(".source.xml", ".xml", StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("Generated normalized XML name: {NormalizedName} for source: {SourceFileName}",
                normalizedName, sourceFileName);
            return normalizedName;
        }

        throw new ArgumentException($"Unknown source filename pattern: {fileName}");
    }
}

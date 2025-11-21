using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System.Xml.Linq;
using System.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for generating diffs between XML documents
/// </summary>
public interface IDiffService
{
    /// <summary>
    /// Generates a detailed diff between two XML strings
    /// </summary>
    /// <param name="originalXml">The original XML string</param>
    /// <param name="reconstructedXml">The reconstructed XML string</param>
    /// <param name="debugOutputDirectory">Optional directory to save debug files</param>
    /// <returns>Diff result containing line-by-line comparison</returns>
    DiffResult GenerateXmlDiff(string originalXml, string reconstructedXml, string? debugOutputDirectory = null);
}

/// <summary>
/// Implementation of diff service using DiffPlex
/// </summary>
public class DiffService : IDiffService
{
    private readonly ILogger<DiffService> _logger;

    public DiffService(ILogger<DiffService> logger)
    {
        _logger = logger;
    }

    public DiffResult GenerateXmlDiff(string originalXml, string reconstructedXml, string? debugOutputDirectory = null)
    {
        try
        {
            _logger.LogInformation("Generating diff between original and reconstructed XML");
            _logger.LogInformation("Original XML size: {OriginalSize} chars, Reconstructed XML size: {ReconstructedSize} chars",
                originalXml.Length, reconstructedXml.Length);

            // Extract body content from both documents for meaningful comparison
            string originalBodyContent, reconstructedBodyContent;

            try
            {
                originalBodyContent = ExtractBodyContent(originalXml);
                _logger.LogInformation("Extracted body content from original XML, body size: {Size} chars", originalBodyContent.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract body from original XML, using full document");
                originalBodyContent = originalXml;
            }

            try
            {
                reconstructedBodyContent = ExtractBodyContent(reconstructedXml);
                _logger.LogInformation("Extracted body content from reconstructed XML, body size: {Size} chars", reconstructedBodyContent.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract body from reconstructed XML, using full document");
                reconstructedBodyContent = reconstructedXml;
            }

            // Pretty print both extracted body contents for consistent comparison
            string formattedOriginal, formattedReconstructed;

            try
            {
                formattedOriginal = PrettyPrintXml(originalBodyContent);
                _logger.LogInformation("Pretty-printed original body content, formatted size: {Size} chars", formattedOriginal.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pretty-print original body content, using as-is");
                formattedOriginal = originalBodyContent;
            }

            try
            {
                formattedReconstructed = PrettyPrintXml(reconstructedBodyContent);
                _logger.LogInformation("Pretty-printed reconstructed body content, formatted size: {Size} chars", formattedReconstructed.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to pretty-print reconstructed body content, using as-is");
                formattedReconstructed = reconstructedBodyContent;
            }

            // DEBUG: Save the extracted and formatted body content for comparison
            if (!string.IsNullOrEmpty(debugOutputDirectory))
            {
                try
                {
                    var originalBodyPath = Path.Combine(debugOutputDirectory, "3-original-body-extracted.xml");
                    File.WriteAllText(originalBodyPath, formattedOriginal);
                    _logger.LogInformation("Saved extracted original body to: {Path}", originalBodyPath);

                    var reconstructedBodyPath = Path.Combine(debugOutputDirectory, "4-reconstructed-body-extracted.xml");
                    File.WriteAllText(reconstructedBodyPath, formattedReconstructed);
                    _logger.LogInformation("Saved extracted reconstructed body to: {Path}", reconstructedBodyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save debug files to {Directory}", debugOutputDirectory);
                }
            }

            // Use DiffPlex to generate side-by-side diff
            var differ = new Differ();
            var builder = new SideBySideDiffBuilder(differ);
            var diffResult = builder.BuildDiffModel(formattedOriginal, formattedReconstructed);

            // Count differences
            int linesAdded = diffResult.NewText.Lines.Count(l => l.Type == ChangeType.Inserted);
            int linesDeleted = diffResult.OldText.Lines.Count(l => l.Type == ChangeType.Deleted);
            int linesModified = diffResult.OldText.Lines.Count(l => l.Type == ChangeType.Modified);
            int linesUnchanged = diffResult.OldText.Lines.Count(l => l.Type == ChangeType.Unchanged);

            int totalLines = diffResult.OldText.Lines.Count;
            double matchPercentage = totalLines > 0
                ? (linesUnchanged * 100.0 / totalLines)
                : 0;

            bool isPerfectMatch = linesAdded == 0 && linesDeleted == 0 && linesModified == 0;

            _logger.LogInformation("Diff complete: {Unchanged} unchanged, {Modified} modified, {Added} added, {Deleted} deleted",
                linesUnchanged, linesModified, linesAdded, linesDeleted);
            _logger.LogInformation("Total lines in original: {TotalLines}, Match percentage: {MatchPercentage:F2}%",
                totalLines, matchPercentage);

            return new DiffResult
            {
                IsPerfectMatch = isPerfectMatch,
                LinesAdded = linesAdded,
                LinesDeleted = linesDeleted,
                LinesModified = linesModified,
                LinesUnchanged = linesUnchanged,
                MatchPercentage = matchPercentage,
                OldTextLines = diffResult.OldText.Lines,
                NewTextLines = diffResult.NewText.Lines
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating XML diff");
            throw;
        }
    }

    /// <summary>
    /// Extracts the body content from an XHTML document for comparison
    /// </summary>
    private string ExtractBodyContent(string xml)
    {
        var doc = XDocument.Parse(xml);

        // Define possible namespaces
        XNamespace xhtmlNs = "http://www.w3.org/1999/xhtml";
        XNamespace noNs = XNamespace.None;

        // Debug: Log document root info
        if (doc.Root != null)
        {
            _logger.LogDebug("Document root element: {RootName}, Namespace: {Namespace}",
                doc.Root.Name.LocalName, doc.Root.Name.NamespaceName);
        }

        // Try to find body element with XHTML namespace first
        var body = doc.Descendants(xhtmlNs + "body").FirstOrDefault();

        // If not found, try without namespace
        if (body == null)
        {
            _logger.LogDebug("Body with XHTML namespace not found, trying without namespace");
            body = doc.Descendants("body").FirstOrDefault();
        }

        if (body == null)
        {
            _logger.LogWarning("No body element found in XML, returning full document");
            _logger.LogDebug("Available elements: {Elements}",
                string.Join(", ", doc.Descendants().Select(e => e.Name.LocalName).Distinct().Take(20)));
            return xml;
        }

        _logger.LogDebug("Found body element, child count: {ChildCount}", body.Elements().Count());

        // Create a new document with just the body content
        var bodyDoc = new XDocument(
            new XElement("body",
                body.Elements().Select(e => RemoveNamespaces(e))
            )
        );

        return bodyDoc.ToString();
    }

    /// <summary>
    /// Recursively removes namespaces from an element and its descendants
    /// </summary>
    private XElement RemoveNamespaces(XElement element)
    {
        return new XElement(
            element.Name.LocalName,
            element.Attributes()
                .Where(a => !a.IsNamespaceDeclaration && a.Name.LocalName != "lang")
                .Select(a => new XAttribute(a.Name.LocalName, a.Value)),
            element.Nodes().Select(n =>
            {
                if (n is XElement e)
                    return RemoveNamespaces(e);
                return n;
            })
        );
    }

    /// <summary>
    /// Pretty prints XML for consistent formatting before comparison
    /// </summary>
    private string PrettyPrintXml(string xml)
    {
        var doc = XDocument.Parse(xml);

        var settings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = System.Xml.NewLineHandling.Replace,
            OmitXmlDeclaration = false,
            Encoding = System.Text.Encoding.UTF8
        };

        using var stringWriter = new System.IO.StringWriter();
        using (var xmlWriter = System.Xml.XmlWriter.Create(stringWriter, settings))
        {
            doc.Save(xmlWriter);
        }
        return stringWriter.ToString();
    }
}

/// <summary>
/// Result of a diff operation
/// </summary>
public class DiffResult
{
    /// <summary>
    /// Whether the documents match perfectly (no differences)
    /// </summary>
    public bool IsPerfectMatch { get; set; }

    /// <summary>
    /// Number of lines added in the new document
    /// </summary>
    public int LinesAdded { get; set; }

    /// <summary>
    /// Number of lines deleted from the original document
    /// </summary>
    public int LinesDeleted { get; set; }

    /// <summary>
    /// Number of lines that were modified
    /// </summary>
    public int LinesModified { get; set; }

    /// <summary>
    /// Number of lines that are unchanged
    /// </summary>
    public int LinesUnchanged { get; set; }

    /// <summary>
    /// Percentage of lines that match (0-100)
    /// </summary>
    public double MatchPercentage { get; set; }

    /// <summary>
    /// Lines from the original document with change information
    /// </summary>
    public List<DiffPiece> OldTextLines { get; set; } = new();

    /// <summary>
    /// Lines from the reconstructed document with change information
    /// </summary>
    public List<DiffPiece> NewTextLines { get; set; } = new();

    /// <summary>
    /// Total number of differences (added + deleted + modified)
    /// </summary>
    public int TotalDifferences => LinesAdded + LinesDeleted + LinesModified;
}

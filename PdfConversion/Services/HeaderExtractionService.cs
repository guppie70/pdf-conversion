using System.Xml.Linq;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for extracting header elements from Normalized XML documents.
/// </summary>
public interface IHeaderExtractionService
{
    /// <summary>
    /// Extracts all header elements (h1-h6) from the specified XML file.
    /// </summary>
    /// <param name="xmlFilePath">Absolute path to the Normalized XML file</param>
    /// <returns>List of DocumentHeader objects</returns>
    Task<List<DocumentHeader>> ExtractHeadersAsync(string xmlFilePath);

    /// <summary>
    /// Finds a header by its XPath expression.
    /// </summary>
    /// <param name="headers">List of headers to search</param>
    /// <param name="xpath">XPath expression to match</param>
    /// <returns>Matching header or null</returns>
    DocumentHeader? FindHeaderByXPath(List<DocumentHeader> headers, string xpath);

    /// <summary>
    /// Marks headers as used based on the current hierarchy tree.
    /// </summary>
    /// <param name="headers">All extracted headers</param>
    /// <param name="hierarchyXmlPath">Path to the hierarchy XML file</param>
    Task MarkUsedHeadersAsync(List<DocumentHeader> headers, string hierarchyXmlPath);
}

public class HeaderExtractionService : IHeaderExtractionService
{
    private readonly ILogger<HeaderExtractionService> _logger;
    private static readonly string[] HeaderLevels = { "h1", "h2", "h3", "h4", "h5", "h6" };

    public HeaderExtractionService(ILogger<HeaderExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<List<DocumentHeader>> ExtractHeadersAsync(string xmlFilePath)
    {
        try
        {
            if (!File.Exists(xmlFilePath))
            {
                _logger.LogWarning("XML file not found: {FilePath}", xmlFilePath);
                return new List<DocumentHeader>();
            }

            var headers = new List<DocumentHeader>();
            var doc = await Task.Run(() => XDocument.Load(xmlFilePath));
            var headerCounters = new Dictionary<string, int>();

            foreach (var level in HeaderLevels)
            {
                headerCounters[level] = 0;
            }

            // Find all header elements
            foreach (var level in HeaderLevels)
            {
                var elements = doc.Descendants(level).ToList();

                foreach (var element in elements)
                {
                    headerCounters[level]++;

                    var header = new DocumentHeader
                    {
                        Id = $"{level}_{headerCounters[level]}",
                        Level = level,
                        Title = GetElementText(element),
                        XPath = GetXPath(element),
                        Context = GetContext(element),
                        IsUsed = false
                    };

                    headers.Add(header);
                }
            }

            _logger.LogInformation("Extracted {Count} headers from {FilePath}", headers.Count, xmlFilePath);
            return headers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract headers from {FilePath}", xmlFilePath);
            return new List<DocumentHeader>();
        }
    }

    public DocumentHeader? FindHeaderByXPath(List<DocumentHeader> headers, string xpath)
    {
        return headers.FirstOrDefault(h => h.XPath.Equals(xpath, StringComparison.OrdinalIgnoreCase));
    }

    public async Task MarkUsedHeadersAsync(List<DocumentHeader> headers, string hierarchyXmlPath)
    {
        try
        {
            if (!File.Exists(hierarchyXmlPath))
            {
                _logger.LogDebug("Hierarchy file not found, no headers marked as used: {FilePath}", hierarchyXmlPath);
                return;
            }

            var doc = await Task.Run(() => XDocument.Load(hierarchyXmlPath));
            var usedXPaths = new HashSet<string>();

            // Find all data-ref attributes in hierarchy
            var items = doc.Descendants("item");
            foreach (var item in items)
            {
                var dataRef = item.Attribute("data-ref")?.Value;
                if (!string.IsNullOrEmpty(dataRef))
                {
                    usedXPaths.Add(dataRef);
                }
            }

            // Mark headers as used
            foreach (var header in headers)
            {
                header.IsUsed = usedXPaths.Contains(header.XPath);
            }

            _logger.LogDebug("Marked {Count} headers as used from hierarchy", usedXPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark used headers from {FilePath}", hierarchyXmlPath);
        }
    }

    /// <summary>
    /// Gets the text content of an element, trimmed and cleaned.
    /// </summary>
    private static string GetElementText(XElement element)
    {
        var text = element.Value.Trim();

        // Limit length for display
        if (text.Length > 100)
        {
            text = text.Substring(0, 97) + "...";
        }

        return text;
    }

    /// <summary>
    /// Generates an XPath expression for the element.
    /// Uses position-based XPath for uniqueness.
    /// </summary>
    private static string GetXPath(XElement element)
    {
        var parts = new List<string>();
        var current = element;

        while (current != null)
        {
            var name = current.Name.LocalName;
            var position = GetElementPosition(current);
            parts.Insert(0, $"{name}[{position}]");
            current = current.Parent;
        }

        return "/" + string.Join("/", parts);
    }

    /// <summary>
    /// Gets the position of an element among its siblings with the same name.
    /// </summary>
    private static int GetElementPosition(XElement element)
    {
        var siblings = element.Parent?.Elements(element.Name) ?? Enumerable.Empty<XElement>();
        return siblings.TakeWhile(e => e != element).Count() + 1;
    }

    /// <summary>
    /// Gets surrounding context for preview (previous and next sibling text).
    /// </summary>
    private static string GetContext(XElement element)
    {
        var context = new List<string>();

        // Get previous sibling
        var prev = element.PreviousNode as XElement;
        if (prev != null)
        {
            var prevText = prev.Value.Trim();
            if (prevText.Length > 50)
            {
                prevText = "..." + prevText.Substring(prevText.Length - 47);
            }
            context.Add(prevText);
        }

        // Get next sibling
        var next = element.NextNode as XElement;
        if (next != null)
        {
            var nextText = next.Value.Trim();
            if (nextText.Length > 50)
            {
                nextText = nextText.Substring(0, 47) + "...";
            }
            context.Add(nextText);
        }

        return string.Join(" | ", context);
    }
}

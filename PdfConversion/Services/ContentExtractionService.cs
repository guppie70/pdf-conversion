using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for extracting content sections from transformed XHTML based on header matches.
/// </summary>
public class ContentExtractionService : IContentExtractionService
{
    private readonly ILogger<ContentExtractionService> _logger;
    private static readonly XNamespace XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    public ContentExtractionService(ILogger<ContentExtractionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public XDocument ExtractContent(
        XDocument transformedXhtml,
        XElement startHeader,
        XElement? endHeader = null)
    {
        ArgumentNullException.ThrowIfNull(transformedXhtml);
        ArgumentNullException.ThrowIfNull(startHeader);

        _logger.LogInformation(
            "Starting content extraction from header: {HeaderText} (level: {Level})",
            startHeader.Value.Trim(),
            GetHeaderLevel(startHeader));

        // Verify startHeader is in the document
        if (!IsElementInDocument(transformedXhtml, startHeader))
        {
            throw new ArgumentException("Start header is not found in the provided document.", nameof(startHeader));
        }

        // Verify endHeader is in the document if specified
        if (endHeader != null && !IsElementInDocument(transformedXhtml, endHeader))
        {
            throw new ArgumentException("End header is specified but not found in the provided document.", nameof(endHeader));
        }

        // Determine the endpoint for extraction
        XElement? actualEndHeader = endHeader ?? FindNextHeader(transformedXhtml, startHeader);

        if (actualEndHeader != null)
        {
            _logger.LogInformation(
                "Content extraction will stop at header: {HeaderText} (level: {Level})",
                actualEndHeader.Value.Trim(),
                GetHeaderLevel(actualEndHeader));
        }
        else
        {
            _logger.LogInformation("Content extraction will continue to end of document.");
        }

        // Extract elements between start and end
        var extractedElements = GetElementsBetween(startHeader, actualEndHeader);
        var elementCount = extractedElements.Count();

        _logger.LogInformation("Extracted {Count} elements", elementCount);

        if (elementCount == 1 && extractedElements.First() == startHeader)
        {
            _logger.LogWarning("No content found after header (header is last element or no content between headers)");
        }

        // Create XHTML document with extracted content
        var resultDocument = CreateXhtmlDocument(extractedElements);

        return resultDocument;
    }

    /// <inheritdoc />
    public XElement? FindNextHeader(
        XDocument transformedXhtml,
        XElement startHeader)
    {
        ArgumentNullException.ThrowIfNull(transformedXhtml);
        ArgumentNullException.ThrowIfNull(startHeader);

        var startLevel = GetHeaderLevel(startHeader);

        // Find all headers in the document
        var allHeaders = transformedXhtml.Descendants()
            .Where(IsHeader)
            .ToList();

        // Find the index of the start header
        var startIndex = allHeaders.IndexOf(startHeader);

        if (startIndex == -1)
        {
            _logger.LogWarning("Start header not found in document header list");
            return null;
        }

        // Find next header at same or higher level (lower number = higher level)
        for (int i = startIndex + 1; i < allHeaders.Count; i++)
        {
            var header = allHeaders[i];
            var headerLevel = GetHeaderLevel(header);

            if (headerLevel <= startLevel)
            {
                _logger.LogDebug(
                    "Found next header at level {Level}: {Text}",
                    headerLevel,
                    header.Value.Trim());
                return header;
            }
        }

        _logger.LogDebug("No next header found at same or higher level");
        return null;
    }

    /// <summary>
    /// Checks if an element is a header (h1-h6).
    /// </summary>
    private bool IsHeader(XElement element)
    {
        var localName = element.Name.LocalName.ToLowerInvariant();
        return new[] { "h1", "h2", "h3", "h4", "h5", "h6" }.Contains(localName);
    }

    /// <summary>
    /// Gets the numeric level of a header element (1-6).
    /// </summary>
    private int GetHeaderLevel(XElement header)
    {
        var localName = header.Name.LocalName.ToLowerInvariant();
        if (localName.Length == 2 && localName[0] == 'h' && char.IsDigit(localName[1]))
        {
            return int.Parse(localName.Substring(1));
        }

        throw new ArgumentException($"Element {localName} is not a valid header element.", nameof(header));
    }

    /// <summary>
    /// Checks if an element exists in the document.
    /// </summary>
    private bool IsElementInDocument(XDocument document, XElement element)
    {
        return document.Descendants().Contains(element);
    }

    /// <summary>
    /// Gets all elements between the start element (inclusive) and end element (exclusive).
    /// If end is null, returns all elements from start to the end of the parent container.
    /// </summary>
    private IEnumerable<XElement> GetElementsBetween(XElement start, XElement? end)
    {
        var elements = new List<XElement>();
        var collecting = false;
        var addedElements = new HashSet<XElement>(); // Track elements we've already added

        // Get the root body element to traverse
        var body = start.Ancestors()
            .FirstOrDefault(a => a.Name.LocalName.ToLowerInvariant() == "body");

        if (body == null)
        {
            _logger.LogWarning("No body element found, extracting from parent container");
            body = start.Parent ?? throw new InvalidOperationException("Start element has no parent");
        }

        // Traverse all descendants of body to find headers (they can be nested)
        foreach (var element in body.Descendants())
        {
            // Skip if this element was already added as part of a parent element
            if (addedElements.Any(added => added == element || added.Descendants().Contains(element)))
            {
                continue;
            }

            if (element == start)
            {
                collecting = true;
                var copy = new XElement(element);
                elements.Add(copy);
                // Mark this element and all its descendants as added
                addedElements.Add(element);
                foreach (var descendant in element.Descendants())
                {
                    addedElements.Add(descendant);
                }
                continue;
            }

            if (collecting)
            {
                // Stop if we've reached the end element
                if (end != null && element == end)
                {
                    break;
                }

                // Only collect top-level elements (direct children of body)
                // or elements that are siblings of the start element
                if (IsDirectDescendantOfBody(element, body) || IsSiblingOrDescendant(element, start))
                {
                    // Only use fallback stopping logic if NO explicit end boundary was specified
                    // When hierarchy provides explicit boundary, respect it instead of guessing
                    if (end == null && IsHeader(element) && element != start)
                    {
                        var startLevel = GetHeaderLevel(start);
                        var currentLevel = GetHeaderLevel(element);

                        if (currentLevel <= startLevel)
                        {
                            // This is the natural stopping point (fallback only)
                            break;
                        }
                    }

                    var copy = new XElement(element);
                    elements.Add(copy);
                    // Mark this element and all its descendants as added
                    addedElements.Add(element);
                    foreach (var descendant in element.Descendants())
                    {
                        addedElements.Add(descendant);
                    }
                }
            }
        }

        return elements;
    }

    /// <summary>
    /// Checks if an element is a direct descendant of the body element.
    /// </summary>
    private bool IsDirectDescendantOfBody(XElement element, XElement body)
    {
        return element.Parent == body;
    }

    /// <summary>
    /// Checks if an element is a sibling of the start element or a descendant of a sibling.
    /// </summary>
    private bool IsSiblingOrDescendant(XElement element, XElement start)
    {
        if (start.Parent == null)
            return false;

        // Check if element is a sibling
        if (element.Parent == start.Parent)
            return true;

        // Check if element is a descendant of a sibling
        var elementAncestors = element.Ancestors().ToList();
        var siblings = start.Parent.Elements().ToList();

        return elementAncestors.Any(ancestor => siblings.Contains(ancestor));
    }

    /// <summary>
    /// Creates a new XHTML document with proper structure containing the extracted content.
    /// </summary>
    private XDocument CreateXhtmlDocument(IEnumerable<XElement> contentElements)
    {
        var xhtml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(XhtmlNamespace + "html",
                new XAttribute("xmlns", XhtmlNamespace.NamespaceName),
                new XElement(XhtmlNamespace + "head",
                    new XElement(XhtmlNamespace + "meta",
                        new XAttribute("charset", "UTF-8")),
                    new XElement(XhtmlNamespace + "title", "Extracted Content")),
                new XElement(XhtmlNamespace + "body",
                    contentElements.Select(e => CloneElementWithNamespace(e, XhtmlNamespace)))));

        return xhtml;
    }

    /// <summary>
    /// Clones an element and ensures it uses the correct namespace.
    /// </summary>
    private XElement CloneElementWithNamespace(XElement element, XNamespace ns)
    {
        var newElement = new XElement(ns + element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration),
            element.Nodes().Select(node =>
            {
                if (node is XElement childElement)
                {
                    return CloneElementWithNamespace(childElement, ns);
                }
                return node;
            }));

        return newElement;
    }
}

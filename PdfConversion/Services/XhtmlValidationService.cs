using PdfConversion.Models;
using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for validating XHTML output against valid HTML elements
/// </summary>
public interface IXhtmlValidationService
{
    /// <summary>
    /// Validates XHTML content against valid HTML element names
    /// </summary>
    /// <param name="xhtmlContent">The XHTML content to validate</param>
    /// <returns>Validation result with any issues found</returns>
    Task<XhtmlValidationResult> ValidateXhtmlAsync(string xhtmlContent);
}

/// <summary>
/// Implementation of XHTML validation service
/// </summary>
public class XhtmlValidationService : IXhtmlValidationService
{
    private readonly ILogger<XhtmlValidationService> _logger;

    /// <summary>
    /// Complete list of valid HTML5 elements (lowercase)
    /// Based on MDN HTML elements reference
    /// </summary>
    private static readonly HashSet<string> ValidHtmlElements = new(StringComparer.Ordinal)
    {
        // Document metadata
        "html", "head", "title", "base", "link", "meta", "style",

        // Sectioning root
        "body",

        // Content sectioning
        "address", "article", "aside", "footer", "header",
        "h1", "h2", "h3", "h4", "h5", "h6", "main", "nav", "section",

        // Text content
        "blockquote", "dd", "div", "dl", "dt", "figcaption", "figure",
        "hr", "li", "ol", "p", "pre", "ul",

        // Inline text
        "a", "abbr", "b", "bdi", "bdo", "br", "cite", "code", "data",
        "dfn", "em", "i", "kbd", "mark", "q", "rp", "rt", "ruby", "s",
        "samp", "small", "span", "strong", "sub", "sup", "time", "u", "var", "wbr",

        // Image and multimedia
        "area", "audio", "img", "map", "track", "video",

        // Embedded content
        "embed", "iframe", "object", "param", "picture", "portal", "source",

        // SVG and MathML
        "svg", "math",

        // Scripting
        "canvas", "noscript", "script",

        // Demarcating edits
        "del", "ins",

        // Table content
        "caption", "col", "colgroup", "table", "tbody", "td", "tfoot", "th", "thead", "tr",

        // Forms
        "button", "datalist", "fieldset", "form", "input", "label", "legend",
        "meter", "optgroup", "option", "output", "progress", "select", "textarea",

        // Interactive elements
        "details", "dialog", "menu", "summary",

        // Web Components
        "slot", "template"
    };

    public XhtmlValidationService(ILogger<XhtmlValidationService> logger)
    {
        _logger = logger;
    }

    public async Task<XhtmlValidationResult> ValidateXhtmlAsync(string xhtmlContent)
    {
        try
        {
            return await Task.Run(() => ValidateXhtmlInternal(xhtmlContent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate XHTML content");

            // Return a validation result with error information
            return XhtmlValidationResult.WithIssues(new List<ValidationIssue>
            {
                new ValidationIssue
                {
                    Type = ValidationIssueType.InvalidElement,
                    ElementName = "ValidationError",
                    OccurrenceCount = 1,
                    XPaths = new List<string> { $"Error: {ex.Message}" }
                }
            });
        }
    }

    private XhtmlValidationResult ValidateXhtmlInternal(string xhtmlContent)
    {
        _logger.LogDebug("Starting XHTML validation");

        // Parse the XHTML content
        var doc = XDocument.Parse(xhtmlContent);

        // Dictionary to track issues: key = (issueType, elementName), value = (count, xpaths)
        var issueTracker = new Dictionary<(ValidationIssueType, string), (int count, List<string> xpaths)>();

        // Traverse all elements in the document
        var allElements = doc.Descendants().ToList();

        _logger.LogDebug("Validating {ElementCount} elements", allElements.Count);

        foreach (var element in allElements)
        {
            var localName = element.Name.LocalName;

            // Skip namespace nodes and focus on element names
            if (string.IsNullOrEmpty(localName))
                continue;

            // Check 1: Invalid element (not in valid HTML elements list)
            if (!ValidHtmlElements.Contains(localName))
            {
                TrackIssue(issueTracker, ValidationIssueType.InvalidElement, localName, element);
            }

            // Check 2: Uppercase in element name
            if (localName != localName.ToLowerInvariant())
            {
                TrackIssue(issueTracker, ValidationIssueType.UppercaseInElementName, localName, element);
            }
        }

        // Convert tracked issues to ValidationIssue objects
        var issues = issueTracker
            .Select(kvp => new ValidationIssue
            {
                Type = kvp.Key.Item1,
                ElementName = kvp.Key.Item2,
                OccurrenceCount = kvp.Value.count,
                XPaths = kvp.Value.xpaths
            })
            .OrderByDescending(i => i.OccurrenceCount)
            .ThenBy(i => i.ElementName)
            .ToList();

        if (issues.Any())
        {
            _logger.LogWarning(
                "XHTML validation found {IssueCount} distinct issues with {TotalOccurrences} total occurrences",
                issues.Count,
                issues.Sum(i => i.OccurrenceCount));

            return XhtmlValidationResult.WithIssues(issues);
        }

        _logger.LogInformation("XHTML validation successful - all elements are valid");
        return XhtmlValidationResult.Success();
    }

    /// <summary>
    /// Tracks a validation issue and stores XPath for first 5 occurrences
    /// </summary>
    private void TrackIssue(
        Dictionary<(ValidationIssueType, string), (int count, List<string> xpaths)> issueTracker,
        ValidationIssueType issueType,
        string elementName,
        XElement element)
    {
        var key = (issueType, elementName);

        if (!issueTracker.ContainsKey(key))
        {
            issueTracker[key] = (0, new List<string>());
        }

        var (count, xpaths) = issueTracker[key];
        count++;

        // Store XPath for first 5 occurrences only
        if (xpaths.Count < 5)
        {
            var xpath = GetXPath(element);
            xpaths.Add(xpath);
        }

        issueTracker[key] = (count, xpaths);
    }

    /// <summary>
    /// Generates XPath for an element (e.g., /html/body/div[1]/section[2]/Table[1])
    /// </summary>
    private string GetXPath(XElement element)
    {
        var parts = new List<string>();
        var current = element;

        while (current != null)
        {
            var name = current.Name.LocalName;

            // Calculate position among siblings with same name
            var position = current.ElementsBeforeSelf(current.Name).Count() + 1;

            parts.Insert(0, $"{name}[{position}]");
            current = current.Parent;
        }

        return "/" + string.Join("/", parts);
    }
}

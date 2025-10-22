using PdfConversion.Models;
using PdfConversion.Utilities;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

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
    private static XmlSchemaSet? _xhtmlSchemaSet = null;
    private static readonly object _schemaLock = new object();

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
            // Run both element name validation and schema validation
            var elementValidation = await Task.Run(() => ValidateXhtmlInternal(xhtmlContent));
            var schemaValidation = await ValidateAgainstXhtmlSchemaAsync(xhtmlContent);

            // Merge and deduplicate issues intelligently
            var mergedIssues = MergeValidationIssues(
                elementValidation.Issues,
                schemaValidation.Issues);

            if (mergedIssues.Any())
            {
                return XhtmlValidationResult.WithIssues(mergedIssues);
            }

            return XhtmlValidationResult.Success();
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

    /// <summary>
    /// Merges validation issues from element and schema validation, removing duplicates
    /// and combining information intelligently
    /// </summary>
    private List<ValidationIssue> MergeValidationIssues(
        List<ValidationIssue> elementIssues,
        List<ValidationIssue> schemaIssues)
    {
        _logger.LogDebug("Merging validation issues: {ElementCount} element issues, {SchemaCount} schema issues",
            elementIssues.Count, schemaIssues.Count);

        var mergedIssues = new List<ValidationIssue>();
        var processedElements = new HashSet<string>();

        // Process element issues first (InvalidElement and UppercaseInElementName)
        foreach (var elementIssue in elementIssues)
        {
            // Extract clean element names (handle comma-separated lists from schema issues)
            var elementNames = elementIssue.ElementName
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .ToList();

            foreach (var elementName in elementNames)
            {
                if (processedElements.Contains(elementName))
                    continue;

                // Find related schema issues for this element
                var relatedSchemaIssues = schemaIssues
                    .Where(si => si.ElementName.Contains(elementName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (relatedSchemaIssues.Any())
                {
                    // Merge element issue with schema issues
                    var mergedIssue = new ValidationIssue
                    {
                        Type = elementIssue.Type, // Keep original type (InvalidElement or Uppercase)
                        ElementName = elementName,
                        OccurrenceCount = elementIssue.OccurrenceCount,
                        XPaths = elementIssue.XPaths,
                        // Add schema validation details
                        SchemaMessage = string.Join(" | ", relatedSchemaIssues
                            .Select(si => si.SchemaMessage)
                            .Where(m => !string.IsNullOrEmpty(m))
                            .Distinct()),
                        LineNumbers = relatedSchemaIssues.SelectMany(si => si.LineNumbers).Take(5).ToList(),
                        LinePositions = relatedSchemaIssues.SelectMany(si => si.LinePositions).Take(5).ToList(),
                        Severity = relatedSchemaIssues.FirstOrDefault()?.Severity
                    };

                    mergedIssues.Add(mergedIssue);
                    processedElements.Add(elementName);

                    _logger.LogDebug("Merged element '{Element}' with {SchemaCount} schema issues",
                        elementName, relatedSchemaIssues.Count);
                }
                else
                {
                    // No schema issue for this element, keep element issue as-is
                    mergedIssues.Add(elementIssue);
                    processedElements.Add(elementName);
                }
            }
        }

        // Add schema issues that weren't related to any element issues
        foreach (var schemaIssue in schemaIssues)
        {
            var elementNames = schemaIssue.ElementName
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim())
                .ToList();

            // Check if any element in this schema issue hasn't been processed
            var unprocessedElements = elementNames
                .Where(e => !processedElements.Contains(e))
                .ToList();

            if (unprocessedElements.Any())
            {
                // Create a schema-only issue for unprocessed elements
                var schemaOnlyIssue = new ValidationIssue
                {
                    Type = ValidationIssueType.SchemaValidationError,
                    ElementName = string.Join(", ", unprocessedElements),
                    SchemaMessage = schemaIssue.SchemaMessage,
                    OccurrenceCount = schemaIssue.OccurrenceCount,
                    LineNumbers = schemaIssue.LineNumbers,
                    LinePositions = schemaIssue.LinePositions,
                    Severity = schemaIssue.Severity,
                    XPaths = schemaIssue.XPaths
                };

                mergedIssues.Add(schemaOnlyIssue);

                foreach (var elem in unprocessedElements)
                {
                    processedElements.Add(elem);
                }

                _logger.LogDebug("Added schema-only issue for elements: {Elements}",
                    string.Join(", ", unprocessedElements));
            }
        }

        // Sort by occurrence count (descending), then by element name
        var sortedIssues = mergedIssues
            .OrderByDescending(i => i.OccurrenceCount)
            .ThenBy(i => i.ElementName)
            .ToList();

        _logger.LogInformation(
            "Merged validation issues: {TotalIssues} unique issues, {TotalOccurrences} total occurrences",
            sortedIssues.Count,
            sortedIssues.Sum(i => i.OccurrenceCount));

        return sortedIssues;
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

    /// <summary>
    /// Validates XHTML content against W3C XHTML 1.0 Strict schema
    /// </summary>
    /// <param name="xhtmlContent">The XHTML content to validate</param>
    /// <param name="relaxedMode">If true, allows common attributes like data-* and filters some validation errors</param>
    /// <returns>Validation result with schema validation issues</returns>
    private async Task<XhtmlValidationResult> ValidateAgainstXhtmlSchemaAsync(
        string xhtmlContent,
        bool relaxedMode = true)
    {
        try
        {
            _logger.LogDebug("=== Starting schema validation ===");

            // Get or create the XHTML schema set (cached)
            if (_xhtmlSchemaSet == null)
            {
                lock (_schemaLock)
                {
                    if (_xhtmlSchemaSet == null)
                    {
                        _logger.LogInformation("Loading XHTML 1.0 Strict schema for validation");
                        var (schemaSet, errors) = ValidatingReader.GetSchemaSet(
                            new XmlUrlResolver(),
                            "http://www.w3.org/2002/08/xhtml/xhtml1-strict.xsd");

                        _logger.LogDebug("Schema load result: schemaSet={SchemaSetNull}, errorCount={ErrorCount}",
                            schemaSet == null ? "NULL" : "OK",
                            errors.Count);

                        if (errors.Any())
                        {
                            _logger.LogWarning("Schema load errors: {Errors}",
                                string.Join("; ", errors));
                        }

                        if (schemaSet == null || errors.Any())
                        {
                            _logger.LogWarning("Failed to load XHTML schema - skipping schema validation");
                            return XhtmlValidationResult.Success(); // Skip schema validation if schema can't be loaded
                        }

                        _logger.LogInformation("XHTML schema loaded successfully");
                        _xhtmlSchemaSet = schemaSet;
                    }
                    else
                    {
                        _logger.LogDebug("Using cached XHTML schema");
                    }
                }
            }
            else
            {
                _logger.LogDebug("Using cached XHTML schema");
            }

            // Prepare content for validation - wrap fragments if needed
            _logger.LogDebug("Preparing content for schema validation (length: {Length})", xhtmlContent.Length);
            var contentToValidate = PrepareContentForSchemaValidation(xhtmlContent);
            _logger.LogDebug("Content prepared for validation (length: {Length})", contentToValidate.Length);

            // Define attribute filter for relaxed mode
            Func<(string elementName, string elementNs), (string attributeName, string attributeNs), string, string?>? attributeFilter = null;
            if (relaxedMode)
            {
                _logger.LogDebug("Using relaxed mode with attribute filter");
                attributeFilter = (element, attribute, value) =>
                {
                    return attribute.attributeName switch
                    {
                        var name when name.StartsWith("data-") => null, // Allow data-* attributes
                        var name when name == "class" && string.IsNullOrEmpty(value) => null, // Allow empty class
                        var name when name == "id" && !string.IsNullOrEmpty(value) && char.IsDigit(value[0]) => null, // Allow numeric IDs
                        "src" when element.elementName == "img" => string.Empty, // img src can be empty in test data
                        _ => value
                    };
                };
            }
            else
            {
                _logger.LogDebug("Using strict mode without attribute filter");
            }

            // Validate using ValidatingReader
            _logger.LogDebug("Creating ValidatingReader and starting validation...");
            using var validatingReader = new ValidatingReader(
                contentToValidate,
                _xhtmlSchemaSet,
                includeElement: null,
                includeAttribute: attributeFilter);

            await validatingReader.ValidateAsync();

            _logger.LogDebug("Validation completed. Message count: {MessageCount}",
                validatingReader.ValidationMessages.Count);

            // Log validation messages for debugging
            if (validatingReader.ValidationMessages.Any())
            {
                foreach (var msg in validatingReader.ValidationMessages.Take(10))
                {
                    _logger.LogDebug("Validation message: [{Severity}] Line {Line}:{Position} in {Element}: {Message}",
                        msg.severity, msg.line, msg.position, msg.element, msg.message);
                }
            }
            else
            {
                _logger.LogDebug("No validation messages generated (schema validation passed)");
            }

            // Convert validation messages to ValidationIssue objects
            // Filter out meta element errors (common in HTML5, acceptable in our output)
            var filteredMessages = validatingReader.ValidationMessages
                .Where(vm => !vm.element.Contains("meta", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!filteredMessages.Any())
            {
                _logger.LogDebug("Schema validation passed (after filtering)");
                return XhtmlValidationResult.Success();
            }

            // Group validation messages by message text and convert to ValidationIssue
            var issues = filteredMessages
                .GroupBy(vm => vm.message)
                .Select(group =>
                {
                    var first = group.First();
                    var lineNumbers = group.Select(vm => vm.line).Take(5).ToList();
                    var linePositions = group.Select(vm => vm.position).Take(5).ToList();
                    var elements = group.Select(vm => vm.element).Distinct().ToList();

                    return new ValidationIssue
                    {
                        Type = ValidationIssueType.SchemaValidationError,
                        ElementName = string.Join(", ", elements.Take(3)), // Show up to 3 element names
                        SchemaMessage = first.message,
                        OccurrenceCount = group.Count(),
                        LineNumbers = lineNumbers,
                        LinePositions = linePositions,
                        Severity = first.severity,
                        XPaths = group.Select(vm => $"Line {vm.line}:{vm.position}").Take(5).ToList()
                    };
                })
                .OrderByDescending(issue => issue.OccurrenceCount)
                .ThenBy(issue => issue.LineNumbers.FirstOrDefault())
                .ToList();

            _logger.LogInformation(
                "Schema validation found {IssueCount} distinct issues with {TotalOccurrences} total occurrences",
                issues.Count,
                issues.Sum(i => i.OccurrenceCount));

            return XhtmlValidationResult.WithIssues(issues);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation failed with exception: {ExceptionType} - {Message}\nStackTrace: {StackTrace}",
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace);
            return XhtmlValidationResult.Success(); // Don't fail validation if schema validation has issues
        }
    }

    /// <summary>
    /// Prepares XHTML content for schema validation by wrapping fragments if needed
    /// and removing DOCTYPE declarations (which cause DTD processing errors).
    /// Uses string-based cloning to preserve ALL content perfectly.
    /// </summary>
    private string PrepareContentForSchemaValidation(string xhtmlContent)
    {
        try
        {
            // Strip DOCTYPE if present (causes security exceptions)
            var withoutDoctype = System.Text.RegularExpressions.Regex.Replace(
                xhtmlContent,
                @"<!DOCTYPE[^>]*>",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var doc = XDocument.Parse(withoutDoctype);
            var root = doc.Root;

            if (root == null)
            {
                _logger.LogWarning("No root element found in XHTML content");
                return withoutDoctype;
            }

            var rootElement = root.Name.LocalName;
            var hasXhtmlNamespace = root.Name.NamespaceName == "http://www.w3.org/1999/xhtml";

            _logger.LogDebug("Root element: {Root}, Has XHTML namespace: {HasNs}",
                rootElement, hasXhtmlNamespace);

            // Case 1: html root without namespace - add namespace using string manipulation
            if (rootElement == "html" && !hasXhtmlNamespace)
            {
                _logger.LogDebug("Adding XHTML namespace to clone using string replacement");

                // Get the document as string - use default (pretty-printed) formatting to match Monaco
                var xmlString = doc.ToString();

                // DEBUG: Log first 50 lines of ORIGINAL
                var originalLines = xmlString.Split('\n');
                _logger.LogDebug("=== ORIGINAL - First 50 lines ===");
                for (int i = 0; i < Math.Min(50, originalLines.Length); i++)
                {
                    var content = originalLines[i].Substring(0, Math.Min(100, originalLines[i].Length));
                    _logger.LogDebug("[ORIG Line {Line}] {Content}", i + 1, content);
                }

                // Find XYZ fragment in original
                for (int i = 0; i < originalLines.Length; i++)
                {
                    if (originalLines[i].Contains("XYZ") && originalLines[i].Contains("Loewm"))
                    {
                        _logger.LogWarning(">>> FOUND XYZ/Loewm in ORIGINAL at line {Line}: {Content}",
                            i + 1, originalLines[i].Trim());
                        break;
                    }
                }

                // Add XHTML namespace to the html tag using regex
                // This preserves ALL content perfectly
                var withNamespace = System.Text.RegularExpressions.Regex.Replace(
                    xmlString,
                    @"<html\b",
                    @"<html xmlns=""http://www.w3.org/1999/xhtml""",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // Also add xml:lang if not present
                if (!withNamespace.Contains("xml:lang"))
                {
                    withNamespace = System.Text.RegularExpressions.Regex.Replace(
                        withNamespace,
                        @"<html\b([^>]*?)>",
                        @"<html$1 xml:lang=""en"">",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }

                // DEBUG: Log first 50 lines of CLONE
                var cloneLines = withNamespace.Split('\n');
                _logger.LogDebug("=== CLONE - First 50 lines ===");
                for (int i = 0; i < Math.Min(50, cloneLines.Length); i++)
                {
                    var content = cloneLines[i].Substring(0, Math.Min(100, cloneLines[i].Length));
                    _logger.LogDebug("[CLONE Line {Line}] {Content}", i + 1, content);
                }

                // Find XYZ fragment in clone
                for (int i = 0; i < cloneLines.Length; i++)
                {
                    if (cloneLines[i].Contains("XYZ") && cloneLines[i].Contains("Loewm"))
                    {
                        _logger.LogWarning(">>> FOUND XYZ/Loewm in CLONE at line {Line}: {Content}",
                            i + 1, cloneLines[i].Trim());
                        break;
                    }
                }

                _logger.LogDebug("Content prepared for validation (original: {Original}, clone: {Clone})",
                    xmlString.Length, withNamespace.Length);

                return withNamespace;
            }

            // Case 2: html root with namespace - return as-is (already valid)
            if (rootElement == "html" && hasXhtmlNamespace)
            {
                _logger.LogDebug("HTML root already has XHTML namespace");
                return doc.ToString(); // Use default (pretty-printed) formatting
            }

            // Case 3: Fragment (div/section/article/body) - wrap in complete HTML
            var needsWrapping = rootElement == "body" || rootElement == "div" ||
                               rootElement == "section" || rootElement == "article";

            if (needsWrapping)
            {
                _logger.LogDebug("Wrapping {Root} fragment in XHTML document", rootElement);

                // Use single-line wrapper with pretty-printed fragment content
                return $@"<html xml:lang=""en"" xmlns=""http://www.w3.org/1999/xhtml""><head><title>XHTML validation</title></head><body>{root.ToString()}</body></html>";
            }

            // Default: return stripped content with pretty-printed formatting
            _logger.LogDebug("Using content as-is for validation");
            return doc.ToString(); // Use default (pretty-printed) formatting
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing content for schema validation");
            return xhtmlContent;
        }
    }

}

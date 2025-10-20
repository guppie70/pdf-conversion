using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Information about a section that used template content
/// </summary>
public class TemplateSectionInfo
{
    public string DataRef { get; set; } = string.Empty;
    public string LinkName { get; set; } = string.Empty;
}

/// <summary>
/// Result of a document reconstruction operation
/// </summary>
public class DocumentReconstructionResult
{
    public string ReconstructedXml { get; set; } = string.Empty;
    public int TemplatesUsed { get; set; }
    public List<TemplateSectionInfo> TemplateUsedForSections { get; set; } = new();
    public int SectionsLoaded { get; set; }
    public int SectionsMissing { get; set; }
}

/// <summary>
/// Service for reconstructing the original Normalized XML from split section files and hierarchy
/// </summary>
public interface IDocumentReconstructionService
{
    /// <summary>
    /// Reconstructs the Normalized XML by reassembling section files in hierarchy order
    /// </summary>
    /// <param name="hierarchyXmlPath">Path to the hierarchy XML file</param>
    /// <param name="sectionsDirectory">Directory containing section XML files</param>
    /// <returns>The reconstructed XML as a string</returns>
    [Obsolete("Use ReconstructNormalizedXmlWithDetailsAsync instead")]
    Task<string> ReconstructNormalizedXmlAsync(string hierarchyXmlPath, string sectionsDirectory);

    /// <summary>
    /// Reconstructs the Normalized XML by reassembling section files in hierarchy order
    /// </summary>
    /// <param name="hierarchyXmlPath">Path to the hierarchy XML file</param>
    /// <param name="sectionsDirectory">Directory containing section XML files</param>
    /// <returns>Detailed reconstruction result with template usage information</returns>
    Task<DocumentReconstructionResult> ReconstructNormalizedXmlWithDetailsAsync(string hierarchyXmlPath, string sectionsDirectory);
}

/// <summary>
/// Implementation of document reconstruction service
/// </summary>
public class DocumentReconstructionService : IDocumentReconstructionService
{
    private readonly ILogger<DocumentReconstructionService> _logger;
    private readonly IOptions<ConversionSettings> _conversionSettings;

    public DocumentReconstructionService(
        ILogger<DocumentReconstructionService> logger,
        IOptions<ConversionSettings> conversionSettings)
    {
        _logger = logger;
        _conversionSettings = conversionSettings;
    }

    public async Task<string> ReconstructNormalizedXmlAsync(string hierarchyXmlPath, string sectionsDirectory)
    {
        var result = await ReconstructNormalizedXmlWithDetailsAsync(hierarchyXmlPath, sectionsDirectory);
        return result.ReconstructedXml;
    }

    public async Task<DocumentReconstructionResult> ReconstructNormalizedXmlWithDetailsAsync(string hierarchyXmlPath, string sectionsDirectory)
    {
        var result = new DocumentReconstructionResult();

        try
        {
            _logger.LogInformation("Starting document reconstruction from {HierarchyPath}", hierarchyXmlPath);

            // 1. Load and parse hierarchy XML
            if (!File.Exists(hierarchyXmlPath))
            {
                throw new FileNotFoundException($"Hierarchy file not found: {hierarchyXmlPath}");
            }

            var hierarchyContent = await File.ReadAllTextAsync(hierarchyXmlPath);
            var hierarchyDoc = XDocument.Parse(hierarchyContent);

            // 2. Extract hierarchy items in order (skip report-root)
            var hierarchyItems = ExtractHierarchyItems(hierarchyDoc);
            _logger.LogInformation("Found {Count} hierarchy items", hierarchyItems.Count);

            // 3. Load template file for missing sections
            string? templateContent = null;
            var templatePath = "/app/data/input/template.xml";
            if (File.Exists(templatePath))
            {
                templateContent = await File.ReadAllTextAsync(templatePath);
                _logger.LogInformation("Template file loaded for missing sections");
            }

            // 4. Create the root XHTML document structure
            XNamespace xhtmlNs = "http://www.w3.org/1999/xhtml";
            var reconstructedDoc = new XDocument(
                new XDocumentType("html", null, null, null),
                new XElement(xhtmlNs + "html",
                    new XAttribute("lang", "en"),
                    new XElement(xhtmlNs + "head",
                        new XElement(xhtmlNs + "meta", new XAttribute("charset", "UTF-8")),
                        new XElement(xhtmlNs + "title", "Reconstructed Document")
                    ),
                    new XElement(xhtmlNs + "body",
                        new XElement(xhtmlNs + "div",
                            new XAttribute("class", "document-content")
                        )
                    )
                )
            );

            var bodyElement = reconstructedDoc.Root?.Element(xhtmlNs + "body");
            if (bodyElement == null)
            {
                throw new InvalidOperationException("Failed to create body element");
            }

            var documentContentDiv = bodyElement.Element(xhtmlNs + "div");
            if (documentContentDiv == null)
            {
                throw new InvalidOperationException("Failed to create document-content div");
            }

            // Get special files from configuration that should be excluded from template usage tracking
            var specialFiles = new HashSet<string>(_conversionSettings.Value.SpecialSectionFiles);

            // 5. Load each section file and append content to body
            foreach (var item in hierarchyItems)
            {
                // Skip report-root as per user instructions
                if (item.Id == "report-root")
                {
                    _logger.LogDebug("Skipping report-root element");
                    continue;
                }

                var sectionPath = Path.Combine(sectionsDirectory, item.DataRef);
                string sectionContent;

                if (File.Exists(sectionPath))
                {
                    // Load actual section file
                    sectionContent = await File.ReadAllTextAsync(sectionPath);
                    result.SectionsLoaded++;
                }
                else if (!string.IsNullOrEmpty(templateContent))
                {
                    // Parse template and inject the linkName into the header
                    var templateDoc = XDocument.Parse(templateContent);
                    var templateNs = templateDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                    // Find the h1 element in the template
                    // Structure: Root (data) → content → article → div → section → h1
                    var contentElement = templateDoc.Root?.Element(templateNs + "content");
                    var articleElement = contentElement?.Element(templateNs + "article");
                    var divElement = articleElement?.Element(templateNs + "div");
                    var sectionElement = divElement?.Element(templateNs + "section");
                    var h1Element = sectionElement?.Element(templateNs + "h1");

                    // Defensive logging
                    if (contentElement == null)
                        _logger.LogWarning("Template parsing: content element not found");
                    else if (articleElement == null)
                        _logger.LogWarning("Template parsing: article element not found");
                    else if (divElement == null)
                        _logger.LogWarning("Template parsing: div element not found (expected <div class='pageblock'>)");
                    else if (sectionElement == null)
                        _logger.LogWarning("Template parsing: section element not found");
                    else if (h1Element == null)
                        _logger.LogWarning("Template parsing: h1 element not found");

                    if (h1Element != null && !string.IsNullOrEmpty(item.LinkName))
                    {
                        // Update the header to include the specific section name
                        h1Element.Value = $"TEMPLATE PLACEHOLDER - {item.LinkName}";
                        _logger.LogDebug("Injected linkName '{LinkName}' into template for {DataRef}", item.LinkName, item.DataRef);
                    }
                    else if (h1Element == null)
                    {
                        _logger.LogWarning("Could not inject linkName '{LinkName}' for {DataRef} - h1 element not found in template",
                            item.LinkName, item.DataRef);
                    }

                    sectionContent = templateDoc.ToString();

                    // Check if this is a special file that should be excluded from reporting
                    if (specialFiles.Contains(item.DataRef))
                    {
                        _logger.LogDebug("Skipping template usage tracking for special file: {DataRef}", item.DataRef);
                        // Use template but don't track it
                    }
                    else
                    {
                        // Use template for missing section and track it
                        _logger.LogWarning("Section file not found: {DataRef}, using template for {LinkName}",
                            item.DataRef, item.LinkName);

                        result.TemplatesUsed++;
                        result.TemplateUsedForSections.Add(new TemplateSectionInfo
                        {
                            DataRef = item.DataRef,
                            LinkName = item.LinkName
                        });
                    }
                }
                else
                {
                    // No template available, skip
                    _logger.LogWarning("Section file not found and no template available: {DataRef}", item.DataRef);
                    result.SectionsMissing++;
                    continue;
                }

                try
                {
                    // Parse section XML
                    var sectionDoc = XDocument.Parse(sectionContent);

                    // Extract content from <section> element
                    var ns = sectionDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                    var contentElement = sectionDoc.Root?.Element(ns + "content");
                    var articleElement = contentElement?.Element(ns + "article");
                    var sectionElement = articleElement?.Descendants(ns + "section").FirstOrDefault();

                    if (sectionElement == null)
                    {
                        _logger.LogWarning("No section content found in {DataRef}", item.DataRef);
                        continue;
                    }

                    // Extract all child elements from section and add to document-content div
                    foreach (var element in sectionElement.Elements())
                    {
                        // Add namespace to elements and shift headers based on hierarchy depth
                        documentContentDiv.Add(AddXhtmlNamespace(element, xhtmlNs, item.Depth));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading section {DataRef}", item.DataRef);
                    result.SectionsMissing++;
                }
            }

            _logger.LogInformation("Reconstruction complete: {Loaded} sections loaded, {Templates} templates used, {Missing} missing",
                result.SectionsLoaded, result.TemplatesUsed, result.SectionsMissing);

            // 6. Pretty print the reconstructed XML for consistent formatting
            var prettyPrintedXml = PrettyPrintXml(reconstructedDoc);
            result.ReconstructedXml = prettyPrintedXml;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document reconstruction");
            throw;
        }
    }

    /// <summary>
    /// Extracts hierarchy items from the hierarchy XML document in order
    /// Starting from /items/structured/item/sub_items as per user instructions
    /// </summary>
    private List<HierarchyItemData> ExtractHierarchyItems(XDocument hierarchyDoc)
    {
        var items = new List<HierarchyItemData>();

        var defaultNs = hierarchyDoc.Root?.GetDefaultNamespace();
        XNamespace ns = defaultNs ?? XNamespace.None;

        // Navigate to /items/structured/item/sub_items
        var structuredElement = hierarchyDoc.Root?.Element(ns + "structured");
        if (structuredElement == null)
        {
            throw new InvalidOperationException("Hierarchy XML missing <structured> element");
        }

        // Find the first item (report-root) and then get its sub_items
        var reportRootItem = structuredElement.Element(ns + "item");
        if (reportRootItem != null)
        {
            var subItemsElement = reportRootItem.Element(ns + "sub_items");
            if (subItemsElement != null)
            {
                // Start extracting from sub_items (skipping report-root itself)
                // Start at depth 1 for items in /items/structured/item/sub_items
                ExtractHierarchyItemsRecursive(subItemsElement, ns, items, depth: 1);
            }
        }
        else
        {
            // Fallback to original behavior if structure is different
            _logger.LogWarning("Could not find expected hierarchy structure, using fallback extraction");
            ExtractHierarchyItemsRecursive(structuredElement, ns, items, depth: 1);
        }

        return items;
    }

    /// <summary>
    /// Recursively extracts hierarchy items maintaining document order
    /// </summary>
    /// <param name="parent">Parent element containing items</param>
    /// <param name="ns">XML namespace</param>
    /// <param name="items">List to accumulate items</param>
    /// <param name="depth">Current hierarchy depth (1-based)</param>
    private void ExtractHierarchyItemsRecursive(XElement parent, XNamespace ns, List<HierarchyItemData> items, int depth = 1)
    {
        foreach (var itemElement in parent.Elements(ns + "item"))
        {
            var dataRef = itemElement.Attribute("data-ref")?.Value;
            var id = itemElement.Attribute("id")?.Value;
            var linkName = itemElement.Element(ns + "web_page")?.Element(ns + "linkname")?.Value ?? string.Empty;

            if (!string.IsNullOrEmpty(dataRef) && !string.IsNullOrEmpty(id))
            {
                items.Add(new HierarchyItemData
                {
                    Id = id,
                    DataRef = dataRef,
                    Depth = depth,
                    LinkName = linkName
                });
            }

            // Process sub_items with incremented depth
            var subItemsElement = itemElement.Element(ns + "sub_items");
            if (subItemsElement != null)
            {
                ExtractHierarchyItemsRecursive(subItemsElement, ns, items, depth + 1);
            }
        }
    }

    /// <summary>
    /// Recursively adds XHTML namespace to an element and all its descendants
    /// </summary>
    /// <param name="element">Source element to transform</param>
    /// <param name="xhtmlNs">XHTML namespace to apply</param>
    /// <param name="hierarchyDepth">Hierarchy depth for header level shifting (1-based)</param>
    /// <returns>New element with XHTML namespace and shifted header levels</returns>
    private XElement AddXhtmlNamespace(XElement element, XNamespace xhtmlNs, int hierarchyDepth = 1)
    {
        var elementName = element.Name.LocalName;

        // Shift header levels based on hierarchy depth
        // Formula: targetLevel = currentHeaderLevel + (hierarchyDepth - 1)
        if (IsHeaderElement(elementName, out int currentLevel))
        {
            int shift = hierarchyDepth - 1;
            int targetLevel = currentLevel + shift;

            // Cap at h6 if target exceeds HTML limits
            if (targetLevel > 6)
            {
                elementName = "h6";
                var shiftedElement = new XElement(
                    xhtmlNs + elementName,
                    element.Attributes().Where(a => !a.IsNamespaceDeclaration),
                    new XAttribute("data-targetheader", $"h{targetLevel}"),
                    element.Nodes().Select(n =>
                    {
                        if (n is XElement e)
                            return AddXhtmlNamespace(e, xhtmlNs, hierarchyDepth);
                        return n;
                    })
                );
                return shiftedElement;
            }
            else
            {
                elementName = $"h{targetLevel}";
            }
        }

        var newElement = new XElement(
            xhtmlNs + elementName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration),
            element.Nodes().Select(n =>
            {
                if (n is XElement e)
                    return AddXhtmlNamespace(e, xhtmlNs, hierarchyDepth);
                return n;
            })
        );

        return newElement;
    }

    /// <summary>
    /// Checks if an element name is a header (h1-h6) and returns its level
    /// </summary>
    /// <param name="elementName">Element name to check</param>
    /// <param name="level">Output parameter for header level (1-6)</param>
    /// <returns>True if element is a header</returns>
    private bool IsHeaderElement(string elementName, out int level)
    {
        level = 0;
        if (elementName.Length == 2 && elementName[0] == 'h' && char.IsDigit(elementName[1]))
        {
            level = elementName[1] - '0';
            return level >= 1 && level <= 6;
        }
        return false;
    }

    /// <summary>
    /// Pretty prints an XML document with consistent formatting
    /// </summary>
    private string PrettyPrintXml(XDocument doc)
    {
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

    /// <summary>
    /// Simple data class to hold hierarchy item information
    /// </summary>
    private class HierarchyItemData
    {
        public string Id { get; set; } = string.Empty;
        public string DataRef { get; set; } = string.Empty;
        public int Depth { get; set; } = 1;
        public string LinkName { get; set; } = string.Empty;
    }
}

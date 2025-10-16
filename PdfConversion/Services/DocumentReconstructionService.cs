using System.Xml.Linq;

namespace PdfConversion.Services;

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
    Task<string> ReconstructNormalizedXmlAsync(string hierarchyXmlPath, string sectionsDirectory);
}

/// <summary>
/// Implementation of document reconstruction service
/// </summary>
public class DocumentReconstructionService : IDocumentReconstructionService
{
    private readonly ILogger<DocumentReconstructionService> _logger;

    public DocumentReconstructionService(ILogger<DocumentReconstructionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ReconstructNormalizedXmlAsync(string hierarchyXmlPath, string sectionsDirectory)
    {
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

            // 5. Load each section file and append content to body
            int sectionsLoaded = 0;
            int sectionsMissing = 0;
            int templatesUsed = 0;

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
                    sectionsLoaded++;
                }
                else if (!string.IsNullOrEmpty(templateContent))
                {
                    // Use template for missing section
                    _logger.LogWarning("Section file not found: {DataRef}, using template", item.DataRef);
                    sectionContent = templateContent;
                    templatesUsed++;
                }
                else
                {
                    // No template available, skip
                    _logger.LogWarning("Section file not found and no template available: {DataRef}", item.DataRef);
                    sectionsMissing++;
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
                    sectionsMissing++;
                }
            }

            _logger.LogInformation("Reconstruction complete: {Loaded} sections loaded, {Templates} templates used, {Missing} missing",
                sectionsLoaded, templatesUsed, sectionsMissing);

            // 6. Pretty print the reconstructed XML for consistent formatting
            var prettyPrintedXml = PrettyPrintXml(reconstructedDoc);
            return prettyPrintedXml;
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

            if (!string.IsNullOrEmpty(dataRef) && !string.IsNullOrEmpty(id))
            {
                items.Add(new HierarchyItemData
                {
                    Id = id,
                    DataRef = dataRef,
                    Depth = depth
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
    }
}

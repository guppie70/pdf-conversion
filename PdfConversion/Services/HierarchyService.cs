using System.Xml.Linq;
using Microsoft.Extensions.Options;
using PdfConversion.Models;

namespace PdfConversion.Services;

public interface IHierarchyService
{
    Task<HierarchyStructure> LoadHierarchyAsync(string filePath);
    Task SaveHierarchyAsync(string filePath, HierarchyStructure hierarchy);
    HierarchyItem? FindItemById(HierarchyStructure hierarchy, string id);
    List<HierarchyItem> GetAllItems(HierarchyStructure hierarchy);
}

public class HierarchyService : IHierarchyService
{
    private readonly ILogger<HierarchyService> _logger;
    private readonly IOptions<ConversionSettings> _conversionSettings;

    public HierarchyService(
        ILogger<HierarchyService> logger,
        IOptions<ConversionSettings> conversionSettings)
    {
        _logger = logger;
        _conversionSettings = conversionSettings;
    }

    public async Task<HierarchyStructure> LoadHierarchyAsync(string filePath)
    {
        try
        {
            _logger.LogInformation("Loading hierarchy from {FilePath}", filePath);

            var xmlContent = await File.ReadAllTextAsync(filePath);
            var doc = XDocument.Parse(xmlContent);

            var structure = new HierarchyStructure();

            // Find the root item (level 0)
            var rootElement = doc.Descendants("item")
                .FirstOrDefault(e => e.Attribute("level")?.Value == "0");

            if (rootElement != null)
            {
                structure.Root = ParseItem(rootElement);
            }

            // Calculate uncertainties (items with confidence < 70%)
            structure.Uncertainties = GetAllItems(structure)
                .Where(item => item.Confidence.HasValue && item.Confidence.Value < 70)
                .ToList();

            // Calculate overall confidence (average of all items with confidence scores)
            var itemsWithConfidence = GetAllItems(structure)
                .Where(item => item.Confidence.HasValue)
                .ToList();

            if (itemsWithConfidence.Any())
            {
                structure.OverallConfidence = (int)itemsWithConfidence.Average(item => item.Confidence!.Value);
            }

            _logger.LogInformation("Loaded hierarchy with {Count} items, overall confidence: {Confidence}%",
                GetAllItems(structure).Count, structure.OverallConfidence);

            return structure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hierarchy from {FilePath}", filePath);
            throw;
        }
    }

    private HierarchyItem ParseItem(XElement element)
    {
        var item = new HierarchyItem
        {
            Id = element.Attribute("id")?.Value ?? string.Empty,
            Level = int.Parse(element.Attribute("level")?.Value ?? "0"),
            DataRef = element.Attribute("data-ref")?.Value ?? string.Empty,
            IsExpanded = true  // Default to expanded when loading from XML
        };

        // Load optional header type (for badge display in Manual Mode)
        item.HeaderType = element.Attribute("header-type")?.Value;

        // Load optional TOC attributes
        item.TocStart = element.Attribute("data-tocstart")?.Value == "true";
        item.TocEnd = element.Attribute("data-tocend")?.Value == "true";
        item.TocNumber = element.Attribute("data-tocnumber")?.Value;
        item.TocStyle = element.Attribute("data-tocstyle")?.Value;
        item.TocHide = element.Attribute("data-tochide")?.Value == "true";

        // Parse web_page element
        var webPage = element.Element("web_page");
        if (webPage != null)
        {
            item.LinkName = webPage.Element("linkname")?.Value ?? string.Empty;
            item.Path = webPage.Element("path")?.Value ?? "/";
        }

        // Parse optional confidence attributes
        var confidenceAttr = element.Attribute("confidence");
        if (confidenceAttr != null && int.TryParse(confidenceAttr.Value, out int confidence))
        {
            item.Confidence = confidence;
        }

        var uncertainAttr = element.Attribute("is-uncertain");
        if (uncertainAttr != null && bool.TryParse(uncertainAttr.Value, out bool isUncertain))
        {
            item.IsUncertain = isUncertain;
        }

        item.Reasoning = element.Attribute("reasoning")?.Value;

        // Parse sub_items recursively
        var subItemsElement = element.Element("sub_items");
        if (subItemsElement != null)
        {
            item.SubItems = subItemsElement.Elements("item")
                .Select(ParseItem)
                .ToList();
        }

        return item;
    }

    public async Task SaveHierarchyAsync(string filePath, HierarchyStructure hierarchy)
    {
        try
        {
            _logger.LogInformation("Saving hierarchy to {FilePath}", filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created directory: {Directory}", directory);
            }

            // Normalize the root element to ensure consistent format
            NormalizeRootElement(hierarchy.Root);

            // Apply ID postfixes if enabled
            ApplyIdPostfixes(hierarchy.Root);

            var doc = new XDocument(
                new XElement("items",
                    new XElement("structured",
                        BuildItemElement(hierarchy.Root)
                    )
                )
            );

            var xmlContent = doc.ToString();
            await File.WriteAllTextAsync(filePath, xmlContent);

            _logger.LogInformation("Successfully saved hierarchy to {FilePath}", filePath);

            // Also write to _work directory for development context
            try
            {
                var workPath = "/app/data/_work";
                if (!Directory.Exists(workPath))
                {
                    Directory.CreateDirectory(workPath);
                }
                var contextHierarchyPath = Path.Combine(workPath, "_hierarchy.xml");
                await File.WriteAllTextAsync(contextHierarchyPath, xmlContent);
                _logger.LogInformation("Saved context hierarchy XML to {Path}", contextHierarchyPath);
            }
            catch (Exception workEx)
            {
                _logger.LogWarning(workEx, "Failed to save context hierarchy XML to _work folder");
                // Non-critical, continue
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save hierarchy to {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Normalizes the root element to ensure consistent format across all hierarchies.
    /// Only modifies the root element (level 0), preserving all child items as-is.
    /// </summary>
    private void NormalizeRootElement(HierarchyItem rootItem)
    {
        if (rootItem == null) return;

        // Only normalize if this is the root (level 0)
        if (rootItem.Level == 0)
        {
            // Log if we're changing values
            if (rootItem.Id != "report-root" || rootItem.DataRef != "report-root.xml")
            {
                _logger.LogInformation(
                    "Normalizing root element: id '{OldId}' → 'report-root', data-ref '{OldDataRef}' → 'report-root.xml'",
                    rootItem.Id, rootItem.DataRef);
            }

            // Force standardized values for root element
            rootItem.Id = "report-root";
            rootItem.DataRef = "report-root.xml";

            // Ensure linkname is "Annual Report 2024" if not already set or different
            if (string.IsNullOrWhiteSpace(rootItem.LinkName) ||
                !rootItem.LinkName.Contains("Annual Report", StringComparison.OrdinalIgnoreCase))
            {
                rootItem.LinkName = "Annual Report 2024";
            }

            // Ensure path is root
            rootItem.Path = "/";
        }
        // Note: Child items (SubItems) are NOT modified - they remain editable
    }

    /// <summary>
    /// Applies ID postfixes to all items in the hierarchy if postfix is enabled.
    /// Regenerates IDs from LinkNames and ensures uniqueness.
    /// </summary>
    private void ApplyIdPostfixes(HierarchyItem rootItem)
    {
        if (!_conversionSettings.Value.IdPostfixEnabled)
        {
            _logger.LogDebug("ID postfix not enabled, skipping postfix application");
            return;
        }

        try
        {
            var timestamp = DateTime.Now.ToString(_conversionSettings.Value.IdPostfixFormat);
            _logger.LogInformation("Applying ID postfix to all items: {Postfix}", timestamp);

            var usedIds = new HashSet<string> { "report-root" }; // Root never gets postfix
            ApplyIdPostfixesRecursive(rootItem, timestamp, usedIds, skipRoot: true);

            _logger.LogInformation("Successfully applied ID postfix to hierarchy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply ID postfixes, timestamp format may be invalid");
            // Don't throw - save hierarchy with existing IDs rather than failing
        }
    }

    /// <summary>
    /// Recursively applies postfixes to item and all descendants.
    /// </summary>
    private void ApplyIdPostfixesRecursive(
        HierarchyItem item,
        string postfix,
        HashSet<string> usedIds,
        bool skipRoot = false)
    {
        // Skip root item (level 0) - it always stays as "report-root"
        if (skipRoot && item.Level == 0)
        {
            _logger.LogDebug("Skipping root item (Id: {Id})", item.Id);

            // Process children but don't modify root
            foreach (var child in item.SubItems)
            {
                ApplyIdPostfixesRecursive(child, postfix, usedIds, skipRoot: false);
            }
            return;
        }

        // Regenerate ID from LinkName with postfix
        var baseId = PdfConversion.Utils.FilenameUtils.NormalizeFileName(item.LinkName, postfix);
        var uniqueId = PdfConversion.Utils.FilenameUtils.EnsureUniqueId(baseId, usedIds);

        _logger.LogDebug("Regenerating ID for '{LinkName}': '{OldId}' → '{NewId}'",
            item.LinkName, item.Id, uniqueId);

        // Update ID and DataRef
        item.Id = uniqueId;
        item.DataRef = $"{uniqueId}.xml";

        // Process all children recursively
        foreach (var child in item.SubItems)
        {
            ApplyIdPostfixesRecursive(child, postfix, usedIds, skipRoot: false);
        }
    }

    private XElement BuildItemElement(HierarchyItem item)
    {
        var element = new XElement("item",
            new XAttribute("id", item.Id),
            new XAttribute("level", item.Level),
            new XAttribute("data-ref", item.DataRef)
        );

        // Add optional header type (for badge display in Manual Mode)
        if (!string.IsNullOrEmpty(item.HeaderType))
        {
            element.Add(new XAttribute("header-type", item.HeaderType));
        }

        // Add optional attributes (excluding confidence - not saved in XML)
        if (item.IsUncertain)
        {
            element.Add(new XAttribute("is-uncertain", true));
        }

        if (!string.IsNullOrEmpty(item.Reasoning))
        {
            element.Add(new XAttribute("reasoning", item.Reasoning));
        }

        // Add optional TOC attributes
        if (item.TocStart)
        {
            element.Add(new XAttribute("data-tocstart", "true"));
        }

        if (item.TocEnd)
        {
            element.Add(new XAttribute("data-tocend", "true"));
        }

        if (!string.IsNullOrEmpty(item.TocNumber))
        {
            element.Add(new XAttribute("data-tocnumber", item.TocNumber));
        }

        if (!string.IsNullOrEmpty(item.TocStyle))
        {
            element.Add(new XAttribute("data-tocstyle", item.TocStyle));
        }

        if (item.TocHide)
        {
            element.Add(new XAttribute("data-tochide", "true"));
        }

        // Add web_page element with ASCII-normalized linkname
        element.Add(new XElement("web_page",
            new XElement("path", item.Path),
            new XElement("linkname", PdfConversion.Utils.FilenameUtils.NormalizeToAscii(item.LinkName))
        ));

        // Add sub_items if any
        if (item.SubItems.Any())
        {
            var subItemsElement = new XElement("sub_items");
            foreach (var subItem in item.SubItems)
            {
                subItemsElement.Add(BuildItemElement(subItem));
            }
            element.Add(subItemsElement);
        }

        return element;
    }

    public HierarchyItem? FindItemById(HierarchyStructure hierarchy, string id)
    {
        return FindItemByIdRecursive(hierarchy.Root, id);
    }

    private HierarchyItem? FindItemByIdRecursive(HierarchyItem item, string id)
    {
        if (item.Id == id)
        {
            return item;
        }

        foreach (var subItem in item.SubItems)
        {
            var found = FindItemByIdRecursive(subItem, id);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    public List<HierarchyItem> GetAllItems(HierarchyStructure hierarchy)
    {
        var items = new List<HierarchyItem>();
        CollectItemsRecursive(hierarchy.Root, items);
        return items;
    }

    private void CollectItemsRecursive(HierarchyItem item, List<HierarchyItem> accumulator)
    {
        accumulator.Add(item);
        foreach (var subItem in item.SubItems)
        {
            CollectItemsRecursive(subItem, accumulator);
        }
    }
}

using System.Xml.Linq;
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

    public HierarchyService(ILogger<HierarchyService> logger)
    {
        _logger = logger;
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
            DataRef = element.Attribute("data-ref")?.Value ?? string.Empty
        };

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

            var doc = new XDocument(
                new XElement("items",
                    new XElement("structured",
                        BuildItemElement(hierarchy.Root)
                    )
                )
            );

            await File.WriteAllTextAsync(filePath, doc.ToString());

            _logger.LogInformation("Successfully saved hierarchy to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save hierarchy to {FilePath}", filePath);
            throw;
        }
    }

    private XElement BuildItemElement(HierarchyItem item)
    {
        var element = new XElement("item",
            new XAttribute("id", item.Id),
            new XAttribute("level", item.Level),
            new XAttribute("data-ref", item.DataRef)
        );

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

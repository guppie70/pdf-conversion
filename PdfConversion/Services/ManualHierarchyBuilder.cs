using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for building hierarchies manually by adjusting indentation levels.
/// Maintains fixed document order - only indentation levels change.
/// </summary>
public interface IManualHierarchyBuilder
{
    /// <summary>
    /// Increases indentation level for selected headers.
    /// Validates that level doesn't exceed MaxNestingDepth.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    void IndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Decreases indentation level for selected headers.
    /// Validates that level doesn't go below 0.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    void OutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Marks selected headers as excluded from hierarchy.
    /// Excluded headers become in-section headers instead of section boundaries.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    void ExcludeItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Resets all headers to flat list (IndentLevel=0, IsExcluded=false).
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    void IncludeAllItems(List<DocumentHeader> allHeaders);

    /// <summary>
    /// Converts flat list with indentation levels into nested HierarchyItem structure.
    /// Maintains OriginalOrder for section generation.
    /// Excludes items with IsExcluded=true.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers with IndentLevel set</param>
    /// <returns>Root-level hierarchy items with nested children</returns>
    List<HierarchyItem> ConvertToHierarchy(List<DocumentHeader> allHeaders);
}

public class ManualHierarchyBuilder : IManualHierarchyBuilder
{
    private readonly ILogger<ManualHierarchyBuilder> _logger;
    private const int MaxNestingDepth = 10;

    public ManualHierarchyBuilder(ILogger<ManualHierarchyBuilder> logger)
    {
        _logger = logger;
    }

    public void IndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return;

        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
            {
                _logger.LogWarning("Header with OriginalOrder {Order} not found", order);
                continue;
            }

            if (header.IndentLevel >= MaxNestingDepth)
            {
                _logger.LogWarning("Cannot indent header '{Title}' - already at max depth {Depth}",
                    header.Title, MaxNestingDepth);
                continue;
            }

            header.IndentLevel++;
            _logger.LogDebug("Indented header '{Title}' to level {Level}",
                header.Title, header.IndentLevel);
        }
    }

    public void OutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return;

        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
            {
                _logger.LogWarning("Header with OriginalOrder {Order} not found", order);
                continue;
            }

            if (header.IndentLevel <= 0)
            {
                _logger.LogWarning("Cannot outdent header '{Title}' - already at root level",
                    header.Title);
                continue;
            }

            header.IndentLevel--;
            _logger.LogDebug("Outdented header '{Title}' to level {Level}",
                header.Title, header.IndentLevel);
        }
    }

    public void ExcludeItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return;

        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
            {
                _logger.LogWarning("Header with OriginalOrder {Order} not found", order);
                continue;
            }

            header.IsExcluded = true;
            _logger.LogDebug("Excluded header '{Title}' from hierarchy", header.Title);
        }
    }

    public void IncludeAllItems(List<DocumentHeader> allHeaders)
    {
        if (allHeaders == null)
            return;

        foreach (var header in allHeaders)
        {
            header.IndentLevel = 0;
            header.IsExcluded = false;
        }

        _logger.LogInformation("Reset all {Count} headers to flat list", allHeaders.Count);
    }

    public List<HierarchyItem> ConvertToHierarchy(List<DocumentHeader> allHeaders)
    {
        if (allHeaders == null || allHeaders.Count == 0)
            return new List<HierarchyItem>();

        // Filter out excluded headers and sort by document order
        var includedHeaders = allHeaders
            .Where(h => !h.IsExcluded)
            .OrderBy(h => h.OriginalOrder)
            .ToList();

        if (includedHeaders.Count == 0)
        {
            _logger.LogInformation("No headers to convert - all excluded");
            return new List<HierarchyItem>();
        }

        var rootItems = new List<HierarchyItem>();
        var parentStack = new Dictionary<int, HierarchyItem>();

        foreach (var header in includedHeaders)
        {
            var item = new HierarchyItem
            {
                Id = $"manual_{header.OriginalOrder}",
                LinkName = header.Title,
                Level = header.IndentLevel + 1,  // HierarchyItem uses 1-based levels
                DataRef = header.XPath,
                Confidence = 100  // Manual is always confident (100%)
            };

            // Find parent based on IndentLevel
            if (header.IndentLevel == 0)
            {
                // Root level item
                rootItems.Add(item);
                _logger.LogDebug("Added root item: '{LinkName}' at level {Level}",
                    item.LinkName, item.Level);
            }
            else
            {
                // Find parent at (IndentLevel - 1)
                var parentLevel = header.IndentLevel - 1;
                if (parentStack.TryGetValue(parentLevel, out var parent))
                {
                    parent.SubItems ??= new List<HierarchyItem>();
                    parent.SubItems.Add(item);
                    _logger.LogDebug("Added child item: '{LinkName}' to parent '{Parent}' at level {Level}",
                        item.LinkName, parent.LinkName, item.Level);
                }
                else
                {
                    // No parent found - orphaned item, treat as root
                    _logger.LogWarning("Orphaned item: '{LinkName}' at level {Level} (no parent at level {ParentLevel}), treating as root",
                        header.Title, header.IndentLevel, parentLevel);
                    rootItems.Add(item);
                    // Reset level to 1 since we're treating as root
                    item.Level = 1;
                }
            }

            // Update parent stack for this level
            // Clear all deeper levels to prevent stale references
            var levelsToRemove = parentStack.Keys.Where(k => k >= header.IndentLevel).ToList();
            foreach (var level in levelsToRemove)
            {
                parentStack.Remove(level);
            }
            parentStack[header.IndentLevel] = item;
        }

        _logger.LogInformation("Converted {IncludedCount} headers (out of {TotalCount}) to {RootCount} root hierarchy items",
            includedHeaders.Count, allHeaders.Count, rootItems.Count);

        return rootItems;
    }
}

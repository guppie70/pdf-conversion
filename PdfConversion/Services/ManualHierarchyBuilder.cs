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
    /// Validates that level doesn't exceed MaxNestingDepth and that no hierarchy gaps are created.
    /// Children move with their parent.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    /// <returns>Tuple indicating success and optional error message</returns>
    (bool success, string? errorMessage) IndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Decreases indentation level for selected headers.
    /// Validates that level doesn't go below 0.
    /// Children move with their parent.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    /// <returns>Tuple indicating success and optional error message</returns>
    (bool success, string? errorMessage) OutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Validates if selected headers can be indented without errors.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    /// <returns>True if indent operation would succeed</returns>
    bool CanIndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

    /// <summary>
    /// Validates if selected headers can be outdented without errors.
    /// </summary>
    /// <param name="allHeaders">Complete list of headers</param>
    /// <param name="selectedOrders">OriginalOrder values of selected headers</param>
    /// <returns>True if outdent operation would succeed</returns>
    bool CanOutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders);

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

    /// <summary>
    /// Finds the previous non-excluded header sibling before the given order.
    /// </summary>
    private DocumentHeader? GetPreviousSibling(List<DocumentHeader> allHeaders, int currentOrder)
    {
        return allHeaders
            .Where(h => !h.IsExcluded && h.OriginalOrder < currentOrder)
            .OrderByDescending(h => h.OriginalOrder)
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds all children (descendants) of a header.
    /// Children are subsequent items with higher indent level until we hit a sibling or uncle.
    /// </summary>
    private List<DocumentHeader> GetChildren(List<DocumentHeader> allHeaders, int parentOrder)
    {
        var parent = allHeaders.FirstOrDefault(h => h.OriginalOrder == parentOrder);
        if (parent == null) return new List<DocumentHeader>();

        var children = new List<DocumentHeader>();
        var parentLevel = parent.IndentLevel;

        // Collect all subsequent items with higher indent level
        foreach (var header in allHeaders.Where(h => h.OriginalOrder > parentOrder).OrderBy(h => h.OriginalOrder))
        {
            if (header.IndentLevel <= parentLevel)
                break; // Found a sibling or uncle, stop

            children.Add(header);
        }

        return children;
    }

    public bool CanIndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return false;

        // Run same validation as IndentItems but without modifying data
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
                return false;

            // Check if already at max depth
            if (header.IndentLevel >= MaxNestingDepth)
                return false;

            // Check for hierarchy gap
            var previousSibling = GetPreviousSibling(allHeaders, order);
            if (previousSibling == null)
                return false;

            var maxAllowedLevel = previousSibling.IndentLevel + 1;
            var newLevel = header.IndentLevel + 1;
            if (newLevel > maxAllowedLevel)
                return false;

            // Check if children would exceed max depth
            var children = GetChildren(allHeaders, order);
            foreach (var child in children)
            {
                if (child.IndentLevel + 1 > MaxNestingDepth)
                    return false;
            }
        }

        return true;
    }

    public bool CanOutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return false;

        // Run same validation as OutdentItems but without modifying data
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
                return false;

            // Check if already at root level
            if (header.IndentLevel <= 0)
                return false;
        }

        return true;
    }

    public (bool success, string? errorMessage) IndentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return (true, null);

        // Pre-validate ALL selected items before making any changes
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
            {
                var error = $"Header with OriginalOrder {order} not found";
                _logger.LogWarning(error);
                return (false, error);
            }

            // Check if already at max depth
            if (header.IndentLevel >= MaxNestingDepth)
            {
                var error = $"Cannot indent '{header.Title}' - already at maximum depth {MaxNestingDepth}";
                _logger.LogWarning(error);
                return (false, error);
            }

            // Check for hierarchy gap - can only indent to max (previous sibling's level + 1)
            var previousSibling = GetPreviousSibling(allHeaders, order);
            if (previousSibling == null)
            {
                // First item - must stay at level 0
                var error = "Cannot indent the first item - no previous sibling exists";
                _logger.LogWarning(error + ": '{Title}'", header.Title);
                return (false, error);
            }

            var maxAllowedLevel = previousSibling.IndentLevel + 1;
            var newLevel = header.IndentLevel + 1;
            if (newLevel > maxAllowedLevel)
            {
                var error = $"Cannot indent '{header.Title}' - would create hierarchy gap. Previous item '{previousSibling.Title}' is at level {previousSibling.IndentLevel}, maximum allowed is level {maxAllowedLevel}";
                _logger.LogWarning(error);
                return (false, error);
            }

            // Check if children would exceed max depth
            var children = GetChildren(allHeaders, order);
            foreach (var child in children)
            {
                if (child.IndentLevel + 1 > MaxNestingDepth)
                {
                    var error = $"Cannot indent '{header.Title}' - child '{child.Title}' would exceed maximum depth {MaxNestingDepth}";
                    _logger.LogWarning(error);
                    return (false, error);
                }
            }
        }

        // All validations passed - apply changes
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.First(h => h.OriginalOrder == order);
            var oldLevel = header.IndentLevel;

            // Get children BEFORE indenting (GetChildren uses parent's current IndentLevel)
            var children = GetChildren(allHeaders, order);

            // Indent the header
            header.IndentLevel++;

            // Indent all children
            foreach (var child in children)
            {
                child.IndentLevel++;
            }

            _logger.LogInformation("Indented header '{Title}' from level {OldLevel} to {NewLevel} (with {ChildCount} children)",
                header.Title, oldLevel, header.IndentLevel, children.Count);
        }

        return (true, null);
    }

    public (bool success, string? errorMessage) OutdentItems(List<DocumentHeader> allHeaders, List<int> selectedOrders)
    {
        if (allHeaders == null || selectedOrders == null || selectedOrders.Count == 0)
            return (true, null);

        // Pre-validate ALL selected items before making any changes
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.FirstOrDefault(h => h.OriginalOrder == order);
            if (header == null)
            {
                var error = $"Header with OriginalOrder {order} not found";
                _logger.LogWarning(error);
                return (false, error);
            }

            // Check if already at root level
            if (header.IndentLevel <= 0)
            {
                var error = $"Cannot outdent '{header.Title}' - already at root level (level 0)";
                _logger.LogWarning(error);
                return (false, error);
            }
        }

        // All validations passed - apply changes
        foreach (var order in selectedOrders)
        {
            var header = allHeaders.First(h => h.OriginalOrder == order);
            var oldLevel = header.IndentLevel;

            // Get children BEFORE outdenting (GetChildren uses parent's current IndentLevel)
            var children = GetChildren(allHeaders, order);

            // Outdent the header
            header.IndentLevel--;

            // Outdent all children
            foreach (var child in children)
            {
                child.IndentLevel--;
            }

            _logger.LogInformation("Outdented header '{Title}' from level {OldLevel} to {NewLevel} (with {ChildCount} children)",
                header.Title, oldLevel, header.IndentLevel, children.Count);
        }

        return (true, null);
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
                // Preserve the original ID if it exists, otherwise generate one
                Id = !string.IsNullOrEmpty(header.Id) ? header.Id : $"manual_{header.OriginalOrder}",
                LinkName = header.Title,
                Level = header.IndentLevel + 1,  // HierarchyItem uses 1-based levels
                DataRef = header.XPath,
                Confidence = 100,  // Manual is always confident (100%)
                HeaderType = header.Level.ToUpper(),  // Store header type (H2, H3, etc.)
                SequentialOrder = header.OriginalOrder  // Store sequential order (1, 2, 3...)
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

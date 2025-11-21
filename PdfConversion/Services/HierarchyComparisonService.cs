using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Compares generated hierarchies against ground truth for validation
/// </summary>
public class HierarchyComparisonService
{
    private readonly ILogger<HierarchyComparisonService> _logger;

    public HierarchyComparisonService(ILogger<HierarchyComparisonService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compare two hierarchies and find differences
    /// </summary>
    public ComparisonResult Compare(HierarchyItem generated, HierarchyItem groundTruth)
    {
        var result = new ComparisonResult();

        // Flatten both hierarchies for comparison
        var generatedFlat = FlattenHierarchy(generated);
        var truthFlat = FlattenHierarchy(groundTruth);

        // Build lookup by linkname (case-insensitive) - handle duplicates with list
        var truthLookup = truthFlat
            .GroupBy(item => item.LinkName.ToLowerInvariant())
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

        // Track which truth items have been matched
        var matchedTruthItems = new HashSet<HierarchyItem>();

        foreach (var genItem in generatedFlat)
        {
            var key = genItem.LinkName.ToLowerInvariant();

            if (truthLookup.TryGetValue(key, out var truthItems))
            {
                // Find best match (same level preferred, then closest level)
                var truthItem = truthItems
                    .Where(t => !matchedTruthItems.Contains(t))
                    .OrderBy(t => Math.Abs(t.Level - genItem.Level))
                    .FirstOrDefault();

                if (truthItem != null)
                {
                    matchedTruthItems.Add(truthItem);

                    // Found matching item - compare details
                    var match = new ItemMatch
                    {
                        LinkName = genItem.LinkName,
                        GeneratedLevel = genItem.Level,
                        GroundTruthLevel = truthItem.Level,
                        GeneratedParent = GetParentName(generated, genItem),
                        GroundTruthParent = GetParentName(groundTruth, truthItem),
                        ConfidenceScore = genItem.ConfidenceScore,
                        IsCorrect = genItem.Level == truthItem.Level
                    };

                    result.Matches.Add(match);

                    if (match.IsCorrect)
                    {
                        result.CorrectCount++;
                    }
                    else
                    {
                        result.IncorrectCount++;
                    }
                }
                else
                {
                    // All truth items with this name were already matched
                    result.ExtraItems.Add(genItem.LinkName);
                }
            }
            else
            {
                // Item in generated but not in ground truth
                result.ExtraItems.Add(genItem.LinkName);
            }
        }

        // Find items in ground truth but not in generated
        var generatedLookup = generatedFlat
            .GroupBy(item => item.LinkName.ToLowerInvariant())
            .ToDictionary(
                g => g.Key,
                g => g.ToList()
            );

        foreach (var truthItem in truthFlat)
        {
            var key = truthItem.LinkName.ToLowerInvariant();
            if (!generatedLookup.ContainsKey(key) || !matchedTruthItems.Contains(truthItem))
            {
                result.MissingItems.Add(truthItem.LinkName);
            }
        }

        result.TotalItems = result.Matches.Count;
        result.Accuracy = result.TotalItems > 0
            ? (double)result.CorrectCount / result.TotalItems
            : 0.0;

        return result;
    }

    /// <summary>
    /// Flatten hierarchy tree into list
    /// </summary>
    private List<HierarchyItem> FlattenHierarchy(HierarchyItem root)
    {
        var result = new List<HierarchyItem>();

        void Traverse(HierarchyItem item)
        {
            if (item.Level > 0) // Skip root
            {
                result.Add(item);
            }

            foreach (var child in item.SubItems)
            {
                Traverse(child);
            }
        }

        Traverse(root);
        return result;
    }

    /// <summary>
    /// Get parent item's linkname
    /// </summary>
    private string? GetParentName(HierarchyItem root, HierarchyItem target)
    {
        string? FindParent(HierarchyItem current, HierarchyItem search)
        {
            foreach (var child in current.SubItems)
            {
                if (child == search)
                {
                    return current.Level > 0 ? current.LinkName : null;
                }

                var result = FindParent(child, search);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        return FindParent(root, target);
    }
}

public class ComparisonResult
{
    public List<ItemMatch> Matches { get; set; } = new();
    public List<string> ExtraItems { get; set; } = new();
    public List<string> MissingItems { get; set; } = new();

    public int TotalItems { get; set; }
    public int CorrectCount { get; set; }
    public int IncorrectCount { get; set; }
    public double Accuracy { get; set; }
}

public class ItemMatch
{
    public required string LinkName { get; set; }
    public int GeneratedLevel { get; set; }
    public int GroundTruthLevel { get; set; }
    public string? GeneratedParent { get; set; }
    public string? GroundTruthParent { get; set; }
    public double ConfidenceScore { get; set; }
    public bool IsCorrect { get; set; }
}

using System.Text.RegularExpressions;
using PdfConversion.Models;

namespace PdfConversion.Services;

public class PatternLearningService
{
    private readonly ILogger<PatternLearningService> _logger;
    private readonly IHierarchyService _hierarchyService;

    public PatternLearningService(
        ILogger<PatternLearningService> logger,
        IHierarchyService hierarchyService)
    {
        _logger = logger;
        _hierarchyService = hierarchyService;
    }

    /// <summary>
    /// Analyzes all training hierarchies and extracts universal patterns
    /// </summary>
    public async Task<PatternDatabase> AnalyzeTrainingHierarchies(string trainingDir)
    {
        var database = new PatternDatabase
        {
            CreatedAt = DateTime.UtcNow
        };

        // Find all XML files (hierarchy files can have various names)
        var files = Directory.GetFiles(trainingDir, "*.xml", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).StartsWith("hierarchy", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _logger.LogInformation("Found {Count} training hierarchy files", files.Length);

        var allItems = new List<HierarchyItem>();
        var itemsByLevel = new Dictionary<int, List<HierarchyItem>>();
        var sequences = new List<(string current, string previous)>();
        var parentChildPairs = new List<(string parent, string child)>();
        var numberingExamples = new List<(string pattern, int level)>();

        // Parse all hierarchies
        foreach (var file in files)
        {
            try
            {
                _logger.LogInformation("Analyzing {File}", Path.GetFileName(Path.GetDirectoryName(file)));

                var hierarchy = await _hierarchyService.LoadHierarchyAsync(file);
                var items = FlattenHierarchy(hierarchy.Root).Where(i => i.Level > 0).ToList();

                allItems.AddRange(items);

                // Group by level
                foreach (var item in items)
                {
                    if (!itemsByLevel.ContainsKey(item.Level))
                        itemsByLevel[item.Level] = new List<HierarchyItem>();
                    itemsByLevel[item.Level].Add(item);
                }

                // Extract sequences (same-level siblings)
                for (int i = 1; i < items.Count; i++)
                {
                    if (items[i].Level == items[i - 1].Level)
                    {
                        sequences.Add((items[i].LinkName, items[i - 1].LinkName));
                    }
                }

                // Extract parent-child relationships
                ExtractParentChildRelationships(hierarchy.Root, parentChildPairs);

                // Extract numbering patterns
                foreach (var item in items)
                {
                    if (!string.IsNullOrEmpty(item.TocNumber))
                    {
                        numberingExamples.Add((item.TocNumber, item.Level));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse {File}", file);
            }
        }

        database.TotalHierarchiesAnalyzed = files.Length;
        database.TotalItemsAnalyzed = allItems.Count;

        _logger.LogInformation("Total items analyzed: {Count}", allItems.Count);

        // Build level profiles
        foreach (var (level, items) in itemsByLevel.OrderBy(x => x.Key))
        {
            _logger.LogInformation("Building profile for level {Level} ({Count} items)", level, items.Count);
            database.LevelProfiles[level] = BuildLevelProfile(level, items);
        }

        // Build section vocabulary
        _logger.LogInformation("Building section vocabulary");
        database.CommonSections = BuildSectionVocabulary(allItems);

        // Build sequence patterns
        _logger.LogInformation("Building sequence patterns");
        database.TypicalSequences = BuildSequencePatterns(sequences);

        // Build numbering patterns
        _logger.LogInformation("Building numbering patterns");
        database.NumberingPatterns = BuildNumberingPatterns(numberingExamples);

        // Build parent-child patterns
        _logger.LogInformation("Building parent-child patterns");
        database.ParentChildPatterns = BuildParentChildPatterns(parentChildPairs);

        return await Task.FromResult(database);
    }

    private List<HierarchyItem> FlattenHierarchy(HierarchyItem root)
    {
        var result = new List<HierarchyItem>();
        var queue = new Queue<HierarchyItem>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var child in current.SubItems)
            {
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private void ExtractParentChildRelationships(HierarchyItem node, List<(string parent, string child)> pairs)
    {
        foreach (var child in node.SubItems)
        {
            if (node.Level > 0) // Skip root
            {
                pairs.Add((node.LinkName, child.LinkName));
            }
            ExtractParentChildRelationships(child, pairs);
        }
    }

    private LevelProfile BuildLevelProfile(int level, List<HierarchyItem> items)
    {
        var wordCounts = items.Select(i => CountWords(i.LinkName)).ToList();
        var childCounts = items.Select(i => i.SubItems.Count).ToList();

        // Calculate sibling counts
        var siblingCounts = new List<int>();
        var itemsByParent = items.GroupBy(i => GetParentKey(i));
        foreach (var group in itemsByParent)
        {
            var count = group.Count();
            foreach (var item in group)
            {
                siblingCounts.Add(count);
            }
        }

        // Count header frequencies
        var headerFrequencies = items
            .GroupBy(i => NormalizeHeader(i.LinkName))
            .Select(g => new HeaderFrequency
            {
                HeaderText = g.Key,
                Occurrences = g.Count(),
                Frequency = (double)g.Count() / items.Count
            })
            .OrderByDescending(h => h.Occurrences)
            .Take(20)
            .ToList();

        return new LevelProfile
        {
            Level = level,
            TotalOccurrences = items.Count,
            AvgWordCount = wordCounts.Average(),
            MinWordCount = wordCounts.Min(),
            MaxWordCount = wordCounts.Max(),
            MedianWordCount = Median(wordCounts),
            AvgChildCount = childCounts.Average(),
            MedianChildCount = Median(childCounts),
            MaxChildCount = childCounts.Max(),
            AvgSiblingCount = siblingCounts.Any() ? siblingCounts.Average() : 0,
            CommonHeaders = headerFrequencies
        };
    }

    private List<SectionVocabulary> BuildSectionVocabulary(List<HierarchyItem> allItems)
    {
        var vocabulary = new Dictionary<string, SectionVocabulary>();

        // Group by normalized header
        var groups = allItems
            .Where(i => i.Level > 0)
            .GroupBy(i => NormalizeHeader(i.LinkName));

        foreach (var group in groups)
        {
            var normalized = group.Key;
            var items = group.ToList();

            // Build level distribution
            var levelDistribution = items
                .GroupBy(i => i.Level)
                .ToDictionary(g => g.Key, g => g.Count());

            var mostCommonLevel = levelDistribution
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;

            var totalOccurrences = items.Count;
            var mostCommonCount = levelDistribution[mostCommonLevel];
            var confidence = (double)mostCommonCount / totalOccurrences;

            // Find common parents and children
            var parents = items
                .Select(i => GetParentHeader(i))
                .Where(p => !string.IsNullOrEmpty(p))
                .GroupBy(p => p)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            var children = items
                .SelectMany(i => i.SubItems.Select(c => NormalizeHeader(c.LinkName)))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList();

            vocabulary[normalized] = new SectionVocabulary
            {
                HeaderText = normalized,
                MostCommonLevel = mostCommonLevel,
                LevelDistribution = levelDistribution,
                TotalOccurrences = totalOccurrences,
                Confidence = confidence,
                CommonParents = parents,
                CommonChildren = children
            };
        }

        return vocabulary.Values
            .OrderByDescending(v => v.TotalOccurrences)
            .ToList();
    }

    private List<SequencePattern> BuildSequencePatterns(List<(string current, string previous)> sequences)
    {
        var patterns = sequences
            .GroupBy(s => s)
            .Select(g => new SequencePattern
            {
                SectionName = NormalizeHeader(g.Key.current),
                TypicallyFollows = NormalizeHeader(g.Key.previous),
                Occurrences = g.Count(),
                Confidence = 0.0 // Will calculate after
            })
            .OrderByDescending(p => p.Occurrences)
            .ToList();

        // Calculate confidence: how often does current follow previous vs all occurrences of current
        var currentTotals = patterns
            .GroupBy(p => p.SectionName)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Occurrences));

        foreach (var pattern in patterns)
        {
            if (currentTotals.TryGetValue(pattern.SectionName, out var total))
            {
                pattern.Confidence = (double)pattern.Occurrences / total;
            }
        }

        return patterns
            .Where(p => p.Occurrences >= 2) // At least 2 occurrences
            .OrderByDescending(p => p.Confidence)
            .ThenByDescending(p => p.Occurrences)
            .Take(50)
            .ToList();
    }

    private Dictionary<string, NumberingPattern> BuildNumberingPatterns(List<(string pattern, int level)> examples)
    {
        var patterns = new Dictionary<string, NumberingPattern>();

        // Group by pattern type (extract pattern structure)
        var groups = examples
            .GroupBy(e => ClassifyNumberingPattern(e.pattern));

        foreach (var group in groups)
        {
            var patternType = group.Key;
            var items = group.ToList();

            // Find most common level for this pattern
            var levelCounts = items
                .GroupBy(i => i.level)
                .ToDictionary(g => g.Key, g => g.Count());

            var mostCommonLevel = levelCounts
                .OrderByDescending(kvp => kvp.Value)
                .First().Key;

            var totalOccurrences = items.Count;
            var mostCommonCount = levelCounts[mostCommonLevel];
            var confidence = (double)mostCommonCount / totalOccurrences;

            patterns[patternType] = new NumberingPattern
            {
                Pattern = patternType,
                MostCommonLevel = mostCommonLevel,
                Occurrences = totalOccurrences,
                Confidence = confidence
            };
        }

        return patterns;
    }

    private List<ParentChildPattern> BuildParentChildPatterns(List<(string parent, string child)> pairs)
    {
        var patterns = pairs
            .GroupBy(p => (NormalizeHeader(p.parent), NormalizeHeader(p.child)))
            .Select(g => new ParentChildPattern
            {
                ParentHeader = g.Key.Item1,
                ChildHeader = g.Key.Item2,
                Occurrences = g.Count(),
                Confidence = 0.0 // Will calculate after
            })
            .OrderByDescending(p => p.Occurrences)
            .ToList();

        // Calculate confidence: how often does parent have this child vs all children
        var parentTotals = patterns
            .GroupBy(p => p.ParentHeader)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Occurrences));

        foreach (var pattern in patterns)
        {
            if (parentTotals.TryGetValue(pattern.ParentHeader, out var total))
            {
                pattern.Confidence = (double)pattern.Occurrences / total;
            }
        }

        return patterns
            .Where(p => p.Occurrences >= 2) // At least 2 occurrences
            .OrderByDescending(p => p.Confidence)
            .ThenByDescending(p => p.Occurrences)
            .Take(50)
            .ToList();
    }

    // Helper methods

    private string NormalizeHeader(string header)
    {
        // Remove numbering, extra whitespace, punctuation
        var normalized = Regex.Replace(header, @"^\d+\.?\s*", ""); // Remove leading numbers
        normalized = Regex.Replace(normalized, @"^[A-Z]\.\s*", ""); // Remove "A. " style
        normalized = Regex.Replace(normalized, @"^\([a-z]\)\s*", ""); // Remove "(a) " style
        normalized = Regex.Replace(normalized, @"\s+", " "); // Normalize whitespace
        normalized = normalized.Trim().TrimEnd('.', ',', ';', ':');
        return normalized;
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private string GetParentKey(HierarchyItem item)
    {
        // Create a unique key for the parent (would need to track parent references)
        // For now, use a simple approach
        return $"parent_{item.Level - 1}";
    }

    private string GetParentHeader(HierarchyItem item)
    {
        // This would need to track parent references during parsing
        // For now, return empty - would need to enhance the data structure
        return string.Empty;
    }

    private string ClassifyNumberingPattern(string pattern)
    {
        // Classify numbering patterns into types
        if (Regex.IsMatch(pattern, @"^\d+\.$"))
            return "1.";
        if (Regex.IsMatch(pattern, @"^\d+\.\d+$"))
            return "1.1";
        if (Regex.IsMatch(pattern, @"^\d+\.\d+\.\d+$"))
            return "1.1.1";
        if (Regex.IsMatch(pattern, @"^[A-Z]\.$"))
            return "A.";
        if (Regex.IsMatch(pattern, @"^\([a-z]\)$"))
            return "(a)";
        if (Regex.IsMatch(pattern, @"^\([ivxlc]+\)$", RegexOptions.IgnoreCase))
            return "(i)";

        return "other";
    }

    private double Median(List<int> values)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        else
            return sorted[mid];
    }

    private double Median(List<double> values)
    {
        if (values.Count == 0)
            return 0;

        var sorted = values.OrderBy(x => x).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        else
            return sorted[mid];
    }
}

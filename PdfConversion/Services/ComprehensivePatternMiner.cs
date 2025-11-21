using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Mines comprehensive section patterns from training data, extracting ALL sections at all levels.
/// Builds a complete database of:
/// - Section patterns with level distributions
/// - Parent-child relationships
/// - Typical contexts for each section
/// </summary>
public class ComprehensivePatternMiner
{
    private readonly ILogger<ComprehensivePatternMiner> _logger;
    private static readonly string TrainingDataPath = Path.Combine("data", "training-material", "hierarchies");

    public ComprehensivePatternMiner(ILogger<ComprehensivePatternMiner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Mines all training hierarchies to extract comprehensive patterns.
    /// This is more complete than the existing learned-rules.json as it:
    /// 1. Captures ALL sections (not just common ones)
    /// 2. Records parent relationships with frequencies
    /// 3. Tracks context patterns for better placement decisions
    /// </summary>
    public ComprehensivePatternDatabase MinePatterns()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sectionPatterns = new Dictionary<string, SectionPattern>();
        var filesProcessed = 0;

        _logger.LogInformation("[ComprehensivePatternMiner] Starting pattern mining from training data");

        if (!Directory.Exists(TrainingDataPath))
        {
            _logger.LogWarning("[ComprehensivePatternMiner] Training data path not found: {Path}", TrainingDataPath);
            return ComprehensivePatternDatabase.Empty();
        }

        // Find all hierarchy XML files recursively
        var hierarchyFiles = Directory.GetFiles(TrainingDataPath, "*.xml", SearchOption.AllDirectories);

        _logger.LogInformation("[ComprehensivePatternMiner] Found {Count} hierarchy files to analyze", hierarchyFiles.Length);

        foreach (var file in hierarchyFiles)
        {
            try
            {
                filesProcessed++;
                ProcessHierarchyFile(file, sectionPatterns);

                if (filesProcessed % 50 == 0)
                {
                    _logger.LogDebug("[ComprehensivePatternMiner] Progress: {Count}/{Total} files processed",
                        filesProcessed, hierarchyFiles.Length);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ComprehensivePatternMiner] Failed to process file: {File}", file);
            }
        }

        stopwatch.Stop();

        // Calculate confidence scores
        foreach (var pattern in sectionPatterns.Values)
        {
            pattern.Confidence = pattern.TotalOccurrences / (double)filesProcessed;

            // Sort parent patterns by frequency
            pattern.TypicalParents = pattern.TypicalParents
                .OrderByDescending(p => p.Frequency)
                .ToList();
        }

        // Convert to sorted list
        var sortedPatterns = sectionPatterns.Values
            .OrderByDescending(p => p.TotalOccurrences)
            .ToList();

        _logger.LogInformation("[ComprehensivePatternMiner] Mining complete:");
        _logger.LogInformation("  - Files processed: {Files}", filesProcessed);
        _logger.LogInformation("  - Unique section patterns: {Patterns}", sortedPatterns.Count);
        _logger.LogInformation("  - Duration: {Ms}ms", stopwatch.ElapsedMilliseconds);

        // Log statistics by level
        for (int level = 1; level <= 4; level++)
        {
            var levelCount = sortedPatterns.Count(p => p.MostCommonLevel == level);
            _logger.LogInformation("  - Level {Level} patterns: {Count}", level, levelCount);
        }

        // Log top 10 most common patterns
        _logger.LogInformation("[ComprehensivePatternMiner] Top 10 most common patterns:");
        foreach (var pattern in sortedPatterns.Take(10))
        {
            _logger.LogInformation("  - \"{Pattern}\" (Level {Level}, appears {Count} times, {Pct:F1}%)",
                pattern.NormalizedTitle,
                pattern.MostCommonLevel,
                pattern.TotalOccurrences,
                (pattern.Confidence * 100.0));
        }

        return new ComprehensivePatternDatabase
        {
            Patterns = sortedPatterns,
            TotalHierarchiesAnalyzed = filesProcessed,
            MinedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Processes a single hierarchy file to extract section patterns.
    /// </summary>
    private void ProcessHierarchyFile(
        string filePath,
        Dictionary<string, SectionPattern> sectionPatterns)
    {
        var doc = XDocument.Load(filePath);

        // Find the root item (level="0")
        var rootItem = doc.Descendants("item")
            .FirstOrDefault(item => item.Attribute("level")?.Value == "0");

        if (rootItem == null)
        {
            _logger.LogWarning("[ComprehensivePatternMiner] No root item found in {File}", filePath);
            return;
        }

        // Process children of root (the actual sections)
        var subItems = rootItem.Element("sub_items");
        if (subItems != null)
        {
            foreach (var item in subItems.Elements("item"))
            {
                ProcessItemRecursively(item, null, sectionPatterns);
            }
        }
    }

    /// <summary>
    /// Processes a single item and its children recursively.
    /// </summary>
    private void ProcessItemRecursively(
        XElement item,
        XElement? parentItem,
        Dictionary<string, SectionPattern> sectionPatterns)
    {
        // Extract item information
        var linkName = item.Element("web_page")?.Element("linkname")?.Value;
        if (string.IsNullOrWhiteSpace(linkName)) return;

        var levelStr = item.Attribute("level")?.Value;
        if (!int.TryParse(levelStr, out var level)) return;

        // Skip root level (level 0)
        if (level == 0) return;

        // Normalize the section title
        var normalizedTitle = NormalizeText(linkName);

        // Get or create pattern entry
        if (!sectionPatterns.ContainsKey(normalizedTitle))
        {
            sectionPatterns[normalizedTitle] = new SectionPattern
            {
                NormalizedTitle = normalizedTitle,
                OriginalTitles = new List<string>(),
                LevelFrequency = new Dictionary<int, int>(),
                TypicalParents = new List<ParentPattern>(),
                TotalOccurrences = 0,
                Confidence = 0.0
            };
        }

        var pattern = sectionPatterns[normalizedTitle];

        // Update statistics
        pattern.TotalOccurrences++;
        if (!pattern.OriginalTitles.Contains(linkName))
        {
            pattern.OriginalTitles.Add(linkName);
        }

        // Update level frequency
        if (!pattern.LevelFrequency.ContainsKey(level))
        {
            pattern.LevelFrequency[level] = 0;
        }
        pattern.LevelFrequency[level]++;

        // Record parent relationship
        if (parentItem != null)
        {
            var parentLinkName = parentItem.Element("web_page")?.Element("linkname")?.Value;
            if (!string.IsNullOrWhiteSpace(parentLinkName))
            {
                var normalizedParent = NormalizeText(parentLinkName);

                var existingParent = pattern.TypicalParents
                    .FirstOrDefault(p => p.ParentNormalizedTitle == normalizedParent);

                if (existingParent != null)
                {
                    existingParent.Frequency++;
                }
                else
                {
                    pattern.TypicalParents.Add(new ParentPattern
                    {
                        ParentNormalizedTitle = normalizedParent,
                        Frequency = 1
                    });
                }
            }
        }

        // Process children recursively
        var subItems = item.Element("sub_items");
        if (subItems != null)
        {
            foreach (var childItem in subItems.Elements("item"))
            {
                ProcessItemRecursively(childItem, item, sectionPatterns);
            }
        }
    }

    /// <summary>
    /// Normalizes text for consistent pattern matching.
    /// Uses most aggressive approach: removes ALL punctuation and spaces.
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Convert to lowercase
        var normalized = text.Trim().ToLowerInvariant();

        // Remove ALL punctuation
        normalized = Regex.Replace(normalized, @"['`"",:;!?()\[\]{}\*\.\-]", string.Empty);

        // Remove ALL spaces
        normalized = normalized.Replace(" ", string.Empty);

        return normalized;
    }
}

/// <summary>
/// Comprehensive database of all section patterns from training data
/// </summary>
public class ComprehensivePatternDatabase
{
    /// <summary>
    /// List of all section patterns sorted by frequency
    /// </summary>
    public List<SectionPattern> Patterns { get; set; } = new();

    /// <summary>
    /// Total number of hierarchies analyzed
    /// </summary>
    public int TotalHierarchiesAnalyzed { get; set; }

    /// <summary>
    /// When this database was mined
    /// </summary>
    public DateTime MinedAt { get; set; }

    /// <summary>
    /// Finds matching patterns for a normalized title.
    /// Returns list sorted by confidence (most confident first).
    /// </summary>
    public List<SectionPattern> FindMatches(string normalizedTitle, double minConfidence = 0.01)
    {
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            return new List<SectionPattern>();

        return Patterns
            .Where(p => p.NormalizedTitle.Equals(normalizedTitle, StringComparison.Ordinal) &&
                       p.Confidence >= minConfidence)
            .OrderByDescending(p => p.Confidence)
            .ToList();
    }

    /// <summary>
    /// Finds the most likely parent for a given section.
    /// </summary>
    public string? FindMostLikelyParent(string normalizedTitle)
    {
        var pattern = Patterns.FirstOrDefault(p =>
            p.NormalizedTitle.Equals(normalizedTitle, StringComparison.Ordinal));

        return pattern?.TypicalParents.FirstOrDefault()?.ParentNormalizedTitle;
    }

    /// <summary>
    /// Returns an empty database for fallback scenarios
    /// </summary>
    public static ComprehensivePatternDatabase Empty()
    {
        return new ComprehensivePatternDatabase
        {
            Patterns = new List<SectionPattern>(),
            TotalHierarchiesAnalyzed = 0,
            MinedAt = DateTime.MinValue
        };
    }
}

/// <summary>
/// Information about a specific section pattern discovered in training data
/// </summary>
public class SectionPattern
{
    /// <summary>
    /// Normalized form of the section title (lowercase, no punctuation/spaces)
    /// </summary>
    public string NormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Original titles as they appeared in training data
    /// </summary>
    public List<string> OriginalTitles { get; set; } = new();

    /// <summary>
    /// Distribution of levels where this section appears (Level -> Count)
    /// </summary>
    public Dictionary<int, int> LevelFrequency { get; set; } = new();

    /// <summary>
    /// Typical parent sections for this section
    /// </summary>
    public List<ParentPattern> TypicalParents { get; set; } = new();

    /// <summary>
    /// Total number of times this pattern appears
    /// </summary>
    public int TotalOccurrences { get; set; }

    /// <summary>
    /// Confidence score (0.0-1.0) based on frequency across hierarchies
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Most common level for this section
    /// </summary>
    public int MostCommonLevel => LevelFrequency.Any()
        ? LevelFrequency.OrderByDescending(kvp => kvp.Value).First().Key
        : 0;

    /// <summary>
    /// Examples of original titles (first 3)
    /// </summary>
    public string Examples => string.Join(", ", OriginalTitles.Take(3));
}

/// <summary>
/// Information about a parent-child relationship pattern
/// </summary>
public class ParentPattern
{
    /// <summary>
    /// Normalized title of the parent section
    /// </summary>
    public string ParentNormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// How often this parent appears for the child
    /// </summary>
    public int Frequency { get; set; }
}

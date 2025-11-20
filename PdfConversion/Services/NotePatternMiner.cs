using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfConversion.Utils;

namespace PdfConversion.Services;

/// <summary>
/// Mines training data to extract common patterns for financial statement notes.
/// Analyzes 269 training hierarchies to identify note sections and their characteristics.
/// </summary>
public class NotePatternMiner
{
    private readonly ILogger<NotePatternMiner> _logger;
    private static readonly string TrainingDataPath = Path.Combine("data", "training-material", "hierarchies");

    public NotePatternMiner(ILogger<NotePatternMiner> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Mines all training hierarchies to extract note patterns.
    /// Returns a NotePatternDatabase with normalized note titles and frequencies.
    /// </summary>
    public NotePatternDatabase MinePatterns()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var notePatterns = new Dictionary<string, NotePatternInfo>();
        var notesParentPatterns = new HashSet<string>();
        var filesProcessed = 0;
        var noteSectionsFound = 0;

        _logger.LogInformation("[NotePatternMiner] Starting pattern mining from training data");

        if (!Directory.Exists(TrainingDataPath))
        {
            _logger.LogWarning("[NotePatternMiner] Training data path not found: {Path}", TrainingDataPath);
            return NotePatternDatabase.Empty();
        }

        // Find all hierarchy XML files recursively
        var hierarchyFiles = Directory.GetFiles(TrainingDataPath, "*.xml", SearchOption.AllDirectories);

        _logger.LogInformation("[NotePatternMiner] Found {Count} hierarchy files to analyze", hierarchyFiles.Length);

        foreach (var file in hierarchyFiles)
        {
            try
            {
                filesProcessed++;
                var noteCount = ProcessHierarchyFile(file, notePatterns, notesParentPatterns);
                if (noteCount > 0)
                {
                    noteSectionsFound += noteCount;
                    _logger.LogDebug("[NotePatternMiner] File {File}: Found {Count} notes",
                        Path.GetFileName(file), noteCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NotePatternMiner] Failed to process file: {File}", file);
            }
        }

        stopwatch.Stop();

        _logger.LogInformation("[NotePatternMiner] Mining complete:");
        _logger.LogInformation("  - Files processed: {Files}", filesProcessed);
        _logger.LogInformation("  - Note sections found: {Sections}", noteSectionsFound);
        _logger.LogInformation("  - Unique note patterns: {Patterns}", notePatterns.Count);
        _logger.LogInformation("  - Duration: {Ms}ms", stopwatch.ElapsedMilliseconds);

        // Sort patterns by frequency
        var sortedPatterns = notePatterns
            .OrderByDescending(p => p.Value.Frequency)
            .Select(p => p.Value)
            .ToList();

        // Log top 20 most common note patterns
        _logger.LogInformation("[NotePatternMiner] Top 20 most common note patterns:");
        foreach (var pattern in sortedPatterns.Take(20))
        {
            _logger.LogInformation("  - \"{Pattern}\" (appears {Count} times, {Pct:F1}%)",
                pattern.NormalizedTitle,
                pattern.Frequency,
                (pattern.Frequency * 100.0 / filesProcessed));
        }

        return new NotePatternDatabase
        {
            Patterns = sortedPatterns,
            NotesParentPatterns = notesParentPatterns.ToList(),
            TotalHierarchiesAnalyzed = filesProcessed,
            TotalNoteSectionsFound = noteSectionsFound,
            MinedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Processes a single hierarchy file to extract note patterns.
    /// Returns the count of notes found in this file.
    /// </summary>
    private int ProcessHierarchyFile(
        string filePath,
        Dictionary<string, NotePatternInfo> notePatterns,
        HashSet<string> notesParentPatterns)
    {
        var doc = XDocument.Load(filePath);
        var notesFound = 0;

        // Find items with linkname containing "Notes to"
        var notesParents = doc.Descendants("item")
            .Where(item =>
            {
                var linkname = item.Element("web_page")?.Element("linkname")?.Value;
                return linkname != null &&
                       (linkname.Contains("Notes to", StringComparison.OrdinalIgnoreCase) ||
                        linkname.Contains("Notes on", StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        foreach (var notesParent in notesParents)
        {
            // Store the parent pattern
            var parentLinkName = notesParent.Element("web_page")?.Element("linkname")?.Value;
            if (parentLinkName != null)
            {
                var normalizedParent = NormalizeNoteTitle(parentLinkName);
                notesParentPatterns.Add(normalizedParent);
            }

            // Find all children of this Notes section
            var subItems = notesParent.Element("sub_items");
            if (subItems == null) continue;

            var noteItems = subItems.Descendants("item")
                .Where(item =>
                {
                    var levelAttr = item.Attribute("level")?.Value;
                    // Notes are typically level 3 or 4
                    return levelAttr == "3" || levelAttr == "4";
                })
                .ToList();

            foreach (var noteItem in noteItems)
            {
                var linkname = noteItem.Element("web_page")?.Element("linkname")?.Value;
                if (string.IsNullOrWhiteSpace(linkname)) continue;

                // Skip if it looks like a Notes parent itself
                if (linkname.Contains("Notes to", StringComparison.OrdinalIgnoreCase) ||
                    linkname.Contains("Notes on", StringComparison.OrdinalIgnoreCase))
                    continue;

                notesFound++;

                // Normalize the note title for pattern matching
                var normalized = NormalizeNoteTitle(linkname);

                if (!notePatterns.ContainsKey(normalized))
                {
                    notePatterns[normalized] = new NotePatternInfo
                    {
                        NormalizedTitle = normalized,
                        OriginalTitles = new List<string>(),
                        Frequency = 0
                    };
                }

                notePatterns[normalized].Frequency++;
                if (!notePatterns[normalized].OriginalTitles.Contains(linkname))
                {
                    notePatterns[normalized].OriginalTitles.Add(linkname);
                }
            }
        }

        return notesFound;
    }

    /// <summary>
    /// Normalizes note titles for consistent pattern matching.
    /// Removes punctuation, extra spaces, and converts to lowercase.
    /// </summary>
    private string NormalizeNoteTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Convert to lowercase
        var normalized = title.Trim().ToLowerInvariant();

        // Remove common prefixes like "note:", "note 1:", etc.
        normalized = Regex.Replace(normalized, @"^note\s*\d*\s*:?\s*", string.Empty, RegexOptions.IgnoreCase);

        // Remove ALL punctuation
        normalized = Regex.Replace(normalized, @"['`"",:;!?()\[\]{}\*\.\-/]", string.Empty);

        // Collapse multiple spaces to single space
        normalized = Regex.Replace(normalized, @"\s+", " ");

        // Remove leading/trailing spaces
        normalized = normalized.Trim();

        return normalized;
    }
}

/// <summary>
/// Database of mined note patterns from training data
/// </summary>
public class NotePatternDatabase
{
    /// <summary>
    /// List of note patterns sorted by frequency (most common first)
    /// </summary>
    public List<NotePatternInfo> Patterns { get; set; } = new();

    /// <summary>
    /// Normalized titles of "Notes to..." parent sections
    /// </summary>
    public List<string> NotesParentPatterns { get; set; } = new();

    /// <summary>
    /// Total number of hierarchies analyzed during mining
    /// </summary>
    public int TotalHierarchiesAnalyzed { get; set; }

    /// <summary>
    /// Total number of note sections found across all hierarchies
    /// </summary>
    public int TotalNoteSectionsFound { get; set; }

    /// <summary>
    /// When this database was mined
    /// </summary>
    public DateTime MinedAt { get; set; }

    /// <summary>
    /// Checks if a normalized title matches a known note pattern.
    /// Uses frequency threshold: pattern must appear in at least 1% of hierarchies.
    /// </summary>
    public bool IsKnownNotePattern(string normalizedTitle, double minFrequencyThreshold = 0.01)
    {
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            return false;

        var minCount = (int)(TotalHierarchiesAnalyzed * minFrequencyThreshold);

        return Patterns.Any(p =>
            p.NormalizedTitle.Equals(normalizedTitle, StringComparison.Ordinal) &&
            p.Frequency >= minCount);
    }

    /// <summary>
    /// Returns an empty database for fallback scenarios
    /// </summary>
    public static NotePatternDatabase Empty()
    {
        return new NotePatternDatabase
        {
            Patterns = new List<NotePatternInfo>(),
            NotesParentPatterns = new List<string>(),
            TotalHierarchiesAnalyzed = 0,
            TotalNoteSectionsFound = 0,
            MinedAt = DateTime.MinValue
        };
    }
}

/// <summary>
/// Information about a specific note pattern discovered in training data
/// </summary>
public class NotePatternInfo
{
    /// <summary>
    /// Normalized form of the note title (lowercase, no punctuation)
    /// </summary>
    public string NormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Original titles as they appeared in training data (for reference)
    /// </summary>
    public List<string> OriginalTitles { get; set; } = new();

    /// <summary>
    /// Number of times this pattern appears across all training hierarchies
    /// </summary>
    public int Frequency { get; set; }

    /// <summary>
    /// Examples of original titles (first 3)
    /// </summary>
    public string Examples => string.Join(", ", OriginalTitles.Take(3));
}

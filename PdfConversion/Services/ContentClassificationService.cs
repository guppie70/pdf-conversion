using System.Text.RegularExpressions;

namespace PdfConversion.Services;

/// <summary>
/// Pass 1: Classifies headers by matching against comprehensive pattern database.
/// Identifies WHAT each section is semantically before determining WHERE it belongs.
/// </summary>
public class ContentClassificationService
{
    private readonly ILogger<ContentClassificationService> _logger;
    private readonly ComprehensivePatternDatabase _patterns;

    public ContentClassificationService(
        ILogger<ContentClassificationService> logger,
        ComprehensivePatternDatabase patterns)
    {
        _logger = logger;
        _patterns = patterns;
    }

    /// <summary>
    /// Classifies all headers from normalized XML.
    /// Returns list of classified sections with pattern matches and confidence scores.
    /// </summary>
    public List<ClassifiedSection> ClassifyHeaders(List<HierarchyGeneratorService.HeaderInfo> headers)
    {
        var classified = new List<ClassifiedSection>();

        _logger.LogInformation("[Pass 1] Content Classification starting for {Count} headers", headers.Count);

        foreach (var header in headers)
        {
            var section = ClassifyHeader(header);
            classified.Add(section);

            // Log classification results
            if (section.MatchedPattern != null)
            {
                _logger.LogDebug("[Pass 1] Line {Line}: \"{Header}\"",
                    section.LineNumber, header.Text);
                _logger.LogDebug("  - Matched pattern: \"{Pattern}\" (confidence {Conf:F2})",
                    section.MatchedPattern.NormalizedTitle, section.MatchConfidence);
                _logger.LogDebug("  - Typical levels: {Levels}",
                    string.Join(", ", section.MatchedPattern.LevelFrequency
                        .OrderByDescending(kvp => kvp.Value)
                        .Select(kvp => $"Level {kvp.Key} ({kvp.Value}x)")));

                if (section.MatchedPattern.TypicalParents.Any())
                {
                    var topParents = section.MatchedPattern.TypicalParents.Take(3);
                    _logger.LogDebug("  - Typical parents: {Parents}",
                        string.Join(", ", topParents.Select(p => $"\"{p.ParentNormalizedTitle}\" ({p.Frequency}x)")));
                }
            }
            else
            {
                _logger.LogDebug("[Pass 1] Line {Line}: \"{Header}\" - No pattern match found",
                    section.LineNumber, header.Text);
            }
        }

        var matchedCount = classified.Count(s => s.MatchedPattern != null);
        _logger.LogInformation("[Pass 1] Classification complete: {Matched}/{Total} headers matched patterns ({Pct:F1}%)",
            matchedCount, headers.Count, (matchedCount * 100.0 / headers.Count));

        return classified;
    }

    /// <summary>
    /// Classifies a single header by matching against pattern database.
    /// </summary>
    private ClassifiedSection ClassifyHeader(HierarchyGeneratorService.HeaderInfo header)
    {
        var normalizedTitle = NormalizeText(header.Text);
        var matches = _patterns.FindMatches(normalizedTitle, minConfidence: 0.01);

        return new ClassifiedSection
        {
            HeaderText = header.Text,
            NormalizedTitle = normalizedTitle,
            LineNumber = 0, // Will be set by caller if needed
            DataNumber = header.DataNumber,
            HeaderLevel = header.Level, // Store level string (e.g., "h1", "h2")
            HeaderInfo = header,
            MatchedPattern = matches.FirstOrDefault(),
            MatchConfidence = matches.FirstOrDefault()?.Confidence ?? 0.0,
            AlternativeMatches = matches.Skip(1).ToList()
        };
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
/// Represents a header that has been classified by Pass 1
/// </summary>
public class ClassifiedSection
{
    /// <summary>
    /// Original header text
    /// </summary>
    public string HeaderText { get; set; } = string.Empty;

    /// <summary>
    /// Normalized title for pattern matching
    /// </summary>
    public string NormalizedTitle { get; set; } = string.Empty;

    /// <summary>
    /// Line number in document (for logging)
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// data-number attribute if present
    /// </summary>
    public string? DataNumber { get; set; }

    /// <summary>
    /// Header level string (e.g., "h1", "h2", "h3")
    /// </summary>
    public string HeaderLevel { get; set; } = string.Empty;

    /// <summary>
    /// Original HeaderInfo from HierarchyGeneratorService
    /// </summary>
    public HierarchyGeneratorService.HeaderInfo HeaderInfo { get; set; } = null!;

    /// <summary>
    /// Best matching pattern from database
    /// </summary>
    public SectionPattern? MatchedPattern { get; set; }

    /// <summary>
    /// Confidence score for the match (0.0-1.0)
    /// </summary>
    public double MatchConfidence { get; set; }

    /// <summary>
    /// Alternative pattern matches (lower confidence)
    /// </summary>
    public List<SectionPattern> AlternativeMatches { get; set; } = new();

    /// <summary>
    /// Level determined by Pass 2 (set after classification)
    /// </summary>
    public int DeterminedLevel { get; set; }

    /// <summary>
    /// Which signal was used to determine the level (for transparency)
    /// </summary>
    public string? DeterminationSignal { get; set; }

    /// <summary>
    /// Reasoning for level determination
    /// </summary>
    public string? DeterminationReasoning { get; set; }
}

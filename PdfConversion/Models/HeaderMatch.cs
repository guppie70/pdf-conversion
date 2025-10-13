using System.Xml.Linq;

namespace PdfConversion.Models;

/// <summary>
/// Represents a match (or attempted match) between a hierarchy item and a header in transformed XHTML
/// </summary>
public class HeaderMatch
{
    /// <summary>
    /// The hierarchy item being matched
    /// </summary>
    public HierarchyItem HierarchyItem { get; set; } = null!;

    /// <summary>
    /// The header element that was matched (null if no match found)
    /// </summary>
    public XElement? MatchedHeader { get; set; }

    /// <summary>
    /// The text content of the matched header (null if no match found)
    /// </summary>
    public string? MatchedText { get; set; }

    /// <summary>
    /// Whether this is an exact text match
    /// </summary>
    public bool IsExactMatch { get; set; }

    /// <summary>
    /// Confidence score of the match (1.0 = exact match, 0.0 = no match)
    /// Fuzzy matches will have scores between 0.0 and 1.0
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// Whether this match is part of a duplicate group (multiple headers matched same hierarchy item)
    /// </summary>
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// Total number of matches for this hierarchy item (if duplicate)
    /// </summary>
    public int DuplicateCount { get; set; }

    /// <summary>
    /// Position in duplicate group (0-based index)
    /// </summary>
    public int DuplicateIndex { get; set; }

    /// <summary>
    /// Returns a formatted string for logging and debugging
    /// </summary>
    public override string ToString()
    {
        var matchStatus = IsExactMatch
            ? "Exact Match"
            : MatchedHeader != null
                ? $"Match ({ConfidenceScore:P0})"
                : "No Match";

        var duplicateInfo = IsDuplicate ? $" [Duplicate {DuplicateIndex + 1}/{DuplicateCount}]" : "";

        return $"{HierarchyItem.LinkName} -> {matchStatus}" +
               (MatchedText != null ? $" ({MatchedText})" : "") +
               duplicateInfo;
    }
}

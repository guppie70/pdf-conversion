namespace PdfConversion.Models;

/// <summary>
/// Represents a header element extracted from the Normalized XML document.
/// Used by the hierarchy editor to display available headers for drag-and-drop.
/// </summary>
public class DocumentHeader
{
    /// <summary>
    /// Unique identifier for this header (e.g., "h1_1", "h2_3")
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Header level (h1, h2, h3, h4, h5, h6)
    /// </summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the header
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// XPath expression to locate this header in the XML
    /// </summary>
    public string XPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this header is already used in the hierarchy tree
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Surrounding context (previous/next siblings) for preview tooltip
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// Sequential order in the document (1, 2, 3...). Immutable, preserves document order.
    /// Used by Manual Mode to ensure headers never reorder.
    /// </summary>
    public int OriginalOrder { get; set; }

    /// <summary>
    /// User-adjustable indentation level for Manual Mode (0, 1, 2, 3...).
    /// Level 0 = flat list, Level 1 = child of previous level 0, etc.
    /// This is separate from the header's original level (h1-h6).
    /// </summary>
    public int IndentLevel { get; set; }

    /// <summary>
    /// Section number extracted from data-number attribute (e.g., "1.2.3").
    /// May be null if header doesn't have numbering.
    /// </summary>
    public string? DataNumber { get; set; }

    /// <summary>
    /// Whether this header is excluded from the hierarchy (Manual Mode).
    /// Excluded headers become in-section headers instead of section boundaries.
    /// </summary>
    public bool IsExcluded { get; set; }

    /// <summary>
    /// Numeric level for sorting (1-6)
    /// </summary>
    public int NumericLevel => Level.ToLower() switch
    {
        "h1" => 1,
        "h2" => 2,
        "h3" => 3,
        "h4" => 4,
        "h5" => 5,
        "h6" => 6,
        _ => 0
    };
}

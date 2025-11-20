namespace PdfConversion.Models;

/// <summary>
/// Flags indicating why a hierarchy decision has uncertain confidence.
/// Used by Rule-Based Hierarchy Generator to identify structural issues.
/// </summary>
public enum UncertaintyFlag
{
    /// <summary>Header lacks data-number attribute</summary>
    NoDataNumber,

    /// <summary>data-number doesn't match expected patterns (e.g., unusual format)</summary>
    UnusualNumbering,

    /// <summary>Content >5000 words (might need splitting into multiple sections)</summary>
    LongContent,

    /// <summary>Content <100 words (might need merging or be a stub)</summary>
    ShortContent,

    /// <summary>More than 4 levels deep in hierarchy</summary>
    DeepNesting,

    /// <summary>Multiple possible parent sections (ambiguous placement)</summary>
    UnclearParent,

    /// <summary>No siblings at same level (isolated header)</summary>
    IsolatedHeader,

    /// <summary>Gap in numbered sequence (e.g., 1, 2, 4 - missing 3)</summary>
    MissingSequence,

    /// <summary>Pattern data suggests different level than assigned</summary>
    LevelMismatch
}

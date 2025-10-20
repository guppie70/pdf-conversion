namespace PdfConversion.Models;

/// <summary>
/// Result of XHTML validation against valid HTML elements
/// </summary>
public class XhtmlValidationResult
{
    /// <summary>
    /// Whether the XHTML is valid (no invalid elements or uppercase in element names)
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation issues found
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// Total number of validation issues
    /// </summary>
    public int TotalIssues => Issues.Count;

    /// <summary>
    /// Total number of invalid element occurrences across all issues
    /// </summary>
    public int TotalOccurrences => Issues.Sum(i => i.OccurrenceCount);

    /// <summary>
    /// Creates a successful validation result (no issues)
    /// </summary>
    public static XhtmlValidationResult Success() => new XhtmlValidationResult { IsValid = true };

    /// <summary>
    /// Creates a validation result with issues
    /// </summary>
    public static XhtmlValidationResult WithIssues(List<ValidationIssue> issues)
    {
        return new XhtmlValidationResult
        {
            IsValid = false,
            Issues = issues
        };
    }
}

/// <summary>
/// Represents a single validation issue for an element
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// Type of validation issue
    /// </summary>
    public ValidationIssueType Type { get; set; }

    /// <summary>
    /// Name of the element with issues
    /// </summary>
    public string ElementName { get; set; } = "";

    /// <summary>
    /// Number of times this issue occurs in the document
    /// </summary>
    public int OccurrenceCount { get; set; }

    /// <summary>
    /// XPath locations of first 5 occurrences (for navigation)
    /// </summary>
    public List<string> XPaths { get; set; } = new();

    /// <summary>
    /// Human-readable description of the issue
    /// </summary>
    public string Description => Type switch
    {
        ValidationIssueType.InvalidElement => $"'{ElementName}' is not a valid HTML element",
        ValidationIssueType.UppercaseInElementName => $"'{ElementName}' contains uppercase characters (HTML elements must be lowercase)",
        _ => $"Unknown issue with '{ElementName}'"
    };
}

/// <summary>
/// Types of validation issues
/// </summary>
public enum ValidationIssueType
{
    /// <summary>
    /// Element name is not in the list of valid HTML elements
    /// </summary>
    InvalidElement,

    /// <summary>
    /// Element name contains uppercase characters
    /// </summary>
    UppercaseInElementName
}

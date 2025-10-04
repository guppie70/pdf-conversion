namespace PdfConversion.Models;

/// <summary>
/// Result of XSLT validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the XSLT is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the error occurred (if applicable)
    /// </summary>
    public int? ErrorLineNumber { get; set; }

    /// <summary>
    /// Column number where the error occurred (if applicable)
    /// </summary>
    public int? ErrorColumnNumber { get; set; }

    /// <summary>
    /// List of warning messages
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ValidationResult Success() => new ValidationResult { IsValid = true };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    public static ValidationResult Failure(string errorMessage, int? lineNumber = null, int? columnNumber = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            ErrorLineNumber = lineNumber,
            ErrorColumnNumber = columnNumber
        };
    }
}

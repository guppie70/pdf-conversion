namespace PdfConversion.Models;

/// <summary>
/// Configuration settings for the conversion process
/// </summary>
public class ConversionSettings
{
    /// <summary>
    /// List of special section files that should be excluded from template usage tracking
    /// during document reconstruction (e.g., "front-cover.xml", "toc.xml", "back-cover.xml")
    /// </summary>
    public List<string> SpecialSectionFiles { get; set; } = new();

    /// <summary>
    /// When true, appends a timestamp postfix to all hierarchy item IDs and section filenames
    /// to prevent collisions when importing into existing Taxxor DM projects.
    /// Default: false (backward compatible)
    /// </summary>
    public bool IdPostfixEnabled { get; set; } = false;

    /// <summary>
    /// Timestamp format string for the ID postfix when IdPostfixEnabled is true.
    /// Example: "yyyyMMdd-HHmmss" produces "20250107-143025"
    /// Default: "yyyyMMdd-HHmmss"
    /// </summary>
    public string IdPostfixFormat { get; set; } = "yyyyMMdd-HHmmss";
}

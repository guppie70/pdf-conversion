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
}

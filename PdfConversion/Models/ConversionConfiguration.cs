namespace PdfConversion.Models;

/// <summary>
/// Configuration settings for the PDF to Taxxor section conversion process
/// </summary>
public class ConversionConfiguration
{
    /// <summary>
    /// Selected project ID (e.g., "ar24-3")
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Source XML file from the project directory
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Hierarchy XML file from the project's metadata directory
    /// </summary>
    public string? HierarchyFile { get; set; }

    /// <summary>
    /// Validates that all required configuration fields are populated
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(ProjectId)
                        && !string.IsNullOrEmpty(SourceFile)
                        && !string.IsNullOrEmpty(HierarchyFile);
}

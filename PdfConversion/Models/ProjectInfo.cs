namespace PdfConversion.Models;

/// <summary>
/// Represents a discovered project with its metadata and display information
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// Customer name (e.g., "optiver")
    /// </summary>
    public string Customer { get; set; } = string.Empty;

    /// <summary>
    /// Project identifier (e.g., "ar24-3")
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Custom label configured by user (null if not set)
    /// </summary>
    public string? CustomLabel { get; set; }

    /// <summary>
    /// Display string in format: "{CustomLabelOrFallback} ({ProjectId})"
    /// </summary>
    public string DisplayString { get; set; } = string.Empty;

    /// <summary>
    /// Full filesystem path to the project folder
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;
}

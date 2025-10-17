namespace PdfConversion.Models;

/// <summary>
/// Represents a PDF conversion project
/// </summary>
public class Project
{
    /// <summary>
    /// Unique identifier for the project (e.g., "ar24-1", "ar24-2", "ar24-3")
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Organization name (e.g., "optiver", "antea-group")
    /// </summary>
    public string Organization { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the project
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to the input directory containing source XML files
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the output directory for transformed XHTML files
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Current processing status of the project
    /// </summary>
    public ProjectStatus Status { get; set; } = ProjectStatus.NotStarted;

    /// <summary>
    /// Number of files in the project
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// List of files in the project (cached for UI display)
    /// </summary>
    public List<string> Files { get; set; } = new List<string>();

    /// <summary>
    /// Date when the project was created
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when the project was last processed (null if never processed)
    /// </summary>
    public DateTime? LastProcessedDate { get; set; }
}

/// <summary>
/// Status of a project
/// </summary>
public enum ProjectStatus
{
    NotStarted,
    InProgress,
    Completed,
    Error
}

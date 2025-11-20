namespace PdfConversion.Models;

/// <summary>
/// Represents a group of files within a project for dropdown display
/// </summary>
public class ProjectFileGroup
{
    /// <summary>
    /// Customer/organization name (e.g., "optiver", "test")
    /// </summary>
    public string Customer { get; set; } = string.Empty;

    /// <summary>
    /// Project identifier (e.g., "ar24-3", "test-pdf")
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the project (e.g., "AR 2024 Report 3 (ar24-3)")
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// List of files within this project
    /// </summary>
    public List<ProjectFile> Files { get; set; } = new();
}

/// <summary>
/// Represents a single file within a project group
/// </summary>
public class ProjectFile
{
    /// <summary>
    /// File name (e.g., "input.xml", "docling-output.html")
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the file (e.g., "optiver/ar24-3/input.xml" or "/app/data/input/...")
    /// </summary>
    public string FullPath { get; set; } = string.Empty;
}

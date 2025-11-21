namespace PdfConversion.Models;

/// <summary>
/// JSON structure for storing project metadata
/// Stored in data/project-metadata.json
/// </summary>
public class ProjectLabelsData
{
    /// <summary>
    /// Nested dictionary: customer -> projectId -> metadata
    /// Example: { "optiver": { "ar24-3": { "label": "...", "status": "Ready" } } }
    /// </summary>
    public Dictionary<string, Dictionary<string, ProjectMetadata>> Projects { get; set; } = new();

    /// <summary>
    /// Timestamp of last modification (UTC)
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

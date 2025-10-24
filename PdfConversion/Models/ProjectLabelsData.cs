namespace PdfConversion.Models;

/// <summary>
/// JSON structure for storing project custom labels
/// Stored in data/project-labels.json
/// </summary>
public class ProjectLabelsData
{
    /// <summary>
    /// Nested dictionary: customer -> projectId -> label
    /// Example: { "optiver": { "ar24-3": "Q3 Financial Report" } }
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Labels { get; set; } = new();

    /// <summary>
    /// Timestamp of last modification (UTC)
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

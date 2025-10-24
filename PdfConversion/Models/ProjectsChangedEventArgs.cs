namespace PdfConversion.Models;

/// <summary>
/// Event args for project directory changes
/// </summary>
public class ProjectsChangedEventArgs : EventArgs
{
    /// <summary>
    /// List of all discovered projects after the change
    /// </summary>
    public List<ProjectInfo> Projects { get; set; } = new();

    /// <summary>
    /// Timestamp when the change was detected (UTC)
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

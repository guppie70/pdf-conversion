namespace PdfConversion.Services;

/// <summary>
/// Service for watching XML files for external changes and notifying subscribers
/// </summary>
public interface IXmlFileWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when the XML file is changed externally
    /// </summary>
    event EventHandler<XmlFileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Start watching the specified XML file
    /// </summary>
    /// <param name="filePath">Path to the XML file to watch</param>
    void StartWatching(string filePath);

    /// <summary>
    /// Stop watching the XML file
    /// </summary>
    void StopWatching();

    /// <summary>
    /// Get the current file content
    /// </summary>
    Task<string> GetFileContentAsync();

    /// <summary>
    /// Register a callback for file changes (replaces any existing callback)
    /// This prevents multiple subscriptions from prerendering
    /// </summary>
    void SetFileChangedCallback(EventHandler<XmlFileChangedEventArgs>? callback);

    /// <summary>
    /// Update the known content hash (call after application saves file to prevent reload loop)
    /// </summary>
    void UpdateContentHash(string content);
}

/// <summary>
/// Event args for XML file change events
/// </summary>
public class XmlFileChangedEventArgs : EventArgs
{
    /// <summary>
    /// Path to the changed file
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// New content of the file
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the change
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

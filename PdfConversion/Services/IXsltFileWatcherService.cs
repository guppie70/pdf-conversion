namespace PdfConversion.Services;

/// <summary>
/// Service for watching XSLT files for external changes and notifying subscribers
/// </summary>
public interface IXsltFileWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when the XSLT file is changed externally
    /// </summary>
    event EventHandler<XsltFileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Start watching the specified XSLT file
    /// </summary>
    /// <param name="filePath">Path to the XSLT file to watch</param>
    void StartWatching(string filePath);

    /// <summary>
    /// Stop watching the XSLT file
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
    void SetFileChangedCallback(EventHandler<XsltFileChangedEventArgs>? callback);
}

/// <summary>
/// Event args for XSLT file change events
/// </summary>
public class XsltFileChangedEventArgs : EventArgs
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

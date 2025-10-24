using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for watching project directories for changes (creation/deletion)
/// </summary>
public interface IProjectDirectoryWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when project directories are created or deleted
    /// </summary>
    event EventHandler<ProjectsChangedEventArgs>? ProjectsChanged;

    /// <summary>
    /// Start watching for project directory changes
    /// </summary>
    /// <param name="basePath">Base path to watch (default: /app/data/input)</param>
    void StartWatching(string basePath = "/app/data/input");

    /// <summary>
    /// Stop watching for project directory changes
    /// </summary>
    void StopWatching();
}

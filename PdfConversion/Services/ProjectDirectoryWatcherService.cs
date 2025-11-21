using System.Timers;
using PdfConversion.Models;
using Timer = System.Timers.Timer;

namespace PdfConversion.Services;

/// <summary>
/// Watches project directories for changes (creation/deletion) and notifies subscribers.
/// Monitors data/input/*/projects/ directories for new or removed project folders.
/// </summary>
public class ProjectDirectoryWatcherService : IProjectDirectoryWatcherService
{
    private readonly ILogger<ProjectDirectoryWatcherService> _logger;
    private readonly IProjectLabelService _projectLabelService;
    private readonly List<FileSystemWatcher> _watchers = new();
    private Timer? _debounceTimer;
    private string? _currentBasePath;
    private bool _disposed;

    // Debounce settings (500ms to match other file watchers)
    private const int DebounceDelayMs = 500;

    public event EventHandler<ProjectsChangedEventArgs>? ProjectsChanged;

    public ProjectDirectoryWatcherService(
        ILogger<ProjectDirectoryWatcherService> logger,
        IProjectLabelService projectLabelService)
    {
        _logger = logger;
        _projectLabelService = projectLabelService;
    }

    public void StartWatching(string basePath = "/app/data/input")
    {
        if (string.IsNullOrEmpty(basePath))
        {
            throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));
        }

        if (!Directory.Exists(basePath))
        {
            _logger.LogWarning("Base path does not exist, cannot start watching: {BasePath}", basePath);
            return;
        }

        // Stop existing watchers if any
        StopWatching();

        _currentBasePath = basePath;

        try
        {
            // Discover all customer directories
            var customerDirs = Directory.GetDirectories(basePath);

            foreach (var customerDir in customerDirs)
            {
                var customer = Path.GetFileName(customerDir);
                var projectsPath = Path.Combine(customerDir, "projects");

                // Create projects directory if it doesn't exist
                if (!Directory.Exists(projectsPath))
                {
                    _logger.LogDebug("Projects directory does not exist for customer {Customer}, skipping: {Path}",
                        customer, projectsPath);
                    continue;
                }

                // Create watcher for this customer's projects directory
                var watcher = new FileSystemWatcher(projectsPath)
                {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                // Subscribe to events
                watcher.Created += OnDirectoryCreated;
                watcher.Deleted += OnDirectoryDeleted;

                _watchers.Add(watcher);

                _logger.LogInformation("Started watching project directories for customer: {Customer}", customer);
            }

            if (_watchers.Count == 0)
            {
                _logger.LogWarning("No customer project directories found to watch in {BasePath}", basePath);
            }
            else
            {
                _logger.LogInformation("Started watching {Count} customer project directories", _watchers.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start directory watchers for {BasePath}", basePath);
            throw;
        }
    }

    public void StopWatching()
    {
        if (_watchers.Count > 0)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnDirectoryCreated;
                watcher.Deleted -= OnDirectoryDeleted;
                watcher.Dispose();
            }

            _watchers.Clear();

            _logger.LogInformation("Stopped watching project directories");
        }

        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    private void OnDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Project directory created: {Path}", e.FullPath);
        DebouncedProjectsChange();
    }

    private void OnDirectoryDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Project directory deleted: {Path}", e.FullPath);
        DebouncedProjectsChange();
    }

    private void DebouncedProjectsChange()
    {
        // Reset debounce timer on each event
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();

        _debounceTimer = new Timer(DebounceDelayMs)
        {
            AutoReset = false
        };

        _debounceTimer.Elapsed += async (sender, e) =>
        {
            await NotifyProjectsChangedAsync();
        };

        _debounceTimer.Start();
    }

    private async Task NotifyProjectsChangedAsync()
    {
        if (string.IsNullOrEmpty(_currentBasePath))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Project directories changed, rescanning: {BasePath}", _currentBasePath);

            // Wait a bit to ensure directories are fully created/deleted
            await Task.Delay(100);

            // Get updated list of projects
            var projects = await _projectLabelService.GetAllProjectsAsync(_currentBasePath);

            var eventArgs = new ProjectsChangedEventArgs
            {
                Projects = projects,
                Timestamp = DateTime.UtcNow
            };

            ProjectsChanged?.Invoke(this, eventArgs);

            _logger.LogInformation("Notified subscribers of project changes: {Count} projects found",
                projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying project changes for {BasePath}", _currentBasePath);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopWatching();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

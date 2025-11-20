using System.Timers;
using Timer = System.Timers.Timer;

namespace PdfConversion.Services;

/// <summary>
/// Watches XSLT files for external changes and notifies subscribers.
/// Handles editor save patterns (write-rename-delete) and debounces rapid changes.
/// </summary>
public class XsltFileWatcherService : IXsltFileWatcherService
{
    private readonly ILogger<XsltFileWatcherService> _logger;
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private string? _currentFilePath;
    private bool _disposed;
    private EventHandler<XsltFileChangedEventArgs>? _callback;

    // Debounce settings
    private const int DebounceDelayMs = 500;

    public event EventHandler<XsltFileChangedEventArgs>? FileChanged;

    /// <summary>
    /// Set a single callback for file changes (prevents double subscription from prerendering)
    /// </summary>
    public void SetFileChangedCallback(EventHandler<XsltFileChangedEventArgs>? callback)
    {
        _callback = callback;
        _logger.LogDebug("File changed callback {Action}", callback == null ? "cleared" : "registered");
    }

    public XsltFileWatcherService(ILogger<XsltFileWatcherService> logger)
    {
        _logger = logger;
    }

    public void StartWatching(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("XSLT file not found at {Path}, cannot start watching", filePath);
            return;
        }

        // Stop existing watcher if any
        StopWatching();

        _currentFilePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory))
        {
            _logger.LogError("Could not determine directory for file {Path}", filePath);
            return;
        }

        try
        {
            _watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            // Subscribe to events
            _watcher.Changed += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Created += OnFileCreated;

            _logger.LogInformation("Started watching XSLT file: {Path}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file watcher for {Path}", filePath);
            throw;
        }
    }

    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Created -= OnFileCreated;
            _watcher.Dispose();
            _watcher = null;

            _logger.LogInformation("Stopped watching XSLT file: {Path}", _currentFilePath);
        }

        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public async Task<string> GetFileContentAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            throw new InvalidOperationException("No file is being watched");
        }

        if (!File.Exists(_currentFilePath))
        {
            throw new FileNotFoundException("XSLT file not found", _currentFilePath);
        }

        // Retry logic to handle file locks
        for (int i = 0; i < 3; i++)
        {
            try
            {
                return await File.ReadAllTextAsync(_currentFilePath);
            }
            catch (IOException) when (i < 2)
            {
                // File might be locked by editor, wait and retry
                await Task.Delay(100);
            }
        }

        // Final attempt without retry
        return await File.ReadAllTextAsync(_currentFilePath);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File change detected: {ChangeType} - {Path}", e.ChangeType, e.FullPath);
        DebouncedFileChange();
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("File renamed from {OldPath} to {NewPath}", e.OldFullPath, e.FullPath);
        // Editors like VS Code often save by renaming
        DebouncedFileChange();
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("File created: {Path}", e.FullPath);
        // Some editors create new file on save
        DebouncedFileChange();
    }

    private void DebouncedFileChange()
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
            await NotifyFileChangedAsync();
        };

        _debounceTimer.Start();
    }

    private async Task NotifyFileChangedAsync()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            return;
        }

        try
        {
            _logger.LogInformation("XSLT file changed externally, reloading: {Path}", _currentFilePath);

            // Wait a bit to ensure file is fully written
            await Task.Delay(100);

            var content = await GetFileContentAsync();

            var eventArgs = new XsltFileChangedEventArgs
            {
                FilePath = _currentFilePath,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            // Invoke callback if set (preferred - prevents double subscription)
            if (_callback != null)
            {
                _callback.Invoke(this, eventArgs);
            }
            else
            {
                // Fallback to event for backward compatibility
                FileChanged?.Invoke(this, eventArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying file change for {Path}", _currentFilePath);
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

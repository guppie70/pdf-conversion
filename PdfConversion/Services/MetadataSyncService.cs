using Microsoft.Extensions.Hosting;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Background service that auto-syncs project-metadata.json with filesystem structure.
/// Preserves existing metadata, only adds new projects with defaults.
/// </summary>
public class MetadataSyncService : BackgroundService, IMetadataSyncService
{
    private readonly IProjectDirectoryWatcherService _directoryWatcher;
    private readonly IProjectLabelService _projectLabelService;
    private readonly ILogger<MetadataSyncService> _logger;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _disposed;

    public MetadataSyncService(
        IProjectDirectoryWatcherService directoryWatcher,
        IProjectLabelService projectLabelService,
        ILogger<MetadataSyncService> logger)
    {
        _directoryWatcher = directoryWatcher;
        _projectLabelService = projectLabelService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetadataSyncService starting");

        // Subscribe to filesystem changes
        _directoryWatcher.ProjectsChanged += OnProjectsChanged;

        // Start watching directories
        _directoryWatcher.StartWatching();

        // Initial sync on startup
        await SyncMetadataAsync();

        // Keep service running
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("MetadataSyncService stopping");
        }
    }

    private async void OnProjectsChanged(object? sender, ProjectsChangedEventArgs e)
    {
        _logger.LogInformation("Filesystem changed, resyncing metadata");
        await SyncMetadataAsync();
    }

    public async Task SyncMetadataAsync()
    {
        // Prevent concurrent syncs
        if (!await _syncLock.WaitAsync(0))
        {
            _logger.LogDebug("Sync already in progress, skipping");
            return;
        }

        try
        {
            _logger.LogDebug("Starting metadata sync");

            // Get current projects from filesystem
            var currentProjects = await _projectLabelService.GetAllProjectsAsync();

            // Load existing metadata
            var data = await _projectLabelService.GetProjectLabelsDataAsync();

            int addedCount = 0;

            // For each project on filesystem
            foreach (var project in currentProjects)
            {
                // Ensure customer entry exists
                if (!data.Projects.ContainsKey(project.Customer))
                {
                    data.Projects[project.Customer] = new Dictionary<string, ProjectMetadata>();
                }

                // If project missing from metadata, add with defaults
                if (!data.Projects[project.Customer].ContainsKey(project.ProjectId))
                {
                    var now = DateTime.UtcNow;
                    data.Projects[project.Customer][project.ProjectId] = new ProjectMetadata
                    {
                        Label = project.ProjectId, // Default label is projectId
                        Status = ProjectLifecycleStatus.Open,
                        CreatedAt = now,
                        LastModified = now
                    };

                    addedCount++;
                    _logger.LogDebug("Added new project to metadata: {Customer}/{ProjectId}",
                        project.Customer, project.ProjectId);
                }
                // If exists: do nothing (preserve existing label/status)
            }

            // Update global timestamp if we added anything
            if (addedCount > 0)
            {
                data.LastModified = DateTime.UtcNow;
                await _projectLabelService.SaveProjectLabelsDataAsync(data);
            }

            _logger.LogInformation("Synced metadata: {TotalProjects} projects on filesystem, added {AddedCount} new entries",
                currentProjects.Count, addedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing metadata");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public new void Dispose()
    {
        if (_disposed) return;

        _directoryWatcher.ProjectsChanged -= OnProjectsChanged;
        _syncLock.Dispose();
        _disposed = true;

        base.Dispose();
        GC.SuppressFinalize(this);
    }
}

using System.Text.Json;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for persisting and retrieving user's file selections
/// </summary>
public interface IUserSelectionService
{
    /// <summary>
    /// Get the current user selection
    /// </summary>
    Task<UserSelection> GetSelectionAsync();

    /// <summary>
    /// Save user selection
    /// </summary>
    Task SaveSelectionAsync(UserSelection selection);

    /// <summary>
    /// Update specific fields in user selection
    /// </summary>
    Task UpdateSelectionAsync(string? projectId = null, string? sourceXml = null, string? hierarchyXml = null, List<string>? trainingHierarchies = null);

    /// <summary>
    /// Update validation modal position
    /// </summary>
    Task UpdateModalPositionAsync(int? x = null, int? y = null);

    /// <summary>
    /// Save project-scoped selection for a specific project
    /// </summary>
    Task SaveProjectSelectionAsync(
        string projectId,
        string? sourceFile = null,
        string? hierarchyFile = null,
        string? xsltFile = null,
        string? viewMode = null,
        string? hierarchyMode = null);

    /// <summary>
    /// Get project-scoped selection for a specific project
    /// </summary>
    Task<ProjectSelection?> GetProjectSelectionAsync(string projectId);
}

/// <summary>
/// File-based implementation of user selection service using JSON storage
/// </summary>
public class UserSelectionService : IUserSelectionService
{
    private readonly string _selectionFilePath;
    private readonly ILogger<UserSelectionService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public UserSelectionService(ILogger<UserSelectionService> logger)
    {
        _logger = logger;
        _selectionFilePath = "/app/data/user-selections.json";

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_selectionFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public async Task<UserSelection> GetSelectionAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_selectionFilePath))
            {
                _logger.LogInformation("Selection file not found, returning defaults");
                return new UserSelection();
            }

            var json = await File.ReadAllTextAsync(_selectionFilePath).ConfigureAwait(false);
            var selection = JsonSerializer.Deserialize<UserSelection>(json, _jsonOptions);

            if (selection == null)
            {
                return new UserSelection();
            }

            // Auto-migrate from old format to new format
            await MigrateIfNeededAsync(selection).ConfigureAwait(false);

            _logger.LogDebug("Loaded selection: Project={Project}, SourceXml={SourceXml}",
                selection?.LastSelectedProject,
                selection?.LastSelectedSourceXml);

            return selection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading user selection file");
            return new UserSelection();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveSelectionAsync(UserSelection selection)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveSelectionInternalAsync(selection).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Internal save method that doesn't acquire lock - caller must hold lock
    /// </summary>
    private async Task SaveSelectionInternalAsync(UserSelection selection)
    {
        try
        {
            var json = JsonSerializer.Serialize(selection, _jsonOptions);
            await File.WriteAllTextAsync(_selectionFilePath, json).ConfigureAwait(false);

            _logger.LogInformation("Saved selection: Project={Project}, SourceXml={SourceXml}",
                selection.LastSelectedProject,
                selection.LastSelectedSourceXml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user selection file");
        }
    }

    public async Task UpdateSelectionAsync(string? projectId = null, string? sourceXml = null, string? hierarchyXml = null, List<string>? trainingHierarchies = null)
    {
        var current = await GetSelectionAsync().ConfigureAwait(false);

        if (projectId != null) current.LastSelectedProject = projectId;
        if (sourceXml != null) current.LastSelectedSourceXml = sourceXml;
        if (hierarchyXml != null) current.LastSelectedHierarchyXml = hierarchyXml;
        if (trainingHierarchies != null) current.LastSelectedTrainingHierarchies = trainingHierarchies;

        await SaveSelectionAsync(current).ConfigureAwait(false);
    }

    public async Task UpdateModalPositionAsync(int? x = null, int? y = null)
    {
        var current = await GetSelectionAsync().ConfigureAwait(false);

        if (x != null) current.ValidationModalX = x;
        if (y != null) current.ValidationModalY = y;

        await SaveSelectionAsync(current).ConfigureAwait(false);
        _logger.LogInformation("Updated validation modal position: X={X}, Y={Y}", x, y);
    }

    /// <summary>
    /// Save project-scoped selection for a specific project
    /// </summary>
    public async Task SaveProjectSelectionAsync(
        string projectId,
        string? sourceFile = null,
        string? hierarchyFile = null,
        string? xsltFile = null,
        string? viewMode = null,
        string? hierarchyMode = null)
    {
        var selections = await GetSelectionAsync().ConfigureAwait(false);

        if (!selections.Projects.ContainsKey(projectId))
        {
            selections.Projects[projectId] = new ProjectSelection();
        }

        var projectSelection = selections.Projects[projectId];

        // Update only non-null parameters (preserve others)
        if (sourceFile != null)
            projectSelection.SourceFile = sourceFile;

        if (hierarchyFile != null)
            projectSelection.HierarchyFile = hierarchyFile;

        if (xsltFile != null)
            projectSelection.XsltFile = xsltFile;

        if (viewMode != null)
            projectSelection.ViewMode = viewMode;

        if (hierarchyMode != null)
            projectSelection.HierarchyMode = hierarchyMode;

        projectSelection.LastAccessed = DateTime.UtcNow;

        await SaveSelectionAsync(selections).ConfigureAwait(false);

        _logger.LogInformation(
            "Saved project selection: Project={Project}, Source={Source}, Hierarchy={Hierarchy}, Xslt={Xslt}, View={View}, Mode={Mode}",
            projectId, sourceFile, hierarchyFile, xsltFile, viewMode, hierarchyMode);
    }

    /// <summary>
    /// Get project-scoped selection for a specific project
    /// </summary>
    public async Task<ProjectSelection?> GetProjectSelectionAsync(string projectId)
    {
        var selections = await GetSelectionAsync().ConfigureAwait(false);
        return selections.Projects.TryGetValue(projectId, out var projectSelection)
            ? projectSelection
            : null;
    }

    /// <summary>
    /// Migrate from old global selection format to new project-scoped format
    /// </summary>
    private async Task MigrateIfNeededAsync(UserSelection selection)
    {
        // Check if migration is needed:
        // - No project-scoped selections exist yet
        // - Has legacy LastSelectedProject value
        // - Has legacy source or hierarchy values
        if (selection.Projects.Count == 0
            && !string.IsNullOrEmpty(selection.LastSelectedProject)
            && (!string.IsNullOrEmpty(selection.LastSelectedSourceXml) || !string.IsNullOrEmpty(selection.LastSelectedHierarchyXml)))
        {
            _logger.LogInformation("Migrating user-selections.json from legacy format to project-scoped format");

            var projectId = selection.LastSelectedProject;
            selection.Projects[projectId] = new ProjectSelection
            {
                SourceFile = selection.LastSelectedSourceXml,
                HierarchyFile = selection.LastSelectedHierarchyXml,
                LastAccessed = DateTime.UtcNow
            };

            // Clear legacy fields after migration
            selection.LastSelectedSourceXml = string.Empty;
            selection.LastSelectedHierarchyXml = string.Empty;

            // Use internal save since we're called from within GetSelectionAsync which already holds the lock
            await SaveSelectionInternalAsync(selection).ConfigureAwait(false);

            _logger.LogInformation(
                "Migration complete: Moved selections for project '{Project}' to new format",
                projectId);
        }
    }
}

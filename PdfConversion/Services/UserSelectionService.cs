using System.Text.Json;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for persisting and retrieving user preferences.
/// NOTE: Page-specific state (file selections, view modes) is managed via URL + WorkflowStateService.
/// This service only stores cross-session UI preferences (modal positions, docling settings, AI training selections).
/// </summary>
public interface IUserSelectionService
{
    /// <summary>
    /// Get the current user preferences
    /// </summary>
    Task<UserSelection> GetSelectionAsync();

    /// <summary>
    /// Save user preferences
    /// </summary>
    Task SaveSelectionAsync(UserSelection selection);

    /// <summary>
    /// Update AI training hierarchy selections
    /// </summary>
    Task UpdateSelectionAsync(List<string>? trainingHierarchies = null);

    /// <summary>
    /// Update validation modal position
    /// </summary>
    Task UpdateModalPositionAsync(int? x = null, int? y = null);
}

/// <summary>
/// File-based implementation of user preferences service using JSON storage.
/// NOTE: Page-specific state is now managed via URL + WorkflowStateService.
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

            _logger.LogDebug("Loaded user preferences");
            return selection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading user preferences file");
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
            var json = JsonSerializer.Serialize(selection, _jsonOptions);
            await File.WriteAllTextAsync(_selectionFilePath, json).ConfigureAwait(false);
            _logger.LogDebug("Saved user preferences");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user preferences file");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSelectionAsync(List<string>? trainingHierarchies = null)
    {
        var current = await GetSelectionAsync().ConfigureAwait(false);

        if (trainingHierarchies != null)
        {
            current.LastSelectedTrainingHierarchies = trainingHierarchies;
        }

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
}

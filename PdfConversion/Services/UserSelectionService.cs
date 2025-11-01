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
    Task UpdateSelectionAsync(string? projectId = null, string? sourceXml = null, string? xslt = null, string? hierarchyXml = null, List<string>? trainingHierarchies = null);

    /// <summary>
    /// Update validation modal position
    /// </summary>
    Task UpdateModalPositionAsync(int? x = null, int? y = null);
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
        await _lock.WaitAsync();
        try
        {
            if (!File.Exists(_selectionFilePath))
            {
                _logger.LogInformation("Selection file not found, returning defaults");
                return new UserSelection();
            }

            var json = await File.ReadAllTextAsync(_selectionFilePath);
            var selection = JsonSerializer.Deserialize<UserSelection>(json, _jsonOptions);

            _logger.LogDebug("Loaded selection: Project={Project}, SourceXml={SourceXml}, Xslt={Xslt}",
                selection?.LastSelectedProject,
                selection?.LastSelectedSourceXml,
                selection?.LastSelectedXslt);

            return selection ?? new UserSelection();
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
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(selection, _jsonOptions);
            await File.WriteAllTextAsync(_selectionFilePath, json);

            _logger.LogInformation("Saved selection: Project={Project}, SourceXml={SourceXml}, Xslt={Xslt}",
                selection.LastSelectedProject,
                selection.LastSelectedSourceXml,
                selection.LastSelectedXslt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user selection file");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateSelectionAsync(string? projectId = null, string? sourceXml = null, string? xslt = null, string? hierarchyXml = null, List<string>? trainingHierarchies = null)
    {
        var current = await GetSelectionAsync();

        if (projectId != null) current.LastSelectedProject = projectId;
        if (sourceXml != null) current.LastSelectedSourceXml = sourceXml;
        if (xslt != null) current.LastSelectedXslt = xslt;
        if (hierarchyXml != null) current.LastSelectedHierarchyXml = hierarchyXml;
        if (trainingHierarchies != null) current.LastSelectedTrainingHierarchies = trainingHierarchies;

        await SaveSelectionAsync(current);
    }

    public async Task UpdateModalPositionAsync(int? x = null, int? y = null)
    {
        var current = await GetSelectionAsync();

        if (x != null) current.ValidationModalX = x;
        if (y != null) current.ValidationModalY = y;

        await SaveSelectionAsync(current);
        _logger.LogInformation("Updated validation modal position: X={X}, Y={Y}", x, y);
    }
}

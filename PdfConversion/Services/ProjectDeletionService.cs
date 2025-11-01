using System.Text.Json;
using System.Text.Json.Serialization;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for deleting projects and all associated data
/// </summary>
public class ProjectDeletionService : IProjectDeletionService
{
    private readonly ILogger<ProjectDeletionService> _logger;
    private readonly string _metadataFilePath;
    private readonly string _userSelectionsFilePath;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProjectDeletionService(ILogger<ProjectDeletionService> logger)
    {
        _logger = logger;
        _metadataFilePath = "/app/data/project-metadata.json";
        _userSelectionsFilePath = "/app/data/user-selections.json";
        _inputBasePath = "/app/data/input";
        _outputBasePath = "/app/data/output";
    }

    /// <summary>
    /// Root structure for project-metadata.json file
    /// </summary>
    private class ProjectMetadataRoot
    {
        [JsonPropertyName("projects")]
        public Dictionary<string, Dictionary<string, ProjectMetadata>> Projects { get; set; } = new();

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }
    }

    public async Task<ProjectDeletionResult> DeleteProjectAsync(string customer, string projectId)
    {
        _logger.LogInformation("Starting deletion of project: {Customer}/{ProjectId}", customer, projectId);

        var details = new DeletionDetails();

        try
        {
            // Delete input folder
            var inputPath = Path.Combine(_inputBasePath, customer, "projects", projectId);
            var inputResult = await TryDeleteDirectoryAsync(inputPath);
            details.InputFolderDeleted = inputResult.success;
            details.InputFolderError = inputResult.error;

            // Delete output folder
            var outputPath = Path.Combine(_outputBasePath, customer, "projects", projectId);
            var outputResult = await TryDeleteDirectoryAsync(outputPath);
            details.OutputFolderDeleted = outputResult.success;
            details.OutputFolderError = outputResult.error;

            // Remove from project-metadata.json
            await _fileLock.WaitAsync();
            try
            {
                var metadataResult = await TryRemoveFromMetadataAsync(customer, projectId);
                details.MetadataRemoved = metadataResult.success;
                details.MetadataError = metadataResult.error;
            }
            finally
            {
                _fileLock.Release();
            }

            // Remove from user-selections.json
            await _fileLock.WaitAsync();
            try
            {
                var selectionsResult = await TryRemoveFromUserSelectionsAsync(customer, projectId);
                details.UserSelectionsRemoved = selectionsResult.success;
                details.UserSelectionsError = selectionsResult.error;
            }
            finally
            {
                _fileLock.Release();
            }

            // Determine overall result
            var hasAnySuccess = details.InputFolderDeleted || details.OutputFolderDeleted ||
                               details.MetadataRemoved || details.UserSelectionsRemoved;

            var hasAnyFailure = !details.InputFolderDeleted || !details.OutputFolderDeleted ||
                               !details.MetadataRemoved || !details.UserSelectionsRemoved;

            if (!hasAnySuccess)
            {
                _logger.LogError("Complete failure deleting project {Customer}/{ProjectId}", customer, projectId);
                return ProjectDeletionResult.Failure(customer, projectId, "All deletion operations failed");
            }

            if (hasAnyFailure)
            {
                _logger.LogWarning("Partial deletion of project {Customer}/{ProjectId}. Warnings: {Warnings}",
                    customer, projectId, details.GetWarningsSummary());
                return ProjectDeletionResult.PartialSuccess(customer, projectId, details);
            }

            _logger.LogInformation("Successfully deleted project {Customer}/{ProjectId}", customer, projectId);

            // Clean up empty customer directories (best effort)
            await CleanupEmptyCustomerDirectoriesAsync(customer);

            return ProjectDeletionResult.FullSuccess(customer, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting project {Customer}/{ProjectId}", customer, projectId);
            return ProjectDeletionResult.Failure(customer, projectId, ex.Message);
        }
    }

    /// <summary>
    /// Cleans up empty customer directory structures after project deletion.
    /// This is a best-effort operation - errors are logged but don't fail the deletion.
    /// </summary>
    private async Task CleanupEmptyCustomerDirectoriesAsync(string customer)
    {
        await Task.Run(() =>
        {
            try
            {
                // Clean up input directories
                var inputProjectsPath = Path.Combine(_inputBasePath, customer, "projects");
                if (Directory.Exists(inputProjectsPath) && !Directory.EnumerateFileSystemEntries(inputProjectsPath).Any())
                {
                    Directory.Delete(inputProjectsPath);
                    _logger.LogInformation("Deleted empty input projects directory: {Path}", inputProjectsPath);

                    // Check if parent customer directory is now empty
                    var inputCustomerPath = Path.Combine(_inputBasePath, customer);
                    if (Directory.Exists(inputCustomerPath) && !Directory.EnumerateFileSystemEntries(inputCustomerPath).Any())
                    {
                        Directory.Delete(inputCustomerPath);
                        _logger.LogInformation("Deleted empty input customer directory: {Path}", inputCustomerPath);
                    }
                }

                // Clean up output directories
                var outputProjectsPath = Path.Combine(_outputBasePath, customer, "projects");
                if (Directory.Exists(outputProjectsPath) && !Directory.EnumerateFileSystemEntries(outputProjectsPath).Any())
                {
                    Directory.Delete(outputProjectsPath);
                    _logger.LogInformation("Deleted empty output projects directory: {Path}", outputProjectsPath);

                    // Check if parent customer directory is now empty
                    var outputCustomerPath = Path.Combine(_outputBasePath, customer);
                    if (Directory.Exists(outputCustomerPath) && !Directory.EnumerateFileSystemEntries(outputCustomerPath).Any())
                    {
                        Directory.Delete(outputCustomerPath);
                        _logger.LogInformation("Deleted empty output customer directory: {Path}", outputCustomerPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - this is best-effort cleanup
                _logger.LogWarning(ex, "Failed to clean up empty customer directories for {Customer}", customer);
            }
        });
    }

    private async Task<(bool success, string? error)> TryDeleteDirectoryAsync(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                await Task.Run(() => Directory.Delete(path, recursive: true));
                _logger.LogInformation("Deleted directory: {Path}", path);
                return (true, null);
            }
            else
            {
                // Not an error if folder doesn't exist
                _logger.LogDebug("Directory does not exist (already deleted?): {Path}", path);
                return (true, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete directory: {Path}", path);
            return (false, ex.Message);
        }
    }

    private async Task<(bool success, string? error)> TryRemoveFromMetadataAsync(string customer, string projectId)
    {
        try
        {
            if (!File.Exists(_metadataFilePath))
            {
                // No metadata file = nothing to remove
                return (true, null);
            }

            var json = await File.ReadAllTextAsync(_metadataFilePath);
            var root = JsonSerializer.Deserialize<ProjectMetadataRoot>(json, _jsonOptions);

            if (root?.Projects == null)
            {
                return (true, null);
            }

            // Remove the project from metadata
            if (root.Projects.TryGetValue(customer, out var projects))
            {
                if (projects.Remove(projectId))
                {
                    // If customer has no more projects, remove customer entry
                    if (projects.Count == 0)
                    {
                        root.Projects.Remove(customer);
                    }

                    // Update lastModified timestamp and write back to file
                    root.LastModified = DateTime.UtcNow;
                    var updatedJson = JsonSerializer.Serialize(root, _jsonOptions);
                    await File.WriteAllTextAsync(_metadataFilePath, updatedJson);

                    _logger.LogInformation("Removed project {Customer}/{ProjectId} from metadata", customer, projectId);
                    return (true, null);
                }
            }

            // Project was not in metadata (not an error)
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove project {Customer}/{ProjectId} from metadata", customer, projectId);
            return (false, ex.Message);
        }
    }

    private async Task<(bool success, string? error)> TryRemoveFromUserSelectionsAsync(string customer, string projectId)
    {
        try
        {
            if (!File.Exists(_userSelectionsFilePath))
            {
                // No selections file = nothing to remove
                return (true, null);
            }

            var json = await File.ReadAllTextAsync(_userSelectionsFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
            {
                return (true, null);
            }

            // Check all keys for matching project paths
            var keysToRemove = new List<string>();
            var projectPath = $"{customer}/projects/{projectId}";

            foreach (var key in data.Keys)
            {
                var value = data[key]?.ToString() ?? string.Empty;
                if (value.Contains(projectPath))
                {
                    keysToRemove.Add(key);
                }
            }

            // Remove matching keys
            if (keysToRemove.Any())
            {
                foreach (var key in keysToRemove)
                {
                    data.Remove(key);
                }

                // Write back to file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var updatedJson = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(_userSelectionsFilePath, updatedJson);

                _logger.LogInformation("Removed {Count} user selection entries for {Customer}/{ProjectId}",
                    keysToRemove.Count, customer, projectId);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove project {Customer}/{ProjectId} from user selections", customer, projectId);
            return (false, ex.Message);
        }
    }
}

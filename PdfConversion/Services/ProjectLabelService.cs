using System.Text.Json;
using System.Text.RegularExpressions;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for managing custom project labels with JSON persistence
/// </summary>
public class ProjectLabelService : IProjectLabelService
{
    private readonly ILogger<ProjectLabelService> _logger;
    private readonly string _labelsFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Regex pattern for "ar24-3" style project IDs
    private static readonly Regex ArPatternRegex = new(@"^ar(\d{2})-(\d+)$", RegexOptions.Compiled);

    public ProjectLabelService(ILogger<ProjectLabelService> logger)
    {
        _logger = logger;
        _labelsFilePath = "/app/data/project-metadata.json";
    }

    public async Task<List<ProjectInfo>> GetAllProjectsAsync(string basePath = "/app/data/input")
    {
        var projects = new List<ProjectInfo>();

        try
        {
            // Check if base path exists
            if (!Directory.Exists(basePath))
            {
                _logger.LogWarning("Base path does not exist: {BasePath}", basePath);
                return projects;
            }

            // Load labels from JSON
            var labelsData = await LoadLabelsAsync();

            // Enumerate all customer folders
            var customerDirs = Directory.GetDirectories(basePath);

            foreach (var customerDir in customerDirs)
            {
                var customer = Path.GetFileName(customerDir);
                var projectsPath = Path.Combine(customerDir, "projects");

                // Check if customer has a projects subfolder
                if (!Directory.Exists(projectsPath))
                {
                    continue;
                }

                // Enumerate all project folders
                var projectDirs = Directory.GetDirectories(projectsPath);

                foreach (var projectDir in projectDirs)
                {
                    var projectId = Path.GetFileName(projectDir);

                    // Get custom label or null
                    var customLabel = GetLabelFromData(labelsData, customer, projectId);

                    // Generate display string
                    var displayString = GenerateDisplayString(projectId, customLabel);

                    _logger.LogDebug("Project discovery: {Customer}/{ProjectId} - CustomLabel: '{CustomLabel}', DisplayString: '{DisplayString}'",
                        customer, projectId, customLabel ?? "null", displayString);

                    projects.Add(new ProjectInfo
                    {
                        Customer = customer,
                        ProjectId = projectId,
                        CustomLabel = customLabel,
                        DisplayString = displayString,
                        FolderPath = projectDir
                    });
                }
            }

            _logger.LogInformation("Discovered {Count} projects across {CustomerCount} customers",
                projects.Count,
                projects.Select(p => p.Customer).Distinct().Count());

            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering projects from {BasePath}", basePath);
            throw;
        }
    }

    public async Task<string?> GetProjectLabelAsync(string customer, string projectId)
    {
        try
        {
            var labelsData = await LoadLabelsAsync();
            return GetLabelFromData(labelsData, customer, projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting label for {Customer}/{ProjectId}", customer, projectId);
            throw;
        }
    }

    public async Task SetProjectLabelAsync(string customer, string projectId, string label)
    {
        await _fileLock.WaitAsync();
        try
        {
            _logger.LogInformation("SetProjectLabelAsync called: {Customer}/{ProjectId} = '{Label}'",
                customer, projectId, label);

            // Load current data
            var labelsData = await LoadLabelsAsync();

            // Ensure customer dictionary exists
            if (!labelsData.Projects.ContainsKey(customer))
            {
                labelsData.Projects[customer] = new Dictionary<string, ProjectMetadata>();
                _logger.LogDebug("Created new customer entry for: {Customer}", customer);
            }

            // Set or update label
            if (!labelsData.Projects[customer].ContainsKey(projectId))
            {
                labelsData.Projects[customer][projectId] = new ProjectMetadata();
            }
            labelsData.Projects[customer][projectId].Label = label;
            labelsData.Projects[customer][projectId].LastModified = DateTime.UtcNow;
            labelsData.LastModified = DateTime.UtcNow;

            // Save to file
            await SaveLabelsAsync(labelsData);

            _logger.LogInformation("Successfully saved label to {Path}: {Customer}/{ProjectId} = '{Label}'",
                _labelsFilePath, customer, projectId, label);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting label for {Customer}/{ProjectId}", customer, projectId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task DeleteProjectLabelAsync(string customer, string projectId)
    {
        await _fileLock.WaitAsync();
        try
        {
            // Load current data
            var labelsData = await LoadLabelsAsync();

            // Check if customer exists
            if (labelsData.Projects.ContainsKey(customer))
            {
                // Remove label
                if (labelsData.Projects[customer].Remove(projectId))
                {
                    labelsData.LastModified = DateTime.UtcNow;
                    await SaveLabelsAsync(labelsData);

                    _logger.LogInformation("Deleted label for {Customer}/{ProjectId}", customer, projectId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting label for {Customer}/{ProjectId}", customer, projectId);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<string> GetDisplayStringAsync(string customer, string projectId)
    {
        try
        {
            var customLabel = await GetProjectLabelAsync(customer, projectId);
            return GenerateDisplayString(projectId, customLabel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating display string for {Customer}/{ProjectId}", customer, projectId);
            throw;
        }
    }

    /// <summary>
    /// Generate display string in format: "{CustomLabelOrFallback} ({ProjectId})"
    /// </summary>
    private string GenerateDisplayString(string projectId, string? customLabel)
    {
        var label = customLabel ?? GenerateFallbackLabel(projectId);
        return $"{label} ({projectId})";
    }

    /// <summary>
    /// Generate fallback label for projects without custom labels
    /// </summary>
    private string GenerateFallbackLabel(string projectId)
    {
        // Check if matches "ar24-3" pattern
        var match = ArPatternRegex.Match(projectId);
        if (match.Success)
        {
            // Extract year and sequence number
            var yearSuffix = int.Parse(match.Groups[1].Value);
            var year = 2000 + yearSuffix;
            var sequence = match.Groups[2].Value;

            return $"Annual Report {year} ({sequence})";
        }

        // Non-standard pattern: use project ID as label
        return projectId;
    }

    /// <summary>
    /// Load labels from JSON file
    /// </summary>
    private async Task<ProjectLabelsData> LoadLabelsAsync()
    {
        try
        {
            if (!File.Exists(_labelsFilePath))
            {
                _logger.LogDebug("Labels file does not exist, returning empty data: {Path}", _labelsFilePath);
                return new ProjectLabelsData();
            }

            var json = await File.ReadAllTextAsync(_labelsFilePath);

            // Use same serialization options as SaveLabelsAsync to ensure consistency
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true // Handle both camelCase and PascalCase
            };

            var data = JsonSerializer.Deserialize<ProjectLabelsData>(json, options);

            if (data == null)
            {
                _logger.LogWarning("Failed to deserialize labels file, returning empty data: {Path}", _labelsFilePath);
                return new ProjectLabelsData();
            }

            _logger.LogInformation("Loaded labels data from {Path}: {CustomerCount} customers, {TotalLabels} total labels",
                _labelsFilePath,
                data.Projects.Count,
                data.Projects.Values.Sum(d => d.Count));

            // Log details about loaded labels for debugging
            foreach (var (customer, projects) in data.Projects)
            {
                foreach (var (projectId, metadata) in projects)
                {
                    _logger.LogDebug("Loaded label: {Customer}/{ProjectId} = '{Label}'", customer, projectId, metadata.Label);
                }
            }

            return data;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON deserialization error in labels file: {Path}", _labelsFilePath);
            return new ProjectLabelsData();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading labels file: {Path}", _labelsFilePath);
            throw;
        }
    }

    /// <summary>
    /// Save labels to JSON file
    /// </summary>
    private async Task SaveLabelsAsync(ProjectLabelsData data)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_labelsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(data, options);

            // Write to temp file first, then rename (atomic operation)
            var tempPath = $"{_labelsFilePath}.tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _labelsFilePath, overwrite: true);

            _logger.LogDebug("Saved labels data to {Path}", _labelsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving labels file: {Path}", _labelsFilePath);
            throw;
        }
    }

    /// <summary>
    /// Get label from loaded data structure
    /// </summary>
    private static string? GetLabelFromData(ProjectLabelsData data, string customer, string projectId)
    {
        if (data.Projects.TryGetValue(customer, out var customerProjects))
        {
            if (customerProjects.TryGetValue(projectId, out var metadata))
            {
                return metadata.Label;
            }
        }

        return null;
    }

    public async Task<ProjectLabelsData> GetProjectLabelsDataAsync()
    {
        return await LoadLabelsAsync();
    }

    public async Task SaveProjectLabelsDataAsync(ProjectLabelsData data)
    {
        await _fileLock.WaitAsync();
        try
        {
            await SaveLabelsAsync(data);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}

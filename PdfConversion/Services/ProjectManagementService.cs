using PdfConversion.Models;
using Microsoft.Extensions.Caching.Memory;

namespace PdfConversion.Services;

/// <summary>
/// Service for managing PDF conversion projects and file operations
/// </summary>
public interface IProjectManagementService
{
    /// <summary>
    /// Gets all available projects
    /// </summary>
    Task<IEnumerable<Project>> GetProjectsAsync();

    /// <summary>
    /// Gets a specific project by its ID
    /// </summary>
    Task<Project?> GetProjectAsync(string projectId);

    /// <summary>
    /// Gets all files in a project's input directory
    /// </summary>
    Task<IEnumerable<string>> GetProjectFilesAsync(string projectId);

    /// <summary>
    /// Reads the content of a specific input file
    /// </summary>
    Task<string> ReadInputFileAsync(string projectId, string fileName);

    /// <summary>
    /// Saves transformed XHTML content to the output directory
    /// </summary>
    Task SaveOutputAsync(string projectId, string content, string? outputFileName = null);

    /// <summary>
    /// Checks if a project exists
    /// </summary>
    Task<bool> ProjectExistsAsync(string projectId);
}

/// <summary>
/// Implementation of project management service
/// </summary>
public class ProjectManagementService : IProjectManagementService
{
    private readonly ILogger<ProjectManagementService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;
    private const string ProjectsCacheKey = "AllProjects";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public ProjectManagementService(ILogger<ProjectManagementService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;

        // Paths are relative to /app in the Docker container
        _inputBasePath = "/app/data/input/optiver/projects";
        _outputBasePath = "/app/data/output/optiver/projects";

        _logger.LogInformation("ProjectManagementService initialized. Input: {InputPath}, Output: {OutputPath}",
            _inputBasePath, _outputBasePath);
    }

    public async Task<IEnumerable<Project>> GetProjectsAsync()
    {
        // Check cache first
        if (_cache.TryGetValue(ProjectsCacheKey, out IEnumerable<Project>? cachedProjects))
        {
            _logger.LogDebug("Returning {Count} projects from cache", cachedProjects?.Count() ?? 0);
            return cachedProjects ?? Enumerable.Empty<Project>();
        }

        try
        {
            if (!Directory.Exists(_inputBasePath))
            {
                _logger.LogWarning("Input base path does not exist: {Path}", _inputBasePath);
                return Enumerable.Empty<Project>();
            }

            var projectDirectories = Directory.GetDirectories(_inputBasePath);
            var projects = new List<Project>();

            foreach (var projectDir in projectDirectories)
            {
                var projectId = Path.GetFileName(projectDir);

                // Skip hidden directories
                if (projectId.StartsWith('.'))
                    continue;

                var project = await CreateProjectFromDirectoryAsync(projectId, projectDir);
                if (project != null)
                {
                    projects.Add(project);
                }
            }

            // Cache the results
            _cache.Set(ProjectsCacheKey, projects, CacheDuration);

            _logger.LogInformation("Found {Count} projects in {Path}", projects.Count, _inputBasePath);
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning for projects in {Path}", _inputBasePath);
            return Enumerable.Empty<Project>();
        }
    }

    public async Task<Project?> GetProjectAsync(string projectId)
    {
        var projects = await GetProjectsAsync();
        return projects.FirstOrDefault(p => p.ProjectId == projectId);
    }

    public async Task<IEnumerable<string>> GetProjectFilesAsync(string projectId)
    {
        try
        {
            var projectPath = Path.Combine(_inputBasePath, projectId);

            if (!Directory.Exists(projectPath))
            {
                _logger.LogWarning("Project directory does not exist: {Path}", projectPath);
                return Enumerable.Empty<string>();
            }

            // Get XML and HTML files
            var files = Directory.GetFiles(projectPath, "*.*")
                .Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .Cast<string>()
                .ToList();

            _logger.LogDebug("Found {Count} files in project {ProjectId}", files.Count, projectId);
            return await Task.FromResult(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting files for project {ProjectId}", projectId);
            return Enumerable.Empty<string>();
        }
    }

    public async Task<string> ReadInputFileAsync(string projectId, string fileName)
    {
        try
        {
            var filePath = Path.Combine(_inputBasePath, projectId, fileName);

            if (!File.Exists(filePath))
            {
                var error = $"File not found: {filePath}";
                _logger.LogWarning(error);
                throw new FileNotFoundException(error);
            }

            var content = await File.ReadAllTextAsync(filePath);
            _logger.LogDebug("Read {Length} bytes from {FileName} in project {ProjectId}",
                content.Length, fileName, projectId);

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {FileName} from project {ProjectId}", fileName, projectId);
            throw;
        }
    }

    public async Task SaveOutputAsync(string projectId, string content, string? outputFileName = null)
    {
        try
        {
            var projectOutputPath = Path.Combine(_outputBasePath, projectId);

            // Create output directory if it doesn't exist
            if (!Directory.Exists(projectOutputPath))
            {
                Directory.CreateDirectory(projectOutputPath);
                _logger.LogInformation("Created output directory: {Path}", projectOutputPath);
            }

            // Default output filename is taxxor.xhtml
            var fileName = outputFileName ?? "taxxor.xhtml";
            var outputPath = Path.Combine(projectOutputPath, fileName);

            await File.WriteAllTextAsync(outputPath, content);

            _logger.LogInformation("Saved output to {Path} ({Length} bytes)", outputPath, content.Length);

            // Invalidate cache since project status may have changed
            _cache.Remove(ProjectsCacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving output for project {ProjectId}", projectId);
            throw;
        }
    }

    public async Task<bool> ProjectExistsAsync(string projectId)
    {
        try
        {
            var projectPath = Path.Combine(_inputBasePath, projectId);
            return await Task.FromResult(Directory.Exists(projectPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if project {ProjectId} exists", projectId);
            return false;
        }
    }

    private async Task<Project?> CreateProjectFromDirectoryAsync(string projectId, string projectPath)
    {
        try
        {
            var files = await GetProjectFilesAsync(projectId);
            var fileCount = files.Count();

            var outputPath = Path.Combine(_outputBasePath, projectId);
            var outputExists = Directory.Exists(outputPath);
            var outputFile = Path.Combine(outputPath, "taxxor.xhtml");
            var hasOutput = File.Exists(outputFile);

            var status = hasOutput ? ProjectStatus.Completed : ProjectStatus.NotStarted;

            var createdDate = Directory.GetCreationTime(projectPath);
            DateTime? lastProcessedDate = null;

            if (hasOutput)
            {
                lastProcessedDate = File.GetLastWriteTime(outputFile);
            }

            return new Project
            {
                ProjectId = projectId,
                Name = FormatProjectName(projectId),
                InputPath = projectPath,
                OutputPath = outputPath,
                Status = status,
                FileCount = fileCount,
                CreatedDate = createdDate,
                LastProcessedDate = lastProcessedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project object for {ProjectId}", projectId);
            return null;
        }
    }

    private static string FormatProjectName(string projectId)
    {
        // Convert "ar24-3" to "Annual Report 2024 - 3"
        if (projectId.StartsWith("ar"))
        {
            var parts = projectId.Substring(2).Split('-');
            if (parts.Length == 2)
            {
                return $"Annual Report 20{parts[0]} - {parts[1]}";
            }
        }

        return projectId;
    }
}

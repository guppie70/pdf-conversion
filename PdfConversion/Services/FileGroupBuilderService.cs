using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for building file groups for dropdown selectors across all pages.
/// Centralizes the logic for filtering and organizing project files by type and location.
/// </summary>
public interface IFileGroupBuilderService
{
    /// <summary>
    /// Builds file groups containing XML files from project directories.
    /// </summary>
    /// <param name="includeInputFiles">Include XML files from input directories (default: true)</param>
    /// <param name="includeOutputFiles">Include XML files from output directories (default: false)</param>
    /// <param name="onlyActiveProjects">Only include projects not marked as "Ready" (default: true)</param>
    /// <returns>List of project file groups with absolute filesystem paths</returns>
    Task<List<ProjectFileGroup>> BuildXmlFileGroupsAsync(
        bool includeInputFiles = true,
        bool includeOutputFiles = false,
        bool onlyActiveProjects = true);

    /// <summary>
    /// Builds file groups containing document files (PDF, DOCX, DOC, etc.) from project input directories.
    /// </summary>
    /// <param name="extensions">File extensions to include (e.g., [".pdf", ".docx", ".doc"])</param>
    /// <param name="onlyActiveProjects">Only include projects not marked as "Ready" (default: true)</param>
    /// <returns>List of project file groups with absolute filesystem paths</returns>
    Task<List<ProjectFileGroup>> BuildDocumentFileGroupsAsync(
        string[] extensions,
        bool onlyActiveProjects = true);

    /// <summary>
    /// Builds file groups containing all files from project directories.
    /// </summary>
    /// <param name="onlyActiveProjects">Only include projects not marked as "Ready" (default: true)</param>
    /// <returns>List of project file groups with absolute filesystem paths</returns>
    Task<List<ProjectFileGroup>> BuildAllFileGroupsAsync(bool onlyActiveProjects = true);
}

/// <summary>
/// Implementation of the file group builder service.
/// </summary>
public class FileGroupBuilderService : IFileGroupBuilderService
{
    private readonly IProjectManagementService _projectService;
    private readonly ProjectMetadataService _metadataService;
    private readonly ILogger<FileGroupBuilderService> _logger;
    private readonly string _inputBasePath = "/app/data/input";
    private readonly string _outputBasePath = "/app/data/output";

    public FileGroupBuilderService(
        IProjectManagementService projectService,
        ProjectMetadataService metadataService,
        ILogger<FileGroupBuilderService> logger)
    {
        _projectService = projectService;
        _metadataService = metadataService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<ProjectFileGroup>> BuildXmlFileGroupsAsync(
        bool includeInputFiles = true,
        bool includeOutputFiles = false,
        bool onlyActiveProjects = true)
    {
        try
        {
            var projects = await GetFilteredProjectsAsync(onlyActiveProjects);
            var fileGroups = new List<ProjectFileGroup>();

            foreach (var project in projects)
            {
                var files = new List<ProjectFile>();

                // Add input XML files
                if (includeInputFiles)
                {
                    var inputPath = Path.Combine(_inputBasePath, project.Organization, "projects", project.ProjectId);
                    if (Directory.Exists(inputPath))
                    {
                        var inputFiles = Directory.GetFiles(inputPath, "*.xml")
                            .Select(f => new ProjectFile
                            {
                                FileName = Path.GetFileName(f),
                                FullPath = f // Absolute path for XML files
                            });
                        files.AddRange(inputFiles);
                    }
                }

                // Add output XML files
                if (includeOutputFiles)
                {
                    var outputPath = Path.Combine(_outputBasePath, project.Organization, "projects", project.ProjectId);
                    if (Directory.Exists(outputPath))
                    {
                        var outputFiles = Directory.GetFiles(outputPath, "*.xml")
                            .Select(f => new ProjectFile
                            {
                                FileName = Path.GetFileName(f),
                                FullPath = f // Absolute path for XML files
                            });
                        files.AddRange(outputFiles);
                    }
                }

                if (files.Any())
                {
                    fileGroups.Add(new ProjectFileGroup
                    {
                        Customer = project.Organization,
                        ProjectId = project.ProjectId,
                        ProjectName = project.Name,
                        Files = files
                    });
                }
            }

            _logger.LogDebug("Built {Count} XML file groups (includeInput={IncludeInput}, includeOutput={IncludeOutput}, onlyActive={OnlyActive})",
                fileGroups.Count, includeInputFiles, includeOutputFiles, onlyActiveProjects);

            return fileGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building XML file groups");
            return new List<ProjectFileGroup>();
        }
    }

    /// <inheritdoc />
    public async Task<List<ProjectFileGroup>> BuildDocumentFileGroupsAsync(
        string[] extensions,
        bool onlyActiveProjects = true)
    {
        try
        {
            if (extensions == null || extensions.Length == 0)
            {
                _logger.LogWarning("No extensions provided to BuildDocumentFileGroupsAsync");
                return new List<ProjectFileGroup>();
            }

            var projects = await GetFilteredProjectsAsync(onlyActiveProjects);
            var fileGroups = new List<ProjectFileGroup>();

            foreach (var project in projects)
            {
                var inputPath = Path.Combine(_inputBasePath, project.Organization, "projects", project.ProjectId);
                if (!Directory.Exists(inputPath))
                {
                    continue;
                }

                var files = Directory.GetFiles(inputPath)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(f => new ProjectFile
                    {
                        FileName = Path.GetFileName(f),
                        FullPath = f // Absolute path for document files
                    })
                    .ToList();

                if (files.Any())
                {
                    fileGroups.Add(new ProjectFileGroup
                    {
                        Customer = project.Organization,
                        ProjectId = project.ProjectId,
                        ProjectName = project.Name,
                        Files = files
                    });
                }
            }

            _logger.LogDebug("Built {Count} document file groups with extensions [{Extensions}] (onlyActive={OnlyActive})",
                fileGroups.Count, string.Join(", ", extensions), onlyActiveProjects);

            return fileGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building document file groups");
            return new List<ProjectFileGroup>();
        }
    }

    /// <inheritdoc />
    public async Task<List<ProjectFileGroup>> BuildAllFileGroupsAsync(bool onlyActiveProjects = true)
    {
        try
        {
            var projects = await GetFilteredProjectsAsync(onlyActiveProjects);
            var fileGroups = new List<ProjectFileGroup>();

            foreach (var project in projects)
            {
                var inputPath = Path.Combine(_inputBasePath, project.Organization, "projects", project.ProjectId);
                if (!Directory.Exists(inputPath))
                {
                    continue;
                }

                var files = Directory.GetFiles(inputPath)
                    .Select(f => new ProjectFile
                    {
                        FileName = Path.GetFileName(f),
                        FullPath = f // Absolute path for all files
                    })
                    .ToList();

                if (files.Any())
                {
                    fileGroups.Add(new ProjectFileGroup
                    {
                        Customer = project.Organization,
                        ProjectId = project.ProjectId,
                        ProjectName = project.Name,
                        Files = files
                    });
                }
            }

            _logger.LogDebug("Built {Count} file groups with all files (onlyActive={OnlyActive})",
                fileGroups.Count, onlyActiveProjects);

            return fileGroups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building all file groups");
            return new List<ProjectFileGroup>();
        }
    }

    /// <summary>
    /// Gets filtered list of projects based on active status.
    /// </summary>
    private async Task<List<Project>> GetFilteredProjectsAsync(bool onlyActiveProjects)
    {
        // Get all projects from ProjectService
        var allProjects = (await _projectService.GetProjectsAsync()).ToList();

        if (!onlyActiveProjects)
        {
            return allProjects;
        }

        // Get active projects from MetadataService
        var activeProjectMetadata = await _metadataService.GetActiveProjects();

        // Filter to only include active projects (must have metadata entry)
        var filteredProjects = allProjects.Where(p =>
        {
            if (activeProjectMetadata.TryGetValue(p.Organization, out var tenantProjects))
            {
                return tenantProjects.ContainsKey(p.ProjectId);
            }
            // If no metadata exists for this tenant/project, exclude it
            return false;
        }).ToList();

        _logger.LogDebug("Filtered {Total} projects to {Active} active projects",
            allProjects.Count, filteredProjects.Count);

        return filteredProjects;
    }
}

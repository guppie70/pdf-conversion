using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Centralizes project loading logic with metadata merging and filtering.
/// Eliminates duplicate LoadProjectsAsync implementations across pages.
/// </summary>
public interface IProjectLoadingService
{
    /// <summary>
    /// Loads active projects with custom labels merged from metadata.
    /// Only returns projects that have active metadata entries.
    /// </summary>
    Task<List<Project>> LoadActiveProjectsWithLabelsAsync();

    /// <summary>
    /// Loads all projects (active and inactive) with custom labels merged from metadata.
    /// </summary>
    Task<List<Project>> LoadAllProjectsWithLabelsAsync();
}

public class ProjectLoadingService : IProjectLoadingService
{
    private readonly IProjectManagementService _projectService;
    private readonly ProjectMetadataService _metadataService;
    private readonly ILogger<ProjectLoadingService> _logger;

    public ProjectLoadingService(
        IProjectManagementService projectService,
        ProjectMetadataService metadataService,
        ILogger<ProjectLoadingService> logger)
    {
        _projectService = projectService;
        _metadataService = metadataService;
        _logger = logger;
    }

    public async Task<List<Project>> LoadActiveProjectsWithLabelsAsync()
    {
        try
        {
            // Get all projects from ProjectService
            var allProjects = (await _projectService.GetProjectsAsync()).ToList();

            // Get active projects from MetadataService
            var activeProjectMetadata = await _metadataService.GetActiveProjects();

            // Filter to only include active projects (must have metadata entry)
            var activeProjects = allProjects.Where(p =>
            {
                if (activeProjectMetadata.TryGetValue(p.Organization, out var tenantProjects))
                {
                    return tenantProjects.ContainsKey(p.ProjectId);
                }
                // If no metadata exists for this tenant/project, exclude it
                return false;
            }).ToList();

            // Merge custom labels from metadata
            MergeCustomLabels(activeProjects, activeProjectMetadata);

            _logger.LogInformation("Loaded {Count} active projects with labels", activeProjects.Count);
            return activeProjects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active projects with labels");
            throw;
        }
    }

    public async Task<List<Project>> LoadAllProjectsWithLabelsAsync()
    {
        try
        {
            // Get all projects
            var allProjects = (await _projectService.GetProjectsAsync()).ToList();

            // Get metadata for label merging (but don't filter)
            var activeProjectMetadata = await _metadataService.GetActiveProjects();

            // Merge custom labels from metadata (for projects that have them)
            MergeCustomLabels(allProjects, activeProjectMetadata);

            _logger.LogInformation("Loaded {Count} projects with labels", allProjects.Count);
            return allProjects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading all projects with labels");
            throw;
        }
    }

    private void MergeCustomLabels(
        List<Project> projects,
        Dictionary<string, Dictionary<string, ProjectMetadata>> metadata)
    {
        foreach (var project in projects)
        {
            if (metadata.TryGetValue(project.Organization, out var tenantProjects) &&
                tenantProjects.TryGetValue(project.ProjectId, out var projectMetadata) &&
                !string.IsNullOrWhiteSpace(projectMetadata.Label))
            {
                project.Name = $"{projectMetadata.Label} ({project.ProjectId})";
            }
        }
    }
}

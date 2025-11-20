using PdfConversion.Models;
using PdfConversion.Services;

namespace PdfConversion.Helpers;

/// <summary>
/// Result of restoring a project selection from saved user preferences.
/// </summary>
public class RestoredSelection
{
    public bool ProjectFound { get; set; }
    public string? ProjectId { get; set; }
    public string? SourceFile { get; set; }
    public string? HierarchyFile { get; set; }
    public string? TransformedXml { get; set; }
    public string? WarningMessage { get; set; }
}

/// <summary>
/// Centralizes selection restoration logic with legacy format handling.
/// Eliminates duplicate RestoreSavedSelectionsAsync implementations across pages.
/// </summary>
public class SelectionRestorationHelper
{
    private readonly IUserSelectionService _userSelectionService;
    private readonly ILogger<SelectionRestorationHelper> _logger;

    public SelectionRestorationHelper(
        IUserSelectionService userSelectionService,
        ILogger<SelectionRestorationHelper> logger)
    {
        _userSelectionService = userSelectionService;
        _logger = logger;
    }

    /// <summary>
    /// Restores project selection from saved preferences, handling legacy format conversion.
    /// </summary>
    /// <param name="availableProjects">List of projects currently available to select from</param>
    /// <returns>Restored selection with project ID, files, and validation status</returns>
    public async Task<RestoredSelection> RestoreProjectSelectionAsync(List<Project> availableProjects)
    {
        var result = new RestoredSelection();

        try
        {
            var selection = await _userSelectionService.GetSelectionAsync();

            if (string.IsNullOrEmpty(selection.LastSelectedProject))
            {
                _logger.LogDebug("No saved project selection found");
                return result;
            }

            // Support both formats:
            // - Full format: "optiver/ar24-3" (preferred, used by unified dropdown)
            // - Short format: "ar24-3" (legacy, needs lookup)

            string? resolvedProjectId = null;

            if (selection.LastSelectedProject.Contains('/'))
            {
                // Already in full format - use directly
                resolvedProjectId = selection.LastSelectedProject;
                _logger.LogInformation("Restored project selection in full format: {Project}", resolvedProjectId);
            }
            else
            {
                // Short format - find matching project in availableProjects list
                _logger.LogWarning(
                    "⚠️ Legacy ProjectId format detected in user-selections.json: '{ProjectId}' (expected 'customer/project-id' format)",
                    selection.LastSelectedProject);

                var matchingProject = availableProjects?.FirstOrDefault(p =>
                    p.ProjectId.EndsWith("/" + selection.LastSelectedProject, StringComparison.OrdinalIgnoreCase));

                resolvedProjectId = matchingProject?.ProjectId;

                if (resolvedProjectId != null)
                {
                    _logger.LogInformation(
                        "Resolved legacy ProjectId '{Short}' to full format: {Full}",
                        selection.LastSelectedProject,
                        resolvedProjectId);
                }
                else
                {
                    _logger.LogWarning(
                        "Could not resolve legacy ProjectId '{Short}' to any available project",
                        selection.LastSelectedProject);
                    result.WarningMessage = $"Could not find project '{selection.LastSelectedProject}'";
                    return result;
                }
            }

            // Validate that the resolved project exists in available projects
            var projectExists = availableProjects?.Any(p =>
                $"{p.Organization}/{p.ProjectId}" == resolvedProjectId) == true;

            if (!projectExists)
            {
                _logger.LogWarning(
                    "Project '{Project}' not found in loaded projects. Available projects: {Available}",
                    resolvedProjectId,
                    string.Join(", ", availableProjects?.Select(p => $"{p.Organization}/{p.ProjectId}") ?? Array.Empty<string>()));
                result.WarningMessage = $"Project '{resolvedProjectId}' is not available";
                return result;
            }

            // Project found - populate result
            result.ProjectFound = true;
            result.ProjectId = resolvedProjectId;
            result.SourceFile = selection.LastSelectedSourceXml;
            result.HierarchyFile = selection.LastSelectedHierarchyXml;
            result.TransformedXml = selection.LastSelectedTransformedXml;

            _logger.LogInformation(
                "Restored selections: Project={Project}, Source={Source}, Hierarchy={Hierarchy}, Transformed={Transformed}",
                result.ProjectId,
                result.SourceFile,
                result.HierarchyFile,
                result.TransformedXml);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error restoring saved selections");
            result.WarningMessage = $"Error restoring selections: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Validates if a file exists in a list of available files.
    /// </summary>
    public bool ValidateFileExists(string? fileName, List<string>? availableFiles)
    {
        if (string.IsNullOrEmpty(fileName) || availableFiles == null)
            return false;

        return availableFiles.Contains(fileName);
    }
}

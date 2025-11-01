using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for creating new projects with proper directory structure and metadata
/// </summary>
public interface IProjectCreationService
{
    /// <summary>
    /// Create a new project with directory structure and metadata
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="projectLabel">Human-readable project label</param>
    /// <returns>Result indicating success or failure with details</returns>
    Task<ProjectCreationResult> CreateProjectAsync(string customer, string projectId, string projectLabel);

    /// <summary>
    /// Get list of existing customers from filesystem
    /// </summary>
    /// <returns>List of customer names</returns>
    Task<List<string>> GetExistingCustomersAsync();

    /// <summary>
    /// Check if a project already exists
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>True if project exists, false otherwise</returns>
    Task<bool> ProjectExistsAsync(string customer, string projectId);
}

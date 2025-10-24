using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for managing custom project labels and project discovery
/// </summary>
public interface IProjectLabelService
{
    /// <summary>
    /// Scan filesystem and get all discovered projects with their labels
    /// </summary>
    /// <param name="basePath">Base path to scan (default: /app/data/input)</param>
    /// <returns>List of all projects with display strings</returns>
    Task<List<ProjectInfo>> GetAllProjectsAsync(string basePath = "/app/data/input");

    /// <summary>
    /// Get custom label for a specific project
    /// </summary>
    /// <param name="customer">Customer name</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>Custom label or null if not set</returns>
    Task<string?> GetProjectLabelAsync(string customer, string projectId);

    /// <summary>
    /// Set custom label for a project
    /// </summary>
    /// <param name="customer">Customer name</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="label">Custom label to set</param>
    Task SetProjectLabelAsync(string customer, string projectId, string label);

    /// <summary>
    /// Delete custom label for a project
    /// </summary>
    /// <param name="customer">Customer name</param>
    /// <param name="projectId">Project identifier</param>
    Task DeleteProjectLabelAsync(string customer, string projectId);

    /// <summary>
    /// Get display string for a project in format: "{CustomLabelOrFallback} ({ProjectId})"
    /// </summary>
    /// <param name="customer">Customer name</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>Formatted display string</returns>
    Task<string> GetDisplayStringAsync(string customer, string projectId);
}

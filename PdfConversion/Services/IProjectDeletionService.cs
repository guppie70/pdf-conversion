using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for deleting projects and associated data
/// </summary>
public interface IProjectDeletionService
{
    /// <summary>
    /// Deletes a project and all its associated data
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>Deletion result with detailed status</returns>
    Task<ProjectDeletionResult> DeleteProjectAsync(string customer, string projectId);
}

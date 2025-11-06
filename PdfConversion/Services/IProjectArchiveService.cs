namespace PdfConversion.Services;

/// <summary>
/// Service for creating downloadable project archives (ZIP files)
/// </summary>
public interface IProjectArchiveService
{
    /// <summary>
    /// Create a ZIP archive containing all project output files and images
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="hierarchyFileName">Selected hierarchy XML filename from metadata folder</param>
    /// <returns>ZIP file bytes, or null if creation failed</returns>
    Task<byte[]?> CreateProjectArchiveAsync(string customer, string projectId, string hierarchyFileName);

    /// <summary>
    /// Get the suggested filename for the ZIP archive
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>Filename in format: customer-projectId.zip</returns>
    string GetArchiveFilename(string customer, string projectId);

    /// <summary>
    /// Check if project has any files to archive
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>True if files exist, false otherwise</returns>
    Task<bool> HasFilesToArchiveAsync(string customer, string projectId);
}

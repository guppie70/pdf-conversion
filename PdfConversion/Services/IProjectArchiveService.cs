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

    /// <summary>
    /// Create a ZIP package and save it to the packages folder
    /// Called automatically after successful section generation
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="hierarchyFilePath">Full path to the hierarchy XML file used in conversion</param>
    /// <param name="packageName">Name for the package (without .zip extension)</param>
    /// <returns>Full path to created package, or null if failed</returns>
    Task<string?> CreateAndSavePackageAsync(string customer, string projectId, string hierarchyFilePath, string packageName);

    /// <summary>
    /// Get list of available pre-generated packages for a project
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <returns>List of package filenames (e.g., "docling-word.zip")</returns>
    Task<List<string>> GetAvailablePackagesAsync(string customer, string projectId);

    /// <summary>
    /// Get full path to a package file for download
    /// </summary>
    /// <param name="customer">Customer identifier</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="packageFileName">Package filename (e.g., "docling-word.zip")</param>
    /// <returns>Full path to the package file, or null if not found</returns>
    string? GetPackagePath(string customer, string projectId, string packageFileName);
}

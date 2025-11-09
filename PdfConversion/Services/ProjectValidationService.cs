using System.Text.RegularExpressions;

namespace PdfConversion.Services;

/// <summary>
/// Service for validating project parameters and constructing safe project paths.
/// Prevents path traversal attacks and ensures consistent path construction.
/// </summary>
public interface IProjectValidationService
{
    /// <summary>
    /// Validates that customer and projectId parameters are safe to use in file paths.
    /// Checks for null/empty values and path traversal attempts.
    /// </summary>
    bool IsValidProjectParameters(string? customer, string? projectId);

    /// <summary>
    /// Checks if a project directory exists on the filesystem.
    /// </summary>
    bool ProjectExists(string customer, string projectId);

    /// <summary>
    /// Gets the absolute path to a project's input directory.
    /// </summary>
    string GetProjectInputPath(string customer, string projectId);

    /// <summary>
    /// Gets the absolute path to a project's output directory.
    /// </summary>
    string GetProjectOutputPath(string customer, string projectId);
}

public class ProjectValidationService : IProjectValidationService
{
    private readonly ILogger<ProjectValidationService> _logger;
    private readonly string _inputBasePath = "/app/data/input";
    private readonly string _outputBasePath = "/app/data/output";

    // Regex pattern: alphanumeric, hyphens, and underscores only (prevents path traversal)
    private static readonly Regex SafePathPattern = new Regex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled);

    public ProjectValidationService(ILogger<ProjectValidationService> logger)
    {
        _logger = logger;
    }

    public bool IsValidProjectParameters(string? customer, string? projectId)
    {
        if (string.IsNullOrWhiteSpace(customer) || string.IsNullOrWhiteSpace(projectId))
        {
            _logger.LogWarning("Invalid project parameters: customer='{Customer}', projectId='{ProjectId}'", customer, projectId);
            return false;
        }

        // Check for path traversal attempts
        if (!SafePathPattern.IsMatch(customer) || !SafePathPattern.IsMatch(projectId))
        {
            _logger.LogWarning("Path traversal attempt detected: customer='{Customer}', projectId='{ProjectId}'", customer, projectId);
            return false;
        }

        return true;
    }

    public bool ProjectExists(string customer, string projectId)
    {
        if (!IsValidProjectParameters(customer, projectId))
            return false;

        var inputPath = GetProjectInputPath(customer, projectId);
        var outputPath = GetProjectOutputPath(customer, projectId);

        return Directory.Exists(inputPath) || Directory.Exists(outputPath);
    }

    public string GetProjectInputPath(string customer, string projectId)
    {
        return Path.Combine(_inputBasePath, customer, "projects", projectId);
    }

    public string GetProjectOutputPath(string customer, string projectId)
    {
        return Path.Combine(_outputBasePath, customer, "projects", projectId);
    }
}

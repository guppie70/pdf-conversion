using System.Text.RegularExpressions;

namespace PdfConversion.Utils;

/// <summary>
/// Components of a parsed project file path.
/// </summary>
public class ProjectPathComponents
{
    public bool Success { get; set; }
    public string? InputOutput { get; set; }  // "input" or "output"
    public string? Organization { get; set; }  // customer/organization name
    public string? ProjectId { get; set; }     // project identifier
    public string? FileName { get; set; }      // file name with extension
    public string? FullProjectId { get; set; } // "organization/projectId"
}

/// <summary>
/// Centralizes project file path parsing logic.
/// Eliminates duplicate regex-based path parsing across pages.
/// </summary>
public static class ProjectPathParser
{
    private static readonly Regex PathRegex = new(
        @"/app/data/(input|output)/([^/]+)/projects/([^/]+)/(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses an absolute project file path into its components.
    /// Expected format: /app/data/{input|output}/{organization}/projects/{projectId}/{fileName}
    /// </summary>
    /// <param name="absolutePath">The absolute file path to parse</param>
    /// <returns>Parsed components, or Success=false if parsing failed</returns>
    public static ProjectPathComponents Parse(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return new ProjectPathComponents { Success = false };
        }

        var match = PathRegex.Match(absolutePath);

        if (!match.Success)
        {
            return new ProjectPathComponents { Success = false };
        }

        var organization = match.Groups[2].Value;
        var projectId = match.Groups[3].Value;

        return new ProjectPathComponents
        {
            Success = true,
            InputOutput = match.Groups[1].Value,
            Organization = organization,
            ProjectId = projectId,
            FileName = match.Groups[4].Value,
            FullProjectId = $"{organization}/{projectId}"
        };
    }

    /// <summary>
    /// Builds an absolute path from components.
    /// </summary>
    /// <param name="inputOrOutput">"input" or "output"</param>
    /// <param name="organization">Organization/customer name</param>
    /// <param name="projectId">Project identifier</param>
    /// <param name="fileName">File name with extension</param>
    /// <returns>Absolute path in format: /app/data/{inputOrOutput}/{organization}/projects/{projectId}/{fileName}</returns>
    public static string BuildPath(string inputOrOutput, string organization, string projectId, string fileName)
    {
        return $"/app/data/{inputOrOutput}/{organization}/projects/{projectId}/{fileName}";
    }

    /// <summary>
    /// Builds an absolute path from a full project ID (organization/projectId format).
    /// </summary>
    /// <param name="inputOrOutput">"input" or "output"</param>
    /// <param name="fullProjectId">Project ID in format "organization/projectId"</param>
    /// <param name="fileName">File name with extension</param>
    /// <returns>Absolute path, or null if fullProjectId format is invalid</returns>
    public static string? BuildPathFromFullId(string inputOrOutput, string fullProjectId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fullProjectId) || !fullProjectId.Contains('/'))
            return null;

        var parts = fullProjectId.Split('/', 2);
        if (parts.Length != 2)
            return null;

        return BuildPath(inputOrOutput, parts[0], parts[1], fileName);
    }
}

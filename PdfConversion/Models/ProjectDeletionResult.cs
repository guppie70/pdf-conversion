namespace PdfConversion.Models;

/// <summary>
/// Result of a project deletion operation
/// </summary>
public class ProjectDeletionResult
{
    /// <summary>
    /// Indicates if the deletion was fully successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Indicates if the operation completed with warnings (partial success)
    /// </summary>
    public bool HasWarnings { get; set; }

    /// <summary>
    /// Customer identifier
    /// </summary>
    public string Customer { get; set; } = string.Empty;

    /// <summary>
    /// Project identifier
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Main message describing the result
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed results for each operation
    /// </summary>
    public DeletionDetails Details { get; set; } = new();

    public static ProjectDeletionResult FullSuccess(string customer, string projectId)
    {
        return new ProjectDeletionResult
        {
            Success = true,
            HasWarnings = false,
            Customer = customer,
            ProjectId = projectId,
            Message = $"Project {projectId} deleted successfully"
        };
    }

    public static ProjectDeletionResult PartialSuccess(string customer, string projectId, DeletionDetails details)
    {
        return new ProjectDeletionResult
        {
            Success = true,
            HasWarnings = true,
            Customer = customer,
            ProjectId = projectId,
            Message = $"Project {projectId} deleted with warnings",
            Details = details
        };
    }

    public static ProjectDeletionResult Failure(string customer, string projectId, string errorMessage)
    {
        return new ProjectDeletionResult
        {
            Success = false,
            Customer = customer,
            ProjectId = projectId,
            Message = $"Failed to delete project {projectId}: {errorMessage}"
        };
    }
}

/// <summary>
/// Detailed breakdown of deletion operations
/// </summary>
public class DeletionDetails
{
    public bool InputFolderDeleted { get; set; }
    public string? InputFolderError { get; set; }

    public bool OutputFolderDeleted { get; set; }
    public string? OutputFolderError { get; set; }

    public bool MetadataRemoved { get; set; }
    public string? MetadataError { get; set; }

    public bool UserSelectionsRemoved { get; set; }
    public string? UserSelectionsError { get; set; }

    /// <summary>
    /// Gets a summary of what failed
    /// </summary>
    public string GetWarningsSummary()
    {
        var warnings = new List<string>();

        if (!InputFolderDeleted && !string.IsNullOrEmpty(InputFolderError))
            warnings.Add($"Input folder: {InputFolderError}");

        if (!OutputFolderDeleted && !string.IsNullOrEmpty(OutputFolderError))
            warnings.Add($"Output folder: {OutputFolderError}");

        if (!MetadataRemoved && !string.IsNullOrEmpty(MetadataError))
            warnings.Add($"Metadata: {MetadataError}");

        if (!UserSelectionsRemoved && !string.IsNullOrEmpty(UserSelectionsError))
            warnings.Add($"User selections: {UserSelectionsError}");

        return string.Join("; ", warnings);
    }
}

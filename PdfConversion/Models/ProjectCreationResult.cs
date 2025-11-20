namespace PdfConversion.Models;

/// <summary>
/// Result of a project creation operation
/// </summary>
public class ProjectCreationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Customer { get; set; }
    public string? ProjectId { get; set; }
    public List<string> CreatedPaths { get; set; } = new();

    public static ProjectCreationResult Successful(string customer, string projectId, List<string> createdPaths)
    {
        return new ProjectCreationResult
        {
            Success = true,
            Customer = customer,
            ProjectId = projectId,
            CreatedPaths = createdPaths
        };
    }

    public static ProjectCreationResult Failure(string errorMessage)
    {
        return new ProjectCreationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

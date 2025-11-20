namespace PdfConversion.Models;

/// <summary>
/// Project lifecycle status for project management tracking
/// (distinct from conversion processing status)
/// </summary>
public enum ProjectLifecycleStatus
{
    Open,
    InProgress,
    Ready,
    Parked
}

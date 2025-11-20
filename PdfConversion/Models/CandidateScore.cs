namespace PdfConversion.Models;

/// <summary>
/// Represents a scored candidate for duplicate header resolution
/// </summary>
public class CandidateScore
{
    /// <summary>
    /// The header match candidate
    /// </summary>
    public HeaderMatch Match { get; set; } = null!;

    /// <summary>
    /// Forward-looking score (based on next 3-4 expected headers)
    /// </summary>
    public int ForwardLookingScore { get; set; }

    /// <summary>
    /// Tiebreaker score (element type, continuation markers, context)
    /// </summary>
    public int TiebreakerScore { get; set; }

    /// <summary>
    /// Explanation of how tiebreaker score was calculated
    /// </summary>
    public string TiebreakerReason { get; set; } = "";

    /// <summary>
    /// Combined total score (forward + tiebreaker)
    /// </summary>
    public int TotalScore => ForwardLookingScore + TiebreakerScore;
}

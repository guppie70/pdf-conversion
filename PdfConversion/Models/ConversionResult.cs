namespace PdfConversion.Models;

/// <summary>
/// Result of a complete conversion operation from PDF to Taxxor sections
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Whether the conversion completed successfully (no failed sections)
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Total number of sections in the hierarchy
    /// </summary>
    public int TotalSections { get; set; }

    /// <summary>
    /// Number of sections successfully converted
    /// </summary>
    public int SuccessfulSections { get; set; }

    /// <summary>
    /// Number of sections that failed during conversion
    /// </summary>
    public int FailedSections { get; set; }

    /// <summary>
    /// Number of sections skipped (duplicates or unmatched)
    /// </summary>
    public int SkippedSections { get; set; }

    /// <summary>
    /// List of successfully created XML files (relative paths)
    /// </summary>
    public List<string> CreatedFiles { get; set; } = new();

    /// <summary>
    /// List of error messages encountered during conversion
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Total time taken for the conversion
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Whether the conversion was cancelled by the user
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Returns a formatted summary of the conversion results
    /// </summary>
    public override string ToString() =>
        WasCancelled
            ? $"Conversion Cancelled: " +
              $"{SuccessfulSections}/{TotalSections} sections created before cancellation, " +
              $"Duration: {Duration.TotalSeconds:F1}s"
            : $"Conversion {(Success ? "Successful" : "Failed")}: " +
              $"{SuccessfulSections}/{TotalSections} sections created, " +
              $"{FailedSections} failed, {SkippedSections} skipped, " +
              $"Duration: {Duration.TotalSeconds:F1}s";
}

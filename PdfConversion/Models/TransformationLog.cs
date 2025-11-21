namespace PdfConversion.Models;

/// <summary>
/// Log entry for a transformation operation
/// </summary>
public class TransformationLog
{
    /// <summary>
    /// Unique identifier for this log entry
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Project identifier this transformation belongs to
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the file that was transformed
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// When the transformation started
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the transformation completed
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Status of the transformation (Success, Error, Warning)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Detailed information about the transformation (statistics, errors, warnings)
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// The XSLT template used for this transformation
    /// </summary>
    public string XsltUsed { get; set; } = string.Empty;

    /// <summary>
    /// Number of headers that were normalized
    /// </summary>
    public int? HeadersNormalized { get; set; }

    /// <summary>
    /// Number of tables that were processed
    /// </summary>
    public int? TablesProcessed { get; set; }

    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long? ProcessingTimeMs { get; set; }
}

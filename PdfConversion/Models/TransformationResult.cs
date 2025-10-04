namespace PdfConversion.Models;

/// <summary>
/// Result of an XSLT transformation operation
/// </summary>
public class TransformationResult
{
    /// <summary>
    /// Indicates whether the transformation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// The transformed XHTML content (populated on success)
    /// </summary>
    public string OutputContent { get; set; } = string.Empty;

    /// <summary>
    /// Error message if transformation failed
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Time taken to process the transformation in milliseconds
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of headers that were normalized (e.g., h1→h2→h3 fixes)
    /// </summary>
    public int HeadersNormalized { get; set; }

    /// <summary>
    /// Number of tables processed and converted to Taxxor format
    /// </summary>
    public int TablesProcessed { get; set; }

    /// <summary>
    /// List of warning messages generated during transformation
    /// </summary>
    public List<string> WarningMessages { get; set; } = new List<string>();
}

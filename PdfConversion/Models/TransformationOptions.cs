namespace PdfConversion.Models;

/// <summary>
/// Options for controlling XSLT transformation behavior
/// </summary>
public class TransformationOptions
{
    /// <summary>
    /// Whether to use the XSLT3Service for XSLT 2.0/3.0 features
    /// If false, uses System.Xml.Xsl (XSLT 1.0 only)
    /// </summary>
    public bool UseXslt3Service { get; set; } = false;

    /// <summary>
    /// Maximum time allowed for transformation in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to normalize header hierarchy after transformation
    /// </summary>
    public bool NormalizeHeaders { get; set; } = true;

    /// <summary>
    /// Custom parameters to pass to the XSLT transformation
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}

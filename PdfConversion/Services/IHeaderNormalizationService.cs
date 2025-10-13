using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for normalizing header levels in extracted content.
/// Ensures content always starts with h1 and maintains proper hierarchy.
/// </summary>
public interface IHeaderNormalizationService
{
    /// <summary>
    /// Normalizes all header levels in the content to ensure the first header is h1
    /// and maintains proper hierarchy throughout.
    /// </summary>
    /// <param name="content">The XML document containing headers to normalize</param>
    /// <returns>A new XDocument with normalized header levels</returns>
    XDocument NormalizeHeaders(XDocument content);

    /// <summary>
    /// Calculates the shift amount needed to normalize headers.
    /// For example, if first header is h3, returns -2 (to shift to h1).
    /// </summary>
    /// <param name="content">The XML document to analyze</param>
    /// <returns>The shift amount (negative to shift down, positive to shift up)</returns>
    int CalculateShiftAmount(XDocument content);
}

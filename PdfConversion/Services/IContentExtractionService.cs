using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for extracting content sections from transformed XHTML based on header matches.
/// </summary>
public interface IContentExtractionService
{
    /// <summary>
    /// Extracts content from a transformed XHTML document starting at the specified header
    /// and ending at either the next header of same/higher level, a specified end header,
    /// or the end of the document.
    /// </summary>
    /// <param name="transformedXhtml">The transformed XHTML document to extract from.</param>
    /// <param name="startHeader">The header element to start extraction from.</param>
    /// <param name="endHeader">Optional end header to stop extraction at.</param>
    /// <returns>A new XHTML document containing the extracted content.</returns>
    /// <exception cref="ArgumentNullException">If transformedXhtml or startHeader is null.</exception>
    /// <exception cref="ArgumentException">If startHeader is not in the document or endHeader is specified but not found.</exception>
    XDocument ExtractContent(
        XDocument transformedXhtml,
        XElement startHeader,
        XElement? endHeader = null);

    /// <summary>
    /// Finds the next header at the same or higher level after the specified start header.
    /// </summary>
    /// <param name="transformedXhtml">The transformed XHTML document to search.</param>
    /// <param name="startHeader">The header element to start searching after.</param>
    /// <returns>The next header element at same/higher level, or null if none found.</returns>
    /// <exception cref="ArgumentNullException">If transformedXhtml or startHeader is null.</exception>
    XElement? FindNextHeader(
        XDocument transformedXhtml,
        XElement startHeader);
}

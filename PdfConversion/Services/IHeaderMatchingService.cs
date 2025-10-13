using PdfConversion.Models;
using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for matching hierarchy items to headers in transformed XHTML documents
/// </summary>
public interface IHeaderMatchingService
{
    /// <summary>
    /// Finds exact text matches between hierarchy items and headers in the transformed XHTML.
    /// If fuzzy matching is enabled, attempts fuzzy matching for unmatched items.
    /// </summary>
    /// <param name="transformedXhtml">The transformed XHTML document containing headers</param>
    /// <param name="hierarchyItems">The list of hierarchy items to match</param>
    /// <param name="enableFuzzyMatch">Enable fuzzy matching for unmatched items (default: true)</param>
    /// <param name="minConfidenceThreshold">Minimum confidence score for fuzzy matches (default: 0.65)</param>
    /// <returns>List of header matches (both matched and unmatched items)</returns>
    Task<List<HeaderMatch>> FindExactMatchesAsync(
        XDocument transformedXhtml,
        List<HierarchyItem> hierarchyItems,
        bool enableFuzzyMatch = true,
        double minConfidenceThreshold = 0.65);
}

namespace PdfConversion.Models;

/// <summary>
/// Root container for hierarchy structure
/// </summary>
public class HierarchyStructure
{
    public HierarchyItem Root { get; set; } = new();

    /// <summary>
    /// Overall confidence of the hierarchy (0-100)
    /// </summary>
    public int OverallConfidence { get; set; }

    /// <summary>
    /// List of items with low confidence (<70%)
    /// </summary>
    public List<HierarchyItem> Uncertainties { get; set; } = new();
}

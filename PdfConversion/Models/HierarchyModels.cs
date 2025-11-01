namespace PdfConversion.Models;

/// <summary>
/// Event args for hierarchy item selection with modifier keys
/// </summary>
public class ItemSelectionEventArgs
{
    public HierarchyItem Item { get; set; } = null!;
    public bool ShiftKey { get; set; }
    public bool CtrlKey { get; set; }
}

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

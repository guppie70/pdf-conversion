namespace PdfConversion.Models;

/// <summary>
/// Statistics collected during rule-based hierarchy generation
/// </summary>
public class HierarchyGenerationStatistics
{
    public int HeadersProcessed { get; set; }
    public int ItemsCreated { get; set; }
    public int MaxDepth { get; set; }
    public int PatternsMatched { get; set; }
    public int TopLevelSections { get; set; }
    public TimeSpan Duration { get; set; }

    public string GetSummary()
    {
        return $"Converted {HeadersProcessed} headers into {TopLevelSections} sections using {MaxDepth} levels of hierarchy";
    }
}

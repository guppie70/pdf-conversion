namespace PdfConversion.Models;

/// <summary>
/// Result of rule-based hierarchy generation with dual-level logging
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// Root hierarchy item
    /// </summary>
    public HierarchyItem Root { get; set; } = null!;

    /// <summary>
    /// User-friendly progress logs (concise, high-level steps)
    /// </summary>
    public List<string> GenericLogs { get; set; } = new();

    /// <summary>
    /// Technical debugging logs (verbose, every decision point)
    /// </summary>
    public List<string> TechnicalLogs { get; set; } = new();

    /// <summary>
    /// Generation statistics for summary display
    /// </summary>
    public GenerationStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Statistical summary of hierarchy generation
/// </summary>
public class GenerationStatistics
{
    /// <summary>
    /// Total number of headers processed from input XML
    /// </summary>
    public int HeadersProcessed { get; set; }

    /// <summary>
    /// Total number of hierarchy items created (excluding root)
    /// </summary>
    public int ItemsCreated { get; set; }

    /// <summary>
    /// Maximum depth of the hierarchy tree
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Number of headers successfully matched to known patterns
    /// </summary>
    public int PatternsMatched { get; set; }

    /// <summary>
    /// Generation duration in milliseconds
    /// </summary>
    public long DurationMs { get; set; }
}

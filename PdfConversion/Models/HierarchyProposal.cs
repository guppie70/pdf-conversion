using PdfConversion.Services;

namespace PdfConversion.Models;

/// <summary>
/// Represents an AI-generated hierarchy proposal with confidence scoring
/// Used as the output of AI hierarchy generation before converting to final HierarchyStructure
/// </summary>
public class HierarchyProposal
{
    /// <summary>
    /// Root item of the proposed hierarchy
    /// </summary>
    public HierarchyItem Root { get; set; } = new();

    /// <summary>
    /// Overall confidence score (0-100) calculated as average of all items
    /// </summary>
    public int OverallConfidence { get; set; }

    /// <summary>
    /// Items with confidence < 70% that need human review
    /// </summary>
    public List<HierarchyItem> Uncertainties { get; set; } = new();

    /// <summary>
    /// AI's reasoning/explanation for the overall structure
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Total number of items in the hierarchy (including root)
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Validation result indicating if hallucinations were detected
    /// </summary>
    public HierarchyValidationResult? ValidationResult { get; set; }

    /// <summary>
    /// Converts this proposal to a HierarchyStructure
    /// </summary>
    public HierarchyStructure ToHierarchyStructure()
    {
        return new HierarchyStructure
        {
            Root = Root,
            OverallConfidence = OverallConfidence,
            Uncertainties = Uncertainties
        };
    }
}

namespace PdfConversion.Models;

/// <summary>
/// Complete pattern database learned from training hierarchies
/// </summary>
public class PatternDatabase
{
    public DateTime CreatedAt { get; set; }
    public int TotalHierarchiesAnalyzed { get; set; }
    public int TotalItemsAnalyzed { get; set; }

    // Level-based patterns
    public Dictionary<int, LevelProfile> LevelProfiles { get; set; } = new();

    // Section vocabulary
    public List<SectionVocabulary> CommonSections { get; set; } = new();

    // Sequence patterns
    public List<SequencePattern> TypicalSequences { get; set; } = new();

    // TOC numbering patterns
    public Dictionary<string, NumberingPattern> NumberingPatterns { get; set; } = new();

    // Parent-child relationships
    public List<ParentChildPattern> ParentChildPatterns { get; set; } = new();
}

public class LevelProfile
{
    public int Level { get; set; }
    public int TotalOccurrences { get; set; }

    // Content metrics
    public double AvgWordCount { get; set; }
    public double MinWordCount { get; set; }
    public double MaxWordCount { get; set; }
    public double MedianWordCount { get; set; }

    // Structure metrics
    public double AvgChildCount { get; set; }
    public double MedianChildCount { get; set; }
    public int MaxChildCount { get; set; }

    public double AvgSiblingCount { get; set; }

    // Most common headers at this level
    public List<HeaderFrequency> CommonHeaders { get; set; } = new();
}

public class HeaderFrequency
{
    public string HeaderText { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public double Frequency { get; set; } // 0.0-1.0
}

public class SectionVocabulary
{
    public string HeaderText { get; set; } = string.Empty;
    public int MostCommonLevel { get; set; }
    public Dictionary<int, int> LevelDistribution { get; set; } = new(); // Level → Count
    public int TotalOccurrences { get; set; }
    public double Confidence { get; set; } // How consistent is the level?

    // Parent-child context
    public List<string> CommonParents { get; set; } = new();
    public List<string> CommonChildren { get; set; } = new();
}

public class SequencePattern
{
    public string SectionName { get; set; } = string.Empty;
    public string TypicallyFollows { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public double Confidence { get; set; }
}

public class NumberingPattern
{
    public string Pattern { get; set; } = string.Empty; // e.g., "1.", "(a)", "1.1"
    public int MostCommonLevel { get; set; }
    public int Occurrences { get; set; }
    public double Confidence { get; set; }
}

public class ParentChildPattern
{
    public string ParentHeader { get; set; } = string.Empty;
    public string ChildHeader { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public double Confidence { get; set; }
}

using System.Text.RegularExpressions;
using PdfConversion.Models;
using PdfConversion.Utils;

namespace PdfConversion.Services;

/// <summary>
/// Generates hierarchies using deterministic rules instead of LLM inference.
/// Faster, more reliable, and debuggable than LLM approach.
/// </summary>
public class RuleBasedHierarchyGenerator
{
    private readonly ILogger<RuleBasedHierarchyGenerator> _logger;

    // Major section keywords that ALWAYS create level 1 boundaries
    private static readonly string[] MajorSectionKeywords = new[]
    {
        "Directors' report",
        "Directors report",
        "Statement of profit or loss",
        "Statement of financial position",
        "Statement of changes in equity",
        "Statement of cash flows",
        "Notes to the financial statements",
        "Directors' declaration",
        "Directors declaration",
        "Independent auditor",
        "Contents",
        "Cover",
        "Table of contents",
        "Lead auditor's independence declaration"
    };

    public RuleBasedHierarchyGenerator(ILogger<RuleBasedHierarchyGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates hierarchy using rule-based logic
    /// </summary>
    public HierarchyItem GenerateHierarchy(List<HierarchyGeneratorService.HeaderInfo> headers)
    {
        _logger.LogInformation("[RuleBasedHierarchy] Starting rule-based hierarchy generation with {Count} headers",
            headers.Count);

        // Create root item
        var rootItem = new HierarchyItem
        {
            Id = "report-root",
            Level = 0,
            LinkName = "Annual Report 2024",
            DataRef = "report-root.xml",
            Path = "/",
            SubItems = new List<HierarchyItem>(),
            Confidence = 100
        };

        // Track current context
        var currentMajorSection = rootItem;
        var lastItemAtLevel = new Dictionary<int, HierarchyItem> { [0] = rootItem };
        var inDirectorsReport = false;
        var inNotesSection = false;
        var directorsReportStartLine = -1;

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var lineNumber = i + 1;
            var headerText = header.Text.Trim();

            // Check if this is a major section boundary
            var isMajorSection = IsMajorSection(headerText);

            if (isMajorSection)
            {
                // Major section detected
                _logger.LogInformation("[RuleBasedHierarchy] Line {Line}: MAJOR SECTION → \"{Header}\"",
                    lineNumber, headerText);

                var item = CreateHierarchyItem(header, 1, rootItem);
                rootItem.SubItems.Add(item);
                lastItemAtLevel[1] = item;
                currentMajorSection = item;

                // Update context flags
                if (IsDirectorsReport(headerText))
                {
                    inDirectorsReport = true;
                    directorsReportStartLine = lineNumber;
                    inNotesSection = false;
                }
                else if (IsNotesSection(headerText))
                {
                    inNotesSection = true;
                    inDirectorsReport = false;
                }
                else
                {
                    inDirectorsReport = false;
                    inNotesSection = false;
                }

                // Clear lower level tracking
                ClearLowerLevels(lastItemAtLevel, 1);
                continue;
            }

            // Check if we should skip this header (subsection within Directors' report)
            if (inDirectorsReport && ShouldSkipAsSubsection(header, lineNumber, directorsReportStartLine))
            {
                _logger.LogDebug("[RuleBasedHierarchy] Line {Line}: SKIPPED (subsection within Directors' report) → \"{Header}\"",
                    lineNumber, headerText);
                continue;
            }

            // Handle Notes section hierarchy
            if (inNotesSection && !string.IsNullOrEmpty(header.DataNumber))
            {
                var noteLevel = DetermineNoteLevel(header.DataNumber);
                if (noteLevel > 0)
                {
                    _logger.LogInformation("[RuleBasedHierarchy] Line {Line}: NOTE LEVEL {Level} (data-number=\"{DataNumber}\") → \"{Header}\"",
                        lineNumber, noteLevel, header.DataNumber, headerText);

                    // Find parent
                    HierarchyItem parent = currentMajorSection;
                    if (noteLevel > 2)
                    {
                        // Try to find parent at level-1
                        if (lastItemAtLevel.TryGetValue(noteLevel - 1, out var potentialParent))
                        {
                            parent = potentialParent;
                        }
                    }

                    var item = CreateHierarchyItem(header, noteLevel, parent);
                    parent.SubItems.Add(item);
                    lastItemAtLevel[noteLevel] = item;

                    // Clear lower levels
                    ClearLowerLevels(lastItemAtLevel, noteLevel);
                    continue;
                }
            }

            // Default: skip this header (it's in-section content)
            _logger.LogDebug("[RuleBasedHierarchy] Line {Line}: IN-SECTION CONTENT → \"{Header}\"",
                lineNumber, headerText);
        }

        _logger.LogInformation("[RuleBasedHierarchy] Generated hierarchy: {TopLevel} top-level sections, {Total} total items",
            rootItem.SubItems.Count, CountItems(rootItem));

        return rootItem;
    }

    /// <summary>
    /// Checks if header text matches a major section keyword
    /// </summary>
    private bool IsMajorSection(string headerText)
    {
        return MajorSectionKeywords.Any(keyword =>
            headerText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if header is Directors' report
    /// </summary>
    private bool IsDirectorsReport(string headerText)
    {
        return headerText.Contains("Directors", StringComparison.OrdinalIgnoreCase) &&
               headerText.Contains("report", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if header is Notes section
    /// </summary>
    private bool IsNotesSection(string headerText)
    {
        return headerText.Contains("Notes to", StringComparison.OrdinalIgnoreCase) ||
               headerText.Contains("Notes to the financial statements", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if a header should be skipped as a subsection within Directors' report.
    /// Skip numbered items like "1.", "2.", "3." between Directors' report and next major section.
    /// </summary>
    private bool ShouldSkipAsSubsection(HierarchyGeneratorService.HeaderInfo header, int lineNumber, int directorsReportStartLine)
    {
        // Only skip if we have a simple numeric data-number like "1.", "2.", etc.
        if (string.IsNullOrEmpty(header.DataNumber))
            return false;

        // Pattern: single digit followed by dot
        var simpleNumberPattern = new Regex(@"^\d+\.$");
        return simpleNumberPattern.IsMatch(header.DataNumber);
    }

    /// <summary>
    /// Determines note hierarchy level based on data-number pattern.
    /// Returns 0 if not a recognized note pattern.
    /// </summary>
    private int DetermineNoteLevel(string dataNumber)
    {
        if (string.IsNullOrEmpty(dataNumber))
            return 0;

        // Level 2: "1." or "12." (simple numeric with dot)
        if (Regex.IsMatch(dataNumber, @"^\d+\.$"))
            return 2;

        // Level 3: "(a)" or "(b)" (single letter in parentheses)
        if (Regex.IsMatch(dataNumber, @"^\([a-z]\)$", RegexOptions.IgnoreCase))
            return 3;

        // Level 4: "(i)" or "(ii)" (roman numerals in parentheses)
        if (Regex.IsMatch(dataNumber, @"^\([ivxlcdm]+\)$", RegexOptions.IgnoreCase))
            return 4;

        // Level 2: "1.1" or "2.3" (dotted notation)
        if (Regex.IsMatch(dataNumber, @"^\d+\.\d+$"))
            return 2;

        return 0;
    }

    /// <summary>
    /// Creates a HierarchyItem from HeaderInfo
    /// </summary>
    private HierarchyItem CreateHierarchyItem(HierarchyGeneratorService.HeaderInfo header, int level, HierarchyItem parent)
    {
        var normalizedId = FilenameUtils.NormalizeFileName(header.Text);

        return new HierarchyItem
        {
            Id = normalizedId,
            Level = level,
            LinkName = header.Text.Trim(),
            DataRef = $"{normalizedId}.xml",
            Path = parent.Path,
            SubItems = new List<HierarchyItem>(),
            Confidence = 100 // Deterministic rules = 100% confidence
        };
    }

    /// <summary>
    /// Clears tracking dictionary for levels below the current level
    /// </summary>
    private void ClearLowerLevels(Dictionary<int, HierarchyItem> lastItemAtLevel, int currentLevel)
    {
        var levelsToRemove = lastItemAtLevel.Keys.Where(k => k > currentLevel).ToList();
        foreach (var level in levelsToRemove)
        {
            lastItemAtLevel.Remove(level);
        }
    }

    /// <summary>
    /// Counts total items in hierarchy tree
    /// </summary>
    private int CountItems(HierarchyItem item)
    {
        int count = 1; // Count this item
        foreach (var subItem in item.SubItems)
        {
            count += CountItems(subItem);
        }
        return count;
    }
}

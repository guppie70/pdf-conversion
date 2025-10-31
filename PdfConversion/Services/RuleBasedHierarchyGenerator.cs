using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using PdfConversion.Models;
using PdfConversion.Utils;

namespace PdfConversion.Services;

/// <summary>
/// Generates hierarchies using deterministic rules instead of LLM inference.
/// Faster, more reliable, and debuggable than LLM approach.
/// Uses learned patterns from training hierarchies when available, falls back to hardcoded rules.
/// </summary>
public class RuleBasedHierarchyGenerator
{
    private readonly ILogger<RuleBasedHierarchyGenerator> _logger;
    private readonly PatternDatabase? _patterns;

    // Major section keywords that ALWAYS create level 1 boundaries (fallback if patterns not loaded)
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
        _patterns = LoadPatterns();
    }

    /// <summary>
    /// Loads learned patterns from database. Returns null if not available (graceful fallback).
    /// </summary>
    private PatternDatabase? LoadPatterns()
    {
        try
        {
            var path = Path.Combine("data", "patterns", "learned-rules.json");
            if (!File.Exists(path))
            {
                _logger.LogWarning("[RuleBasedHierarchy] Pattern database not found at {Path}, using hardcoded rules", path);
                return null;
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var patterns = JsonSerializer.Deserialize<PatternDatabase>(json, options);

            if (patterns == null)
            {
                _logger.LogWarning("[RuleBasedHierarchy] Failed to deserialize pattern database, using hardcoded rules");
                return null;
            }

            _logger.LogInformation("[RuleBasedHierarchy] Loaded pattern database: {Files} hierarchies, {Items} items, {Sections} sections analyzed",
                patterns.TotalHierarchiesAnalyzed,
                patterns.TotalItemsAnalyzed,
                patterns.CommonSections?.Count ?? 0);

            return patterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RuleBasedHierarchy] Failed to load pattern database, falling back to hardcoded rules");
            return null;
        }
    }

    /// <summary>
    /// Generates hierarchy using rule-based logic with dual-level logging
    /// </summary>
    public GenerationResult GenerateHierarchy(List<HierarchyGeneratorService.HeaderInfo> headers)
    {
        var stopwatch = Stopwatch.StartNew();
        var genericLogs = new List<string>();
        var technicalLogs = new List<string>();
        var patternsMatched = 0;

        // Generic logs: user-friendly progress messages
        genericLogs.Add("Starting hierarchy generation...");
        genericLogs.Add($"Processing {headers.Count} headers from document");

        // Technical logs: detailed debugging information
        technicalLogs.Add("[RuleBasedHierarchyGenerator] Starting generation");
        technicalLogs.Add($"[Input] {headers.Count} headers found in normalized XML");
        technicalLogs.Add($"[Pattern Database] Using {(_patterns != null ? "learned patterns" : "hardcoded rules")}");

        if (_patterns != null)
        {
            genericLogs.Add("Applying learned patterns from training data");
            technicalLogs.Add($"[Pattern Database] {_patterns.TotalHierarchiesAnalyzed} training hierarchies analyzed");
            technicalLogs.Add($"[Pattern Database] {_patterns.CommonSections?.Count ?? 0} known section patterns");
        }
        else
        {
            genericLogs.Add("Using hardcoded rules for section detection");
            technicalLogs.Add("[Pattern Database] No learned patterns available, using fallback rules");
        }

        _logger.LogInformation("[RuleBasedHierarchy] Starting rule-based hierarchy generation with {Count} headers",
            headers.Count);
        _logger.LogInformation("[RuleBasedHierarchy] Using {Mode} for section detection",
            _patterns != null ? "learned patterns" : "hardcoded rules");

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

                // Add to logs
                genericLogs.Add($"Matched section pattern: \"{headerText}\"");

                technicalLogs.Add($"[Pattern Matching] Line {lineNumber}: Major section detected");
                technicalLogs.Add($"  - Header text: \"{headerText}\"");
                technicalLogs.Add($"  - Normalized form: \"{NormalizeText(headerText)}\"");
                technicalLogs.Add($"  - Decision: Assign Level 1 (major section)");

                patternsMatched++;

                var item = CreateHierarchyItem(header, 1, rootItem, lastItemAtLevel);
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

                // Technical logs only (too verbose for generic)
                technicalLogs.Add($"[Decision] Line {lineNumber}: Skipped (subsection within Directors' report) → \"{headerText}\"");
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

                    // Add to logs
                    technicalLogs.Add($"[Pattern Matching] Line {lineNumber}: Note header detected");
                    technicalLogs.Add($"  - Header text: \"{headerText}\"");
                    technicalLogs.Add($"  - data-number: \"{header.DataNumber}\"");
                    technicalLogs.Add($"  - Determined level: {noteLevel}");
                    technicalLogs.Add($"  - Parent context: \"{parent.LinkName}\"");

                    patternsMatched++;

                    var item = CreateHierarchyItem(header, noteLevel, parent, lastItemAtLevel);
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

            // Technical logs only (too verbose for generic)
            technicalLogs.Add($"[Decision] Line {lineNumber}: Skipped (in-section content) → \"{headerText}\"");
        }

        // Generation complete - add building hierarchy message
        genericLogs.Add("Building hierarchy structure...");
        technicalLogs.Add("[Hierarchy Building] Creating hierarchy tree from matched sections");

        // Log confidence distribution
        var allItems = new List<HierarchyItem>();
        CollectAllItems(rootItem, allItems);

        var highConf = allItems.Count(i => i.Level > 0 && i.ConfidenceScore >= 0.9);
        var medConf = allItems.Count(i => i.Level > 0 && i.ConfidenceScore >= 0.6 && i.ConfidenceScore < 0.9);
        var lowConf = allItems.Count(i => i.Level > 0 && i.ConfidenceScore < 0.6);
        var totalItems = allItems.Count(i => i.Level > 0); // Exclude root

        _logger.LogInformation("[RuleBasedHierarchy] Generated hierarchy: {TopLevel} top-level sections, {Total} total items",
            rootItem.SubItems.Count, CountItems(rootItem));

        _logger.LogInformation("[RuleBasedHierarchy] Confidence distribution: " +
            "High (≥0.9): {High}/{Total} ({HighPct:F1}%), " +
            "Medium (0.6-0.9): {Medium}/{Total} ({MedPct:F1}%), " +
            "Low (<0.6): {Low}/{Total} ({LowPct:F1}%)",
            highConf, totalItems, (highConf * 100.0 / totalItems),
            medConf, totalItems, (medConf * 100.0 / totalItems),
            lowConf, totalItems, (lowConf * 100.0 / totalItems));

        // Log uncertain items (confidence < 0.9)
        var uncertainItems = allItems
            .Where(i => i.Level > 0 && i.ConfidenceScore < 0.9)
            .OrderBy(i => i.ConfidenceScore)
            .Take(10)
            .ToList();

        if (uncertainItems.Any())
        {
            _logger.LogInformation("[RuleBasedHierarchy] Top {Count} uncertain decisions (for review):",
                Math.Min(10, uncertainItems.Count));

            foreach (var item in uncertainItems)
            {
                _logger.LogInformation("  - {Name} (Level {Level}, Confidence: {Conf:F2}): {Reasoning}",
                    item.LinkName, item.Level, item.ConfidenceScore, item.Reasoning);
            }
        }

        stopwatch.Stop();

        // Calculate final statistics
        var itemsCreated = allItems.Count(i => i.Level > 0); // Exclude root
        var maxDepth = allItems.Any() ? allItems.Max(i => i.Level) : 0;

        // Add final summary to logs
        genericLogs.Add($"Created {itemsCreated} hierarchy items");
        genericLogs.Add($"Hierarchy depth: {maxDepth} levels");
        genericLogs.Add($"Patterns matched: {patternsMatched}/{headers.Count} ({(headers.Count > 0 ? (patternsMatched * 100.0 / headers.Count) : 0):F1}%)");
        genericLogs.Add("✓ Generation complete!");

        technicalLogs.Add($"[Statistics] Total items: {itemsCreated}, Headers processed: {headers.Count}, Patterns matched: {patternsMatched}");
        technicalLogs.Add($"[Statistics] Max depth: {maxDepth} levels");
        technicalLogs.Add($"[Hierarchy Building] Added {rootItem.SubItems.Count} top-level items");
        technicalLogs.Add($"[Performance] Generation completed in {stopwatch.ElapsedMilliseconds}ms");
        technicalLogs.Add("✓ Generation complete");

        // Return comprehensive result
        return new GenerationResult
        {
            Root = rootItem,
            GenericLogs = genericLogs,
            TechnicalLogs = technicalLogs,
            Statistics = new GenerationStatistics
            {
                HeadersProcessed = headers.Count,
                ItemsCreated = itemsCreated,
                MaxDepth = maxDepth,
                PatternsMatched = patternsMatched,
                DurationMs = stopwatch.ElapsedMilliseconds
            }
        };
    }

    /// <summary>
    /// Collects all items from hierarchy tree into a flat list
    /// </summary>
    private void CollectAllItems(HierarchyItem item, List<HierarchyItem> accumulator)
    {
        accumulator.Add(item);
        foreach (var subItem in item.SubItems)
        {
            CollectAllItems(subItem, accumulator);
        }
    }

    /// <summary>
    /// Checks if header text matches a major section keyword.
    /// Uses learned patterns if available, falls back to hardcoded keywords.
    /// </summary>
    private bool IsMajorSection(string headerText)
    {
        // If patterns loaded, use learned vocabulary
        if (_patterns != null)
        {
            return IsKnownLevel1Section(headerText);
        }

        // Fallback to hardcoded keywords if patterns not available
        return MajorSectionKeywords.Any(keyword =>
            headerText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if header appears in Level 1 learned patterns with sufficient confidence
    /// </summary>
    private bool IsKnownLevel1Section(string headerText)
    {
        if (_patterns?.LevelProfiles == null || !_patterns.LevelProfiles.ContainsKey(1))
            return false;

        var level1Profile = _patterns.LevelProfiles[1];
        var normalizedHeader = NormalizeText(headerText);

        // Check if this header appears in Level 1 common headers
        // Use frequency threshold: appears in at least 1% of hierarchies (269 hierarchies → ~3 occurrences)
        var isCommon = level1Profile.CommonHeaders.Any(h =>
            NormalizeText(h.HeaderText).Equals(normalizedHeader, StringComparison.Ordinal) &&
            h.Frequency >= 0.01);

        if (isCommon)
        {
            _logger.LogDebug("[RuleBasedHierarchy] Recognized Level 1 section from patterns: {Header}", headerText);
            return true;
        }

        // Also check section vocabulary for high-confidence Level 1 sections
        // This catches sections that might not be in top common headers but have clear Level 1 classification
        var vocabMatch = _patterns.CommonSections?
            .FirstOrDefault(s => NormalizeText(s.HeaderText).Equals(normalizedHeader, StringComparison.Ordinal));

        if (vocabMatch != null && vocabMatch.MostCommonLevel == 1 && vocabMatch.Confidence >= 0.7)
        {
            _logger.LogDebug("[RuleBasedHierarchy] Recognized Level 1 section from vocabulary: {Header} (confidence: {Conf:F2})",
                headerText, vocabMatch.Confidence);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Normalizes text for consistent pattern matching.
    /// Uses most aggressive approach: removes ALL punctuation and spaces.
    /// This ensures maximum robustness in matching header text variations.
    /// Examples: "Director's report" → "directorsreport", "Directors' Report" → "directorsreport"
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Convert to lowercase
        var normalized = text.Trim().ToLowerInvariant();

        // Remove ALL punctuation (apostrophes, quotes, commas, periods, hyphens, etc.)
        normalized = Regex.Replace(normalized, @"['`"",:;!?()\[\]{}\*\.\-]", string.Empty);

        // Remove ALL spaces (most aggressive matching)
        normalized = normalized.Replace(" ", string.Empty);

        return normalized;
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
    /// Uses learned patterns if available, falls back to regex-based detection.
    /// Returns 0 if not a recognized note pattern.
    /// </summary>
    private int DetermineNoteLevel(string dataNumber)
    {
        if (string.IsNullOrEmpty(dataNumber))
            return 0;

        // If patterns loaded, use learned numbering patterns first
        if (_patterns?.NumberingPatterns != null)
        {
            // Try exact match
            if (_patterns.NumberingPatterns.TryGetValue(dataNumber, out var pattern))
            {
                // Use learned pattern if confidence is reasonable (≥0.5)
                if (pattern.Confidence >= 0.5)
                {
                    _logger.LogDebug("[RuleBasedHierarchy] Using learned pattern for {DataNumber} → Level {Level} (confidence: {Conf:F2})",
                        dataNumber, pattern.MostCommonLevel, pattern.Confidence);
                    return pattern.MostCommonLevel;
                }
            }
        }

        // Fallback to regex-based detection (existing logic)
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
    /// Creates a HierarchyItem from HeaderInfo with confidence scoring
    /// </summary>
    private HierarchyItem CreateHierarchyItem(
        HierarchyGeneratorService.HeaderInfo header,
        int level,
        HierarchyItem parent,
        Dictionary<int, HierarchyItem> lastItemAtLevel)
    {
        var normalizedId = FilenameUtils.NormalizeFileName(header.Text);

        // Calculate confidence score based on structural patterns
        var (confidence, flags, reasoning) = CalculateConfidence(header, level, lastItemAtLevel);

        return new HierarchyItem
        {
            Id = normalizedId,
            Level = level,
            LinkName = header.Text.Trim(),
            DataRef = $"{normalizedId}.xml",
            Path = parent.Path,
            SubItems = new List<HierarchyItem>(),

            // Legacy field (kept for compatibility, convert 0.0-1.0 to 0-100)
            Confidence = (int)(confidence * 100),

            // New confidence fields
            ConfidenceScore = confidence,
            UncertaintyFlags = flags,
            Reasoning = reasoning,
            WordCount = header.WordCount,
            ChildCount = header.ChildHeaderCount
        };
    }

    /// <summary>
    /// Calculates confidence score for a hierarchy decision using ONLY pattern-based logic.
    /// NO HARDCODED STRINGS - all scoring based on structural patterns.
    /// </summary>
    private (double confidence, List<UncertaintyFlag> flags, string reasoning) CalculateConfidence(
        HierarchyGeneratorService.HeaderInfo header,
        int level,
        Dictionary<int, HierarchyItem> lastItemAtLevel)
    {
        var score = 0.5; // Base score
        var flags = new List<UncertaintyFlag>();
        var reasons = new List<string>();

        // PATTERN 1: Data-number presence and format
        if (!string.IsNullOrEmpty(header.DataNumber))
        {
            score += 0.25;
            reasons.Add("Has data-number");

            // Check if it's a standard format
            if (IsStandardNumberingFormat(header.DataNumber))
            {
                score += 0.15;
                reasons.Add("Standard numbering format");

                // Penalize roman numerals (less common than numeric)
                if (Regex.IsMatch(header.DataNumber, @"^\([ivxlcdm]+\)$", RegexOptions.IgnoreCase))
                {
                    flags.Add(UncertaintyFlag.UnusualNumbering);
                    score -= 0.15;
                    reasons.Add("Roman numeral format (less common)");
                }
                // Penalize single letters (less structured than numeric)
                else if (Regex.IsMatch(header.DataNumber, @"^\([a-z]\)$", RegexOptions.IgnoreCase))
                {
                    score -= 0.05;
                    reasons.Add("Letter format (common but less structured)");
                }
            }
            else
            {
                flags.Add(UncertaintyFlag.UnusualNumbering);
                score -= 0.15;
                reasons.Add("Unusual numbering format");
            }
        }
        else
        {
            flags.Add(UncertaintyFlag.NoDataNumber);
            score -= 0.15;
            reasons.Add("Missing data-number");
        }

        // PATTERN 2: Content length (universal metric)
        var wordCount = header.WordCount;
        if (wordCount >= 500 && wordCount <= 3000)
        {
            score += 0.2;
            reasons.Add("Typical content length");
        }
        else if (wordCount >= 200 && wordCount < 500)
        {
            score += 0.15;
            reasons.Add("Moderate content length");
        }
        else if (wordCount > 5000)
        {
            flags.Add(UncertaintyFlag.LongContent);
            score -= 0.15;
            reasons.Add($"Long content ({wordCount} words)");
        }
        else if (wordCount < 100 && wordCount > 0)
        {
            flags.Add(UncertaintyFlag.ShortContent);
            score -= 0.2;
            reasons.Add($"Short content ({wordCount} words)");
        }

        // PATTERN 3: Nesting depth
        if (level > 4)
        {
            flags.Add(UncertaintyFlag.DeepNesting);
            score -= 0.2;
            reasons.Add($"Deep nesting (level {level})");
        }
        else if (level <= 2)
        {
            score += 0.1;
            reasons.Add("Shallow hierarchy");
        }
        else if (level == 4)
        {
            score -= 0.05;
            reasons.Add("Level 4 depth (consider if necessary)");
        }

        // PATTERN 4: Child count (structural metric)
        if (header.ChildHeaderCount > 0)
        {
            score += 0.12;
            reasons.Add($"Has {header.ChildHeaderCount} children");
        }
        else if (level <= 2 && header.ChildHeaderCount == 0)
        {
            score -= 0.05;
            reasons.Add("Top-level with no children (unusual)");
        }

        // PATTERN 5: Sibling context (check if isolated)
        var hasPrevSibling = lastItemAtLevel.ContainsKey(level);
        // Note: We can't easily determine "next sibling" during generation,
        // so we only check for previous sibling as a proxy for isolation
        if (!hasPrevSibling && level > 1)
        {
            flags.Add(UncertaintyFlag.IsolatedHeader);
            score -= 0.1;
            reasons.Add("First item at this level (check if isolated)");
        }

        // Clamp to valid range
        var finalScore = Math.Clamp(score, 0.0, 1.0);
        var reasoning = string.Join("; ", reasons);

        return (finalScore, flags, reasoning);
    }

    /// <summary>
    /// Checks if a data-number matches standard numbering patterns
    /// </summary>
    private bool IsStandardNumberingFormat(string dataNumber)
    {
        // Standard patterns: "1", "1.", "1.1", "1.1.1", "(a)", "(i)", "Note 1"
        return Regex.IsMatch(
            dataNumber,
            @"^(\d+\.?|\d+\.\d+(\.\d+)*|\([a-z]\)|\([ivxlcdm]+\)|Note \d+)$",
            RegexOptions.IgnoreCase
        );
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

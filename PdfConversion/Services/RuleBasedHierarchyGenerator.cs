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
    private readonly NotePatternDatabase? _notePatterns;
    private readonly ComprehensivePatternDatabase? _comprehensivePatterns;
    private readonly ContentClassificationService? _classificationService;

    // Configuration: Thresholds for numbering scheme detection
    private const double NumberingSchemeThreshold = 0.30; // 30% of headers must have data-numbers
    private const double MajorSectionThreshold = 0.80;    // 80% of pattern occurrences at Level 1

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
        _notePatterns = LoadNotePatterns();
        _comprehensivePatterns = LoadComprehensivePatterns();
        _classificationService = _comprehensivePatterns != null
            ? new ContentClassificationService(
                LoggerFactory.Create(builder => builder.AddConsole())
                    .CreateLogger<ContentClassificationService>(),
                _comprehensivePatterns)
            : null;
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
    /// Loads or mines note patterns from training data.
    /// Mines patterns on-demand if not cached, then returns the database.
    /// </summary>
    private NotePatternDatabase? LoadNotePatterns()
    {
        try
        {
            var path = Path.Combine("data", "patterns", "note-patterns.json");

            // Try to load cached patterns first
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var patterns = JsonSerializer.Deserialize<NotePatternDatabase>(json, options);

                if (patterns != null && patterns.Patterns.Count > 0)
                {
                    _logger.LogInformation("[RuleBasedHierarchy] Loaded note pattern database: {Patterns} patterns from {Files} hierarchies",
                        patterns.Patterns.Count,
                        patterns.TotalHierarchiesAnalyzed);
                    return patterns;
                }
            }

            // Mine patterns if not cached
            _logger.LogInformation("[RuleBasedHierarchy] Note patterns not cached, mining from training data...");

            // Create a logger for NotePatternMiner using LoggerFactory
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var minerLogger = loggerFactory.CreateLogger<NotePatternMiner>();

            var miner = new NotePatternMiner(minerLogger);
            var minedPatterns = miner.MinePatterns();

            // Cache the results
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonToSave = JsonSerializer.Serialize(minedPatterns, jsonOptions);
            File.WriteAllText(path, jsonToSave);

            _logger.LogInformation("[RuleBasedHierarchy] Cached note patterns to {Path}", path);

            return minedPatterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RuleBasedHierarchy] Failed to load/mine note patterns, proceeding without notes context");
            return null;
        }
    }

    /// <summary>
    /// Loads or mines comprehensive patterns from training data.
    /// Mines patterns on-demand if not cached, then returns the database.
    /// </summary>
    private ComprehensivePatternDatabase? LoadComprehensivePatterns()
    {
        try
        {
            var path = Path.Combine("data", "patterns", "comprehensive-patterns.json");

            // Try to load cached patterns first
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var patterns = JsonSerializer.Deserialize<ComprehensivePatternDatabase>(json, options);

                if (patterns != null && patterns.Patterns.Count > 0)
                {
                    _logger.LogInformation("[RuleBasedHierarchy] Loaded comprehensive pattern database: {Patterns} patterns from {Files} hierarchies",
                        patterns.Patterns.Count,
                        patterns.TotalHierarchiesAnalyzed);
                    return patterns;
                }
            }

            // Mine patterns if not cached
            _logger.LogInformation("[RuleBasedHierarchy] Comprehensive patterns not cached, mining from training data...");

            // Create a logger for ComprehensivePatternMiner using LoggerFactory
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var minerLogger = loggerFactory.CreateLogger<ComprehensivePatternMiner>();

            var miner = new ComprehensivePatternMiner(minerLogger);
            var minedPatterns = miner.MinePatterns();

            // Cache the results
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var jsonToSave = JsonSerializer.Serialize(minedPatterns, jsonOptions);
            File.WriteAllText(path, jsonToSave);

            _logger.LogInformation("[RuleBasedHierarchy] Cached comprehensive patterns to {Path}", path);

            return minedPatterns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RuleBasedHierarchy] Failed to load/mine comprehensive patterns, proceeding without comprehensive database");
            return null;
        }
    }

    /// <summary>
    /// Detects if document uses explicit numbering scheme (data-number attributes).
    /// If >= 30% of headers have data-numbers, the document is considered to use numbering.
    /// </summary>
    private bool DetectNumberingScheme(List<ClassifiedSection> sections)
    {
        if (sections.Count == 0) return false;

        var totalSections = sections.Count;
        var numberedSections = sections.Count(s => !string.IsNullOrEmpty(s.DataNumber));
        var percentage = (double)numberedSections / totalSections;

        // If >= 30% of headers have data-numbers, document uses numbering scheme
        var usesNumbering = percentage >= NumberingSchemeThreshold;

        _logger.LogInformation("[Numbering Detection] {Numbered}/{Total} sections have data-numbers ({Pct:F1}%)",
            numberedSections, totalSections, percentage * 100);
        _logger.LogInformation("[Numbering Detection] Document uses explicit numbering: {Uses}",
            usesNumbering ? "YES" : "NO");

        return usesNumbering;
    }

    /// <summary>
    /// Checks if a pattern is a "major section" pattern (typically Level 1).
    /// A pattern is major if:
    /// 1. It appears at Level 1 in >= 80% of cases, OR
    /// 2. It's a structural parent (5+ other patterns list it as typical parent)
    /// This is GENERIC - no hard-coded section names.
    /// </summary>
    private bool IsMajorSectionPattern(SectionPattern pattern)
    {
        // Check 1: Typically at Level 1?
        if (pattern.LevelFrequency.TryGetValue(1, out var level1Count))
        {
            var totalCount = pattern.LevelFrequency.Values.Sum();
            var level1Percentage = (double)level1Count / totalCount;

            if (level1Percentage >= MajorSectionThreshold)
            {
                _logger.LogDebug("[Major Section] Pattern \"{Pattern}\" typically L1 ({Pct:F1}% = {L1}/{Total})",
                    pattern.NormalizedTitle, level1Percentage * 100, level1Count, totalCount);
                return true;
            }
        }

        // Check 2: Structural parent?
        if (_comprehensivePatterns != null && IsStructuralParent(pattern, _comprehensivePatterns))
        {
            _logger.LogInformation("[Major Section] Pattern \"{Pattern}\" is structural parent (keeps despite no L1 dominance)",
                pattern.NormalizedTitle);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a pattern is a structural parent by counting how many other patterns
    /// list it as a typical parent in the training data.
    /// A pattern that is commonly a parent to other sections is structurally important
    /// even if it doesn't always appear at Level 1.
    /// </summary>
    private bool IsStructuralParent(SectionPattern pattern, ComprehensivePatternDatabase database)
    {
        if (database?.Patterns == null || !database.Patterns.Any())
            return false;

        var normalizedTitle = pattern.NormalizedTitle;

        // Count how many OTHER patterns list this pattern as a typical parent
        var childCount = 0;
        foreach (var otherPattern in database.Patterns)
        {
            if (otherPattern.NormalizedTitle == normalizedTitle)
                continue; // Skip self

            // Check if this pattern is in the typical parents
            if (otherPattern.TypicalParents?.Any(p => p.ParentNormalizedTitle == normalizedTitle) == true)
            {
                childCount++;
            }
        }

        // If 5+ patterns list this as their parent, it's a structural section
        const int StructuralParentThreshold = 5;

        if (childCount >= StructuralParentThreshold)
        {
            _logger.LogDebug("[Structural Parent Check] Pattern \"{Pattern}\" is parent to {Count} other patterns (threshold: {Threshold})",
                normalizedTitle, childCount, StructuralParentThreshold);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generates hierarchy using two-pass rule-based logic with dual-level logging
    /// Pass 1: Content Classification - identify WHAT each section is
    /// Pass 2: Hierarchy Building - determine WHERE each section belongs
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
        technicalLogs.Add("[RuleBasedHierarchyGenerator] Starting generation with two-pass architecture");
        technicalLogs.Add($"[Input] {headers.Count} headers found in normalized XML");
        technicalLogs.Add($"[Pattern Database] Using {(_patterns != null ? "learned patterns" : "hardcoded rules")}");

        if (_comprehensivePatterns != null)
        {
            genericLogs.Add("Two-pass architecture enabled: Content Classification + Hierarchy Building");
            technicalLogs.Add($"[Comprehensive Patterns] {_comprehensivePatterns.Patterns.Count} section patterns available");
            technicalLogs.Add($"[Comprehensive Patterns] {_comprehensivePatterns.TotalHierarchiesAnalyzed} training hierarchies analyzed");
        }
        else
        {
            genericLogs.Add("Legacy mode: Using data-number + Level 1 patterns only");
            technicalLogs.Add("[Comprehensive Patterns] Not available, using legacy logic");
        }

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
        _logger.LogInformation("[RuleBasedHierarchy] Mode: {Mode}",
            _comprehensivePatterns != null ? "Two-pass (comprehensive)" : "Legacy (data-number + Level 1)");

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

        // Notes context tracking
        var inNotesContext = false;
        var notesParentLevel = 0;
        var notesParentDataNumber = "";
        HierarchyItem? notesParentItem = null;

        // PASS 1: Content Classification (if comprehensive patterns available)
        List<ClassifiedSection>? classifiedSections = null;
        if (_classificationService != null && _comprehensivePatterns != null)
        {
            genericLogs.Add("[Pass 1] Classifying content against pattern database...");
            technicalLogs.Add("[Pass 1] Content Classification starting");

            classifiedSections = _classificationService.ClassifyHeaders(headers);

            var matchedCount = classifiedSections.Count(s => s.MatchedPattern != null);
            genericLogs.Add($"[Pass 1] Classified {matchedCount}/{headers.Count} sections ({(matchedCount * 100.0 / headers.Count):F1}% match rate)");
            technicalLogs.Add($"[Pass 1] Classification complete: {matchedCount}/{headers.Count} matched");

            // Log Pass 1 results to technical log
            for (int i = 0; i < classifiedSections.Count; i++)
            {
                var section = classifiedSections[i];
                section.LineNumber = i + 1;

                if (section.MatchedPattern != null)
                {
                    var mostCommonLevel = section.MatchedPattern.MostCommonLevel;
                    var levelDist = string.Join(", ", section.MatchedPattern.LevelFrequency
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(2)
                        .Select(kvp => $"L{kvp.Key}:{kvp.Value}x"));

                    technicalLogs.Add($"[Pass 1] Line {i + 1}: \"{section.HeaderText}\"");
                    technicalLogs.Add($"  - Matched: \"{section.MatchedPattern.NormalizedTitle}\" (conf: {section.MatchConfidence:F2})");
                    technicalLogs.Add($"  - Typical levels: {levelDist}");

                    if (section.MatchedPattern.TypicalParents.Any())
                    {
                        var topParent = section.MatchedPattern.TypicalParents.First();
                        technicalLogs.Add($"  - Typical parent: \"{topParent.ParentNormalizedTitle}\" ({topParent.Frequency}x)");
                    }
                }
                else
                {
                    technicalLogs.Add($"[Pass 1] Line {i + 1}: \"{section.HeaderText}\" - No pattern match");
                }
            }

            genericLogs.Add("[Pass 2] Building hierarchy structure...");
            technicalLogs.Add("[Pass 2] Hierarchy Building starting");
        }

        // Detect numbering scheme for filtering (outside the classification block)
        bool usesNumberingScheme = false;
        if (classifiedSections != null && _comprehensivePatterns != null)
        {
            usesNumberingScheme = DetectNumberingScheme(classifiedSections);

            if (usesNumberingScheme)
            {
                _logger.LogInformation("[Pass 2] Numbering scheme detected - filtering to numbered headers only");
                genericLogs.Add("[Pass 2] Document uses numbering - filtering unnumbered headers");
                technicalLogs.Add("[Numbering Scheme] Document uses explicit numbering");
                technicalLogs.Add("[Numbering Scheme] Only headers with data-numbers will create hierarchy items");
                technicalLogs.Add("[Numbering Scheme] Exception: Major section patterns kept even without data-numbers");
            }
        }

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var lineNumber = i + 1;
            var headerText = header.Text.Trim();
            ClassifiedSection? classifiedSection = classifiedSections?[i];

            // TWO-PASS MODE: Use comprehensive pattern matching if available
            if (classifiedSection != null && _comprehensivePatterns != null)
            {
                // FILTER: If document uses numbering, skip headers without data-numbers
                if (usesNumberingScheme && string.IsNullOrEmpty(classifiedSection.DataNumber))
                {
                    // EXCEPTION: Keep major section patterns even without data-numbers
                    if (classifiedSection.MatchedPattern != null && IsMajorSectionPattern(classifiedSection.MatchedPattern))
                    {
                        // Determine why it's considered major (L1 frequency or structural parent)
                        var isL1Dominant = false;
                        var l1Percentage = 0.0;
                        if (classifiedSection.MatchedPattern.LevelFrequency.TryGetValue(1, out var l1Count))
                        {
                            var totalCount = classifiedSection.MatchedPattern.LevelFrequency.Values.Sum();
                            l1Percentage = (double)l1Count / totalCount;
                            isL1Dominant = l1Percentage >= MajorSectionThreshold;
                        }

                        var isStructuralParent = !isL1Dominant && _comprehensivePatterns != null &&
                            IsStructuralParent(classifiedSection.MatchedPattern, _comprehensivePatterns);

                        if (isL1Dominant)
                        {
                            _logger.LogInformation("[Pass 2] Line {Line}: L1-dominant section - KEEPING despite no data-number → \"{Header}\"",
                                lineNumber, headerText);
                            technicalLogs.Add($"[Pass 2] Line {lineNumber}: L1-dominant section - kept despite no data-number");
                            technicalLogs.Add($"  - Pattern: \"{classifiedSection.MatchedPattern.NormalizedTitle}\"");
                            technicalLogs.Add($"  - Level 1 frequency: {l1Percentage:F1}% (threshold: {MajorSectionThreshold:F1}%)");
                        }
                        else if (isStructuralParent)
                        {
                            _logger.LogInformation("[Pass 2] Line {Line}: Structural section - KEEPING despite no data-number → \"{Header}\"",
                                lineNumber, headerText);
                            technicalLogs.Add($"[Pass 2] Line {lineNumber}: Structural section (parent to many) - kept despite no data-number");
                            technicalLogs.Add($"  - Pattern: \"{classifiedSection.MatchedPattern.NormalizedTitle}\"");
                            technicalLogs.Add($"  - Reason: Common parent in training data (5+ child patterns)");
                        }
                        else
                        {
                            _logger.LogInformation("[Pass 2] Line {Line}: Major section pattern - KEEPING despite no data-number → \"{Header}\"",
                                lineNumber, headerText);
                            technicalLogs.Add($"[Pass 2] Line {lineNumber}: Major section (pattern match) - kept despite no data-number");
                            technicalLogs.Add($"  - Pattern: \"{classifiedSection.MatchedPattern.NormalizedTitle}\"");
                        }
                        // Continue processing this header (do not skip)
                    }
                    else
                    {
                        _logger.LogInformation("[Pass 2] Line {Line}: SKIPPED (no data-number in numbered document) → \"{Header}\"",
                            lineNumber, headerText);
                        technicalLogs.Add($"[Pass 2] Line {lineNumber}: SKIPPED (in-section content) → \"{headerText}\"");
                        continue; // Skip this header entirely
                    }
                }

                // Get previous classified section for context
                ClassifiedSection? previousSection = i > 0 ? classifiedSections![i - 1] : null;

                // Determine level using multi-signal analysis
                var (level, signal, reasoning) = DetermineLevelForSection(
                    classifiedSection,
                    previousSection,
                    lastItemAtLevel);

                // Store determination results
                classifiedSection.DeterminedLevel = level;
                classifiedSection.DeterminationSignal = signal;
                classifiedSection.DeterminationReasoning = reasoning;

                // Log Pass 2 decision
                technicalLogs.Add($"[Pass 2] Line {lineNumber}: \"{headerText}\"");
                technicalLogs.Add($"  - Signal used: {signal}");
                technicalLogs.Add($"  - Reasoning: {reasoning}");
                technicalLogs.Add($"  - Decision: Assign Level {level}");

                // Find parent
                var parent = FindParentForSection(classifiedSection, level, lastItemAtLevel, rootItem);
                technicalLogs.Add($"  - Parent: \"{parent.LinkName}\"");

                // Create and add item
                patternsMatched++;
                var item = CreateHierarchyItem(header, level, parent, lastItemAtLevel, classifiedSection);
                parent.SubItems.Add(item);
                lastItemAtLevel[level] = item;

                // Update major section context if this is level 1
                if (level == 1)
                {
                    currentMajorSection = item;
                    inDirectorsReport = IsDirectorsReport(headerText);
                    inNotesSection = IsNotesSection(headerText);
                    if (inDirectorsReport) directorsReportStartLine = lineNumber;
                }

                // Clear lower levels
                ClearLowerLevels(lastItemAtLevel, level);
                continue;
            }

            // LEGACY MODE: Use data-number + Level 1 pattern matching
            // PRIORITY 1: Use data-number attribute if present
            if (!string.IsNullOrEmpty(header.DataNumber))
            {
                var dataNumber = header.DataNumber;
                var level = ParseDataNumberLevel(dataNumber);
                var isKnownNotePattern = false;

                // Check if we should adjust level based on notes context
                if (inNotesContext && _notePatterns != null)
                {
                    var normalizedTitle = NormalizeNoteTitle(headerText);

                    // Check if this matches a known note pattern
                    if (_notePatterns.IsKnownNotePattern(normalizedTitle, minFrequencyThreshold: 0.01))
                    {
                        // This is a note subsection - assign level relative to notes parent
                        level = notesParentLevel + 1;
                        isKnownNotePattern = true;

                        _logger.LogInformation("[Notes Context] Line {Line}: Note section detected → \"{Header}\"",
                            lineNumber, headerText);
                        _logger.LogInformation("[Notes Context]   - Matched known note pattern: \"{Pattern}\"", normalizedTitle);
                        _logger.LogInformation("[Notes Context]   - Assigned level: {Level} (notes parent + 1)", level);

                        technicalLogs.Add($"[Notes Context] Line {lineNumber}: Known note pattern detected");
                        technicalLogs.Add($"  - Header text: \"{headerText}\"");
                        technicalLogs.Add($"  - Normalized: \"{normalizedTitle}\"");
                        technicalLogs.Add($"  - Notes parent level: {notesParentLevel}");
                        technicalLogs.Add($"  - Decision: Assign Level {level} (note under parent)");
                    }
                    // Check if we're exiting notes context (higher or equal level to notes parent)
                    else if (level <= notesParentLevel)
                    {
                        inNotesContext = false;
                        notesParentItem = null;
                        _logger.LogInformation("[Notes Context] Exiting notes mode");
                        _logger.LogInformation("[Notes Context]   - Trigger: data-number=\"{DataNumber}\" at level {Level} (>= notes parent level {ParentLevel})",
                            dataNumber, level, notesParentLevel);

                        technicalLogs.Add($"[Notes Context] Exiting notes context");
                        technicalLogs.Add($"  - Trigger: Found higher/equal level section (level {level} >= {notesParentLevel})");
                    }
                }

                if (level > 0)
                {
                    _logger.LogInformation("[RuleBasedHierarchy] Line {Line}: DATA-NUMBER HIERARCHY Level {Level} (data-number=\"{DataNumber}\") → \"{Header}\"",
                        lineNumber, level, dataNumber, headerText);

                    // Find parent - use notes parent if this is a known note pattern
                    HierarchyItem parent;
                    if (isKnownNotePattern && notesParentItem != null)
                    {
                        parent = notesParentItem;
                        _logger.LogInformation("[Notes Context]   - Using notes parent: \"{Parent}\"", parent.LinkName);
                    }
                    else if (level == 1)
                    {
                        parent = rootItem;
                    }
                    else
                    {
                        parent = FindParentByDataNumber(dataNumber, lastItemAtLevel, rootItem);
                    }

                    // Add to logs
                    genericLogs.Add($"Hierarchy from data-number: Level {level} - \"{headerText}\"");
                    technicalLogs.Add($"[Data-Number Analysis] Line {lineNumber}: data-number=\"{dataNumber}\"");
                    technicalLogs.Add($"  - Header text: \"{headerText}\"");
                    technicalLogs.Add($"  - Determined level: {level}");
                    technicalLogs.Add($"  - Parent context: \"{parent.LinkName}\"");

                    patternsMatched++;

                    var item = CreateHierarchyItem(header, level, parent, lastItemAtLevel, classifiedSection);
                    parent.SubItems.Add(item);
                    lastItemAtLevel[level] = item;

                    // Update major section context if this is level 1
                    if (level == 1)
                    {
                        currentMajorSection = item;
                        inDirectorsReport = IsDirectorsReport(headerText);
                        inNotesSection = IsNotesSection(headerText);
                        if (inDirectorsReport) directorsReportStartLine = lineNumber;
                    }

                    // Check if this section triggers notes context mode
                    if (IsNotesToSection(headerText))
                    {
                        inNotesContext = true;
                        notesParentLevel = level;
                        notesParentDataNumber = dataNumber;
                        notesParentItem = item;

                        _logger.LogInformation("[Notes Context] Entering notes mode");
                        _logger.LogInformation("[Notes Context]   - Trigger: \"{Header}\"", headerText);
                        _logger.LogInformation("[Notes Context]   - Parent level: {Level}", notesParentLevel);
                        _logger.LogInformation("[Notes Context]   - Parent data-number: \"{DataNumber}\"", notesParentDataNumber);

                        technicalLogs.Add($"[Notes Context] Entering notes context mode");
                        technicalLogs.Add($"  - Trigger section: \"{headerText}\"");
                        technicalLogs.Add($"  - Notes parent level: {notesParentLevel}");
                        technicalLogs.Add($"  - Notes parent data-number: \"{notesParentDataNumber}\"");
                    }

                    // Clear lower levels
                    ClearLowerLevels(lastItemAtLevel, level);
                    continue;
                }
            }

            // PRIORITY 2: Check if this is a major section boundary (text pattern matching)
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

                var item = CreateHierarchyItem(header, 1, rootItem, lastItemAtLevel, classifiedSection);
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

                    var item = CreateHierarchyItem(header, noteLevel, parent, lastItemAtLevel, classifiedSection);
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
    /// Parses data-number attribute to determine hierarchy level.
    /// Examples: "1" → 1, "2.1" → 2, "4.6.2" → 3, "note 1" → 3
    /// </summary>
    private int ParseDataNumberLevel(string dataNumber)
    {
        if (string.IsNullOrEmpty(dataNumber))
            return 0;

        // Normalize: trim and lowercase
        var normalized = dataNumber.Trim().ToLowerInvariant();

        // Special case: "note N" format (common in financial statements)
        if (normalized.StartsWith("note "))
        {
            // Notes are typically level 3 (under "Notes to financial statements")
            return 3;
        }

        // Count dots to determine depth
        // "1" = 0 dots → Level 1
        // "2.1" = 1 dot → Level 2
        // "4.6.2" = 2 dots → Level 3
        var dotCount = dataNumber.Count(c => c == '.');
        return dotCount + 1;
    }

    /// <summary>
    /// Finds the parent HierarchyItem based on data-number prefix.
    /// Example: "2.1" should find parent with data-number "2"
    ///          "4.6.2" should find parent with data-number "4.6"
    /// </summary>
    private HierarchyItem FindParentByDataNumber(string dataNumber, Dictionary<int, HierarchyItem> lastItemAtLevel, HierarchyItem rootItem)
    {
        if (string.IsNullOrEmpty(dataNumber))
            return rootItem;

        // Calculate this item's level
        var level = ParseDataNumberLevel(dataNumber);

        // Level 1 items always have root as parent
        if (level <= 1)
            return rootItem;

        // Extract parent prefix by removing the last segment
        // "2.1" → "2", "4.6.2" → "4.6"
        var lastDotIndex = dataNumber.LastIndexOf('.');
        if (lastDotIndex <= 0)
            return rootItem;

        var parentPrefix = dataNumber.Substring(0, lastDotIndex);

        // Search for parent with matching data-number
        var parentLevel = level - 1;
        if (lastItemAtLevel.TryGetValue(parentLevel, out var lastParent))
        {
            // Check if last item at parent level has matching data-number
            if (lastParent.DataNumber != null && lastParent.DataNumber.Equals(parentPrefix, StringComparison.Ordinal))
            {
                _logger.LogDebug("[RuleBasedHierarchy] Found parent by data-number: \"{Parent}\" (data-number=\"{ParentNum}\") for child data-number=\"{ChildNum}\"",
                    lastParent.LinkName, lastParent.DataNumber, dataNumber);
                return lastParent;
            }

            // If not exact match, search siblings and ancestors
            var candidate = FindItemByDataNumber(lastParent, parentPrefix);
            if (candidate != null)
            {
                _logger.LogDebug("[RuleBasedHierarchy] Found parent by search: \"{Parent}\" (data-number=\"{ParentNum}\") for child data-number=\"{ChildNum}\"",
                    candidate.LinkName, candidate.DataNumber, dataNumber);
                return candidate;
            }
        }

        // Fallback: search all items at parent level
        for (int searchLevel = parentLevel; searchLevel >= 1; searchLevel--)
        {
            if (lastItemAtLevel.TryGetValue(searchLevel, out var item))
            {
                var found = FindItemByDataNumber(item, parentPrefix);
                if (found != null)
                {
                    _logger.LogDebug("[RuleBasedHierarchy] Found parent by fallback search: \"{Parent}\" (data-number=\"{ParentNum}\") for child data-number=\"{ChildNum}\"",
                        found.LinkName, found.DataNumber, dataNumber);
                    return found;
                }
            }
        }

        // Last resort: return root
        _logger.LogWarning("[RuleBasedHierarchy] Could not find parent for data-number=\"{DataNumber}\", using root as parent", dataNumber);
        return rootItem;
    }

    /// <summary>
    /// Recursively searches for an item with matching data-number
    /// </summary>
    private HierarchyItem? FindItemByDataNumber(HierarchyItem searchRoot, string targetDataNumber)
    {
        // Check this item
        if (searchRoot.DataNumber != null && searchRoot.DataNumber.Equals(targetDataNumber, StringComparison.Ordinal))
            return searchRoot;

        // Search children recursively
        foreach (var child in searchRoot.SubItems)
        {
            var found = FindItemByDataNumber(child, targetDataNumber);
            if (found != null)
                return found;
        }

        return null;
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
        Dictionary<int, HierarchyItem> lastItemAtLevel,
        ClassifiedSection? classifiedSection = null)
    {
        var normalizedId = FilenameUtils.NormalizeFileName(header.Text);

        // Calculate confidence score based on structural patterns
        var (confidence, flags, reasoning) = CalculateConfidence(header, level, lastItemAtLevel, classifiedSection);

        return new HierarchyItem
        {
            Id = normalizedId,
            Level = level,
            // ARCHITECTURE FIX: Use OriginalText (unmodified header) for display
            // Falls back to Text if OriginalText is empty (backward compatibility)
            LinkName = !string.IsNullOrEmpty(header.OriginalText) ? header.OriginalText.Trim() : header.Text.Trim(),
            DataRef = $"{normalizedId}.xml",
            Path = parent.Path,
            SubItems = new List<HierarchyItem>(),

            // Store data-number for parent matching
            DataNumber = header.DataNumber,

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
        Dictionary<int, HierarchyItem> lastItemAtLevel,
        ClassifiedSection? classifiedSection)
    {
        var score = 0.5; // Base score
        var flags = new List<UncertaintyFlag>();
        var reasons = new List<string>();

        // PATTERN 0: Pattern matching from training data (STRONGEST SIGNAL)
        if (classifiedSection?.MatchedPattern != null)
        {
            var pattern = classifiedSection.MatchedPattern;

            // Check if pattern has level frequency data
            if (pattern.LevelFrequency != null && pattern.LevelFrequency.Any())
            {
                // Find most common level for this pattern
                var totalOccurrences = pattern.LevelFrequency.Values.Sum();
                var currentLevelCount = pattern.LevelFrequency.GetValueOrDefault(level, 0);
                var currentLevelPercentage = totalOccurrences > 0
                    ? (double)currentLevelCount / totalOccurrences
                    : 0;

                if (currentLevelPercentage >= 0.95) // 95%+ at this level
                {
                    score += 0.40;
                    reasons.Add($"Pattern strongly suggests L{level} ({currentLevelPercentage:P0} of {totalOccurrences} cases)");
                }
                else if (currentLevelPercentage >= 0.80) // 80-95%
                {
                    score += 0.30;
                    reasons.Add($"Pattern typically L{level} ({currentLevelPercentage:P0} of {totalOccurrences} cases)");
                }
                else if (currentLevelPercentage >= 0.60) // 60-80%
                {
                    score += 0.20;
                    reasons.Add($"Pattern often L{level} ({currentLevelPercentage:P0} of {totalOccurrences} cases)");
                }
                else if (currentLevelPercentage >= 0.40) // 40-60%
                {
                    score += 0.10;
                    reasons.Add($"Pattern sometimes L{level} ({currentLevelPercentage:P0} of {totalOccurrences} cases)");
                }
                else if (currentLevelPercentage > 0) // < 40% but exists
                {
                    // No bonus, but note it
                    reasons.Add($"Pattern rarely L{level} ({currentLevelPercentage:P0} of {totalOccurrences} cases)");
                }
                else // Never at this level
                {
                    // Find where it usually appears
                    var mostCommonLevel = pattern.LevelFrequency
                        .OrderByDescending(kvp => kvp.Value)
                        .First();

                    score -= 0.20;
                    flags.Add(UncertaintyFlag.LevelMismatch);
                    reasons.Add($"Pattern never L{level} - typically L{mostCommonLevel.Key} ({mostCommonLevel.Value}x)");
                }
            }
        }

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

    /// <summary>
    /// Checks if header text indicates a "Notes to..." section that should trigger notes context.
    /// Examples: "Notes to the consolidated financial statements", "Notes to financial statements"
    /// </summary>
    private bool IsNotesToSection(string headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return false;

        // Common patterns for notes sections
        return headerText.Contains("Notes to", StringComparison.OrdinalIgnoreCase) ||
               headerText.Contains("Notes on", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes note titles for pattern matching.
    /// Uses same normalization as NotePatternMiner for consistency.
    /// </summary>
    private string NormalizeNoteTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        // Convert to lowercase
        var normalized = title.Trim().ToLowerInvariant();

        // Remove common prefixes like "note:", "note 1:", etc.
        normalized = Regex.Replace(normalized, @"^note\s*\d*\s*:?\s*", string.Empty, RegexOptions.IgnoreCase);

        // Remove ALL punctuation
        normalized = Regex.Replace(normalized, @"['`"",:;!?()\[\]{}\*\.\-/]", string.Empty);

        // Collapse multiple spaces to single space
        normalized = Regex.Replace(normalized, @"\s+", " ");

        // Remove leading/trailing spaces
        normalized = normalized.Trim();

        return normalized;
    }

    /// <summary>
    /// Finds parent by matching against learned parent-child relationships from training data.
    /// This method prioritizes high-frequency relationships that appear consistently across many hierarchies.
    /// Only returns a match if the parent pattern meets the minimum frequency threshold.
    /// </summary>
    private HierarchyItem? FindParentByLearnedPattern(
        List<ParentPattern> typicalParents,
        Dictionary<int, HierarchyItem> lastItemAtLevel,
        int minFrequency = 10)
    {
        // Get the most common parent pattern(s) above threshold
        var qualifyingParents = typicalParents
            .Where(p => p.Frequency >= minFrequency)
            .OrderByDescending(p => p.Frequency)
            .ToList();

        if (!qualifyingParents.Any())
        {
            return null;
        }

        // Search through recent items at various levels (newest to oldest)
        foreach (var level in lastItemAtLevel.Keys.OrderByDescending(k => k))
        {
            var item = lastItemAtLevel[level];
            var normalizedItemTitle = NormalizeText(item.LinkName);

            // Check if this item matches any qualifying parent
            foreach (var parentPattern in qualifyingParents)
            {
                if (normalizedItemTitle.Equals(parentPattern.ParentNormalizedTitle, StringComparison.Ordinal))
                {
                    _logger.LogInformation("[Pass 2] Found learned parent: \"{Parent}\" (Level {Level}, frequency: {Freq}x in training)",
                        item.LinkName, item.Level, parentPattern.Frequency);
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Pass 2: Determines level for a section using multiple signals.
    /// Uses classified section data from Pass 1 to make intelligent placement decisions.
    /// PRIORITY ORDER:
    /// 1. Learned parent-child relationships (highest confidence from training data)
    /// 2. data-number (explicit structure)
    /// 3. Pattern match (typical level for this section type)
    /// 4. Sequential context (position relative to previous section)
    /// 5. Header type fallback (HTML tag as last resort)
    /// </summary>
    private (int level, string signal, string reasoning) DetermineLevelForSection(
        ClassifiedSection section,
        ClassifiedSection? previousSection,
        Dictionary<int, HierarchyItem> lastItemAtLevel)
    {
        // Signal 1 (HIGHEST PRIORITY): Learned parent-child relationship from training data
        // This is statistical evidence from 269 hierarchies - more reliable than data-number assumptions
        if (section.MatchedPattern?.TypicalParents.Any() == true)
        {
            // Find if any recent item matches a typical parent with high frequency
            var learnedParent = FindParentByLearnedPattern(
                section.MatchedPattern.TypicalParents,
                lastItemAtLevel,
                minFrequency: 10); // Must appear in at least 10 training hierarchies

            if (learnedParent != null)
            {
                var level = learnedParent.Level + 1;
                var topParent = section.MatchedPattern.TypicalParents
                    .OrderByDescending(p => p.Frequency)
                    .First();

                // Log if this overrides a data-number signal
                string overrideNote = "";
                if (!string.IsNullOrEmpty(section.DataNumber))
                {
                    overrideNote = $" (overriding data-number=\"{section.DataNumber}\")";
                }

                return (level,
                        "learned-parent-child",
                        $"Learned parent \"{learnedParent.LinkName}\" ({topParent.Frequency}x in training){overrideNote} → Level {level}");
            }
        }

        // Signal 2: data-number (explicit structure)
        if (!string.IsNullOrEmpty(section.DataNumber))
        {
            var level = ParseDataNumberLevel(section.DataNumber);
            if (level > 0)
            {
                return (level, "data-number", $"data-number=\"{section.DataNumber}\" → Level {level}");
            }
        }

        // Signal 3: Matched pattern with high confidence
        if (section.MatchedPattern != null && section.MatchConfidence >= 0.05)
        {
            // Get most common level for this pattern
            var mostCommonLevel = section.MatchedPattern.MostCommonLevel;
            var frequency = section.MatchedPattern.LevelFrequency[mostCommonLevel];

            return (mostCommonLevel,
                    "pattern-match",
                    $"Pattern \"{section.MatchedPattern.NormalizedTitle}\" typically Level {mostCommonLevel} ({frequency}x, conf: {section.MatchConfidence:F2})");
        }

        // Signal 4: Parent-child relationship from patterns (adjacent sections)
        if (previousSection?.MatchedPattern != null && section.MatchedPattern != null)
        {
            var expectedLevel = InferLevelFromParentChild(
                section.NormalizedTitle,
                previousSection.NormalizedTitle,
                previousSection.DeterminedLevel);

            if (expectedLevel > 0)
            {
                return (expectedLevel,
                        "parent-child",
                        $"Known child of \"{previousSection.NormalizedTitle}\" → Level {expectedLevel}");
            }
        }

        // Signal 5: Sequential context (previous level + 1 for likely subsection)
        if (previousSection != null && !IsLikelySibling(section, previousSection))
        {
            var contextLevel = Math.Min(previousSection.DeterminedLevel + 1, 4); // Cap at level 4
            return (contextLevel,
                    "sequential-context",
                    $"Following \"{previousSection.HeaderText}\" (Level {previousSection.DeterminedLevel}) → Level {contextLevel}");
        }

        // Signal 6: Header type fallback
        var headerType = section.HeaderLevel.ToLowerInvariant();
        var fallbackLevel = headerType switch
        {
            "h1" => 1,
            "h2" => 2,
            "h3" => 3,
            "h4" => 4,
            _ => 2  // Default to Level 2 if unknown
        };

        return (fallbackLevel,
                "header-type-fallback",
                $"HTML tag <{headerType}> → Level {fallbackLevel} (no other signals available)");
    }

    /// <summary>
    /// Infers level for a child section based on parent-child pattern relationships
    /// </summary>
    private int InferLevelFromParentChild(string childNormalized, string parentNormalized, int parentLevel)
    {
        if (_comprehensivePatterns == null)
            return 0;

        // Find the child pattern
        var childPattern = _comprehensivePatterns.Patterns.FirstOrDefault(p =>
            p.NormalizedTitle.Equals(childNormalized, StringComparison.Ordinal));

        if (childPattern == null)
            return 0;

        // Check if this parent is a known parent for this child
        var parentRelationship = childPattern.TypicalParents.FirstOrDefault(p =>
            p.ParentNormalizedTitle.Equals(parentNormalized, StringComparison.Ordinal));

        if (parentRelationship != null && parentRelationship.Frequency >= 2)
        {
            // This is a known relationship - child should be one level deeper than parent
            return parentLevel + 1;
        }

        return 0;
    }

    /// <summary>
    /// Checks if two sections are likely siblings (same level) vs parent-child
    /// </summary>
    private bool IsLikelySibling(ClassifiedSection current, ClassifiedSection previous)
    {
        // If both have data-numbers, check if they're siblings
        if (!string.IsNullOrEmpty(current.DataNumber) && !string.IsNullOrEmpty(previous.DataNumber))
        {
            var currentLevel = ParseDataNumberLevel(current.DataNumber);
            var previousLevel = ParseDataNumberLevel(previous.DataNumber);

            // Same level in numbering = siblings
            if (currentLevel == previousLevel)
                return true;
        }

        // If both matched patterns and have same most-common-level, likely siblings
        if (current.MatchedPattern != null && previous.MatchedPattern != null)
        {
            if (current.MatchedPattern.MostCommonLevel == previous.MatchedPattern.MostCommonLevel)
                return true;
        }

        // Default: assume parent-child relationship (safer for nesting)
        return false;
    }

    /// <summary>
    /// Pass 2: Finds parent for a section using multiple strategies
    /// </summary>
    private HierarchyItem FindParentForSection(
        ClassifiedSection section,
        int level,
        Dictionary<int, HierarchyItem> lastItemAtLevel,
        HierarchyItem rootItem)
    {
        // Strategy 1: data-number prefix matching (existing logic)
        if (!string.IsNullOrEmpty(section.DataNumber))
        {
            var parent = FindParentByDataNumber(section.DataNumber, lastItemAtLevel, rootItem);
            if (parent != rootItem || level == 1)  // Found specific parent OR this is Level 1
                return parent;
        }

        // Strategy 2: Learned parent-child patterns
        if (section.MatchedPattern?.TypicalParents.Any() == true)
        {
            var parent = FindParentByPattern(
                section.MatchedPattern.TypicalParents,
                lastItemAtLevel);

            if (parent != null)
                return parent;
        }

        // Strategy 3: Most recent item at parent level (fallback)
        if (level > 1 && lastItemAtLevel.TryGetValue(level - 1, out var parentItem))
        {
            return parentItem;
        }

        // Last resort: root
        return rootItem;
    }

    /// <summary>
    /// Finds parent by matching against typical parent patterns
    /// </summary>
    private HierarchyItem? FindParentByPattern(
        List<ParentPattern> typicalParents,
        Dictionary<int, HierarchyItem> lastItemAtLevel)
    {
        // Search through recent items at various levels
        foreach (var level in lastItemAtLevel.Keys.OrderByDescending(k => k))
        {
            var item = lastItemAtLevel[level];
            var normalizedItemTitle = NormalizeText(item.LinkName);

            // Check if this item matches any typical parent
            foreach (var parentPattern in typicalParents.OrderByDescending(p => p.Frequency))
            {
                if (normalizedItemTitle.Equals(parentPattern.ParentNormalizedTitle, StringComparison.Ordinal))
                {
                    _logger.LogDebug("[Pass 2] Found parent by pattern: \"{Parent}\" (pattern frequency: {Freq})",
                        item.LinkName, parentPattern.Frequency);
                    return item;
                }
            }
        }

        return null;
    }
}

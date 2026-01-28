using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PdfConversion.Models;
using PdfConversion.Services;

namespace PdfConversion.Endpoints;

/// <summary>
/// Debug endpoint for testing and analyzing the duplicate header disambiguation logic.
///
/// This endpoint helps diagnose issues with the section extraction process when multiple
/// headers in the document match the same hierarchy item text.
///
/// USAGE:
///   curl "http://localhost:8085/api/debug/disambiguation?projectId=dutch-flower-group/ar24&hierarchyFile=hierarchies/hierarchy-pdf-xml.xml"
///   curl "http://localhost:8085/api/debug/disambiguation?projectId=dutch-flower-group/ar24&hierarchyFile=hierarchies/hierarchy-pdf-xml.xml&normalizedFile=normalized/docling-pdf.xml"
///
/// RETURNS:
///   JSON with detailed analysis of all duplicate header groups, including:
///   - Position filtering decisions (which candidates were excluded and why)
///   - Forward-looking scores for each candidate
///   - Expected vs found subsequent headers
///   - Auto-selection decisions and reasoning
/// </summary>
public static class DisambiguationDebugEndpoint
{
    public static async Task HandleAsync(
        HttpContext context,
        IConversionService conversionService,
        IProjectManagementService projectService,
        IHeaderMatchingService headerMatchingService,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        context.Response.ContentType = "application/json";

        try
        {
            // Get parameters
            var projectId = context.Request.Query["projectId"].FirstOrDefault();
            var hierarchyFile = context.Request.Query["hierarchyFile"].FirstOrDefault();
            var normalizedFile = context.Request.Query["normalizedFile"].FirstOrDefault();

            if (string.IsNullOrEmpty(projectId))
            {
                await WriteError(context, "Missing required parameter: projectId (format: customer/projectId)");
                return;
            }

            if (string.IsNullOrEmpty(hierarchyFile))
            {
                await WriteError(context, "Missing required parameter: hierarchyFile");
                return;
            }

            // Parse project ID
            var projectParts = projectId.Split('/', 2);
            if (projectParts.Length != 2)
            {
                await WriteError(context, $"Invalid projectId format: {projectId}. Expected 'customer/projectId'");
                return;
            }
            var customer = projectParts[0];
            var project = projectParts[1];

            // Build file paths
            var hierarchyPath = hierarchyFile.StartsWith("/app/data")
                ? hierarchyFile
                : Path.Combine("/app/data/output", customer, "projects", project, hierarchyFile);

            if (!File.Exists(hierarchyPath))
            {
                await WriteError(context, $"Hierarchy file not found: {hierarchyPath}");
                return;
            }

            // Auto-detect normalized file if not provided
            if (string.IsNullOrEmpty(normalizedFile))
            {
                // Try to infer from hierarchy filename
                var hierarchyName = Path.GetFileNameWithoutExtension(hierarchyPath);
                if (hierarchyName.StartsWith("hierarchy-"))
                {
                    var baseName = hierarchyName.Substring("hierarchy-".Length);
                    normalizedFile = $"normalized/{baseName}.xml";
                }
                else
                {
                    await WriteError(context, "Could not auto-detect normalizedFile. Please provide it explicitly.");
                    return;
                }
            }

            // Try output folder first, then input folder
            var normalizedPath = Path.Combine("/app/data/output", customer, "projects", project, normalizedFile);
            if (!File.Exists(normalizedPath))
            {
                normalizedPath = Path.Combine("/app/data/input", customer, "projects", project, normalizedFile);
            }
            if (!File.Exists(normalizedPath))
            {
                await WriteError(context, $"Normalized file not found in output or input folders: {normalizedFile}");
                return;
            }

            logger.LogInformation("DisambiguationDebug: Analyzing {HierarchyFile} against {NormalizedFile}",
                hierarchyFile, normalizedFile);

            // Load files
            var normalizedXml = await File.ReadAllTextAsync(normalizedPath);

            // Parse hierarchy
            var hierarchyStructure = await hierarchyService.LoadHierarchyAsync(hierarchyPath);
            var hierarchyItems = hierarchyService.GetAllItems(hierarchyStructure);

            // Parse normalized XML
            var xhtmlDoc = XDocument.Parse(normalizedXml);

            // Run header matching
            var matches = await headerMatchingService.FindExactMatchesAsync(xhtmlDoc, hierarchyItems);

            // Analyze disambiguation
            var result = AnalyzeDisambiguation(hierarchyItems, matches, xhtmlDoc, logger);

            // Add metadata
            result.ProjectId = projectId;
            result.HierarchyFile = hierarchyFile;
            result.NormalizedFile = normalizedFile;
            result.TotalHierarchyItems = hierarchyItems.Count;
            result.TotalMatches = matches.Count;

            // Write JSON response
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(result, options));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in DisambiguationDebug endpoint");
            await WriteError(context, $"Error: {ex.Message}");
        }
    }

    private static DisambiguationAnalysis AnalyzeDisambiguation(
        List<HierarchyItem> hierarchyItems,
        List<HeaderMatch> matches,
        XDocument xhtmlDoc,
        ILogger logger)
    {
        var result = new DisambiguationAnalysis();

        // Get all headers from document
        var allHeaders = xhtmlDoc.Descendants()
            .Where(e => IsHeaderElement(e))
            .ToList();

        result.TotalHeadersInDocument = allHeaders.Count;

        // Find duplicate groups
        var duplicateGroups = matches
            .Where(m => m.IsDuplicate)
            .GroupBy(m => m.HierarchyItem.Id)
            .ToList();

        result.TotalDuplicateGroups = duplicateGroups.Count;

        // Track SELECTED matches only (not all duplicates) - mirrors ConversionService.selectedMatches
        // This is crucial: we must use the position of the SELECTED match, not unselected duplicates
        var selectedMatches = new List<HeaderMatch>();

        foreach (var match in matches)
        {
            // Check if this is a duplicate
            if (match.IsDuplicate)
            {
                // Get all duplicates for this hierarchy item
                var allDuplicatesForItem = matches
                    .Where(m => m.HierarchyItem.Id == match.HierarchyItem.Id && m.IsDuplicate)
                    .ToList();

                // Only process once per group (first occurrence)
                if (allDuplicatesForItem.First() != match)
                {
                    continue; // Skip - this duplicate will be handled when we process the first one
                }

                // Use selectedMatches (actually used matches) to compute lastProcessedHeader
                // This mirrors the fix in ConversionService
                var lastProcHeader = selectedMatches.LastOrDefault(m => m.MatchedHeader != null)?.MatchedHeader;

                var group = new DuplicateGroupAnalysis
                {
                    HierarchyItemId = match.HierarchyItem.Id,
                    LinkName = match.HierarchyItem.LinkName,
                    HierarchyIndex = matches.IndexOf(match),
                    LastProcessedHeaderPosition = lastProcHeader != null ? GetDocumentPosition(lastProcHeader, xhtmlDoc) : -1,
                    LastProcessedHeaderText = lastProcHeader?.Value?.Trim()
                };

                // Analyze each candidate
                foreach (var duplicate in allDuplicatesForItem)
                {
                    var candidate = new CandidateAnalysis
                    {
                        DuplicateIndex = duplicate.DuplicateIndex + 1,
                        MatchedText = duplicate.MatchedText?.Trim(),
                        HasMatchedHeader = duplicate.MatchedHeader != null
                    };

                    if (duplicate.MatchedHeader != null)
                    {
                        candidate.Position = GetDocumentPosition(duplicate.MatchedHeader, xhtmlDoc);

                        // Check if included after position filter
                        if (lastProcHeader == null)
                        {
                            candidate.IncludedAfterFilter = true;
                            candidate.FilterReason = "No previous header (always included)";
                        }
                        else
                        {
                            var lastPos = GetDocumentPosition(lastProcHeader, xhtmlDoc);
                            var thisPos = candidate.Position;

                            // Current filter uses >=
                            candidate.IncludedAfterFilter = thisPos >= lastPos;
                            candidate.FilterReason = candidate.IncludedAfterFilter
                                ? $"Position {thisPos} >= {lastPos} (included)"
                                : $"Position {thisPos} < {lastPos} (EXCLUDED)";
                        }

                        // Calculate forward-looking score
                        var (score, expectedHeaders, foundHeaders) = CalculateForwardScore(
                            duplicate,
                            hierarchyItems,
                            matches.IndexOf(match),
                            allHeaders,
                            xhtmlDoc);

                        candidate.ForwardLookingScore = score;
                        candidate.ExpectedHeaders = expectedHeaders;
                        candidate.FoundHeaders = foundHeaders;

                        // Get context
                        candidate.PreviousContext = GetPreviousContext(duplicate.MatchedHeader, xhtmlDoc);
                        candidate.NextContext = GetNextContext(duplicate.MatchedHeader, xhtmlDoc);
                    }
                    else
                    {
                        candidate.IncludedAfterFilter = false;
                        candidate.FilterReason = "No matched header reference";
                    }

                    group.Candidates.Add(candidate);
                }

                // Determine auto-selection result
                var includedCandidates = group.Candidates.Where(c => c.IncludedAfterFilter).ToList();
                if (includedCandidates.Count == 0)
                {
                    group.AutoSelectionResult = "FAILED";
                    group.AutoSelectionReason = "All candidates were filtered out!";
                }
                else if (includedCandidates.Count == 1)
                {
                    group.AutoSelectionResult = $"candidate-{includedCandidates[0].DuplicateIndex}";
                    group.AutoSelectionReason = "Only one candidate after filtering";
                }
                else
                {
                    var sorted = includedCandidates.OrderByDescending(c => c.ForwardLookingScore).ToList();
                    var best = sorted[0];
                    var secondBest = sorted.Count > 1 ? sorted[1] : null;

                    bool hasPositiveScore = best.ForwardLookingScore >= 0;
                    bool hasForwardWinner = secondBest == null ||
                        best.ForwardLookingScore - secondBest.ForwardLookingScore >= 3;

                    if (hasPositiveScore && hasForwardWinner)
                    {
                        group.AutoSelectionResult = $"candidate-{best.DuplicateIndex}";
                        group.AutoSelectionReason = $"Clear winner: score {best.ForwardLookingScore} vs {secondBest?.ForwardLookingScore ?? 0} (diff >= 3)";
                    }
                    else if (!hasPositiveScore)
                    {
                        group.AutoSelectionResult = "MANUAL_REQUIRED";
                        group.AutoSelectionReason = $"Best score is negative ({best.ForwardLookingScore}) - expected headers not found";
                    }
                    else
                    {
                        group.AutoSelectionResult = "MANUAL_REQUIRED";
                        group.AutoSelectionReason = $"Scores too close: {best.ForwardLookingScore} vs {secondBest?.ForwardLookingScore} (diff < 3)";
                    }
                }

                result.DuplicateGroups.Add(group);

                // Add the SELECTED match to selectedMatches (simulate auto-selection for tracking)
                // This determines what "lastProcessedHeader" will be for subsequent duplicate groups
                var selectedCandidate = group.Candidates
                    .Where(c => c.IncludedAfterFilter)
                    .OrderByDescending(c => c.ForwardLookingScore)
                    .FirstOrDefault();

                if (selectedCandidate != null)
                {
                    // Find the actual match object for the selected candidate
                    var selectedMatch = allDuplicatesForItem
                        .FirstOrDefault(m => m.DuplicateIndex + 1 == selectedCandidate.DuplicateIndex);
                    if (selectedMatch != null)
                    {
                        selectedMatches.Add(selectedMatch);
                    }
                }
            }
            else
            {
                // Non-duplicate: add directly to selectedMatches
                if (match.MatchedHeader != null)
                {
                    selectedMatches.Add(match);
                }
            }

        }

        return result;
    }

    private static (int Score, List<string> Expected, List<string> Found) CalculateForwardScore(
        HeaderMatch duplicate,
        List<HierarchyItem> hierarchyItems,
        int startIndex,
        List<XElement> allHeaders,
        XDocument xhtmlDoc)
    {
        var expected = new List<string>();
        var found = new List<string>();
        int score = 0;

        if (duplicate.MatchedHeader == null) return (score, expected, found);

        // Get next 4 hierarchy items
        int lookAhead = Math.Min(4, hierarchyItems.Count - startIndex - 1);
        for (int j = 0; j < lookAhead; j++)
        {
            var item = hierarchyItems[startIndex + 1 + j];
            expected.Add(item.LinkName);
        }

        // Find position of this candidate in allHeaders
        var candidateIndex = allHeaders.IndexOf(duplicate.MatchedHeader);
        if (candidateIndex < 0) return (score, expected, found);

        // Get headers after this candidate
        var headersAfter = allHeaders.Skip(candidateIndex + 1).Take(20).ToList();

        // Score each expected header
        for (int j = 0; j < expected.Count; j++)
        {
            var expectedHeader = expected[j];
            var foundHeader = headersAfter.FirstOrDefault(h =>
                HeaderMatchesText(h.Value, expectedHeader));

            if (foundHeader != null)
            {
                found.Add(expectedHeader);
                score += (5 - j); // 5 for first, 4 for second, etc.
            }
            else
            {
                score -= 2; // Penalty
            }
        }

        return (score, expected, found);
    }

    private static bool HeaderMatchesText(string headerValue, string expectedText)
    {
        if (string.IsNullOrEmpty(headerValue) || string.IsNullOrEmpty(expectedText))
            return false;

        var normalized1 = NormalizeText(headerValue);
        var normalized2 = NormalizeText(expectedText);

        return normalized1.Contains(normalized2) || normalized2.Contains(normalized1);
    }

    private static string NormalizeText(string text)
    {
        return text.Trim().ToLowerInvariant()
            .Replace("\n", " ")
            .Replace("\r", "")
            .Replace("  ", " ");
    }

    private static bool IsHeaderElement(XElement element)
    {
        var localName = element.Name.LocalName.ToLowerInvariant();
        return localName is "h1" or "h2" or "h3" or "h4" or "h5" or "h6";
    }

    private static int GetDocumentPosition(XElement element, XDocument doc)
    {
        var body = doc.Descendants().FirstOrDefault(e =>
            e.Name.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase));
        if (body == null) return -1;

        var allElements = body.Descendants().ToList();
        return allElements.IndexOf(element);
    }

    private static string? GetPreviousContext(XElement header, XDocument doc)
    {
        var prev = header.ElementsBeforeSelf().LastOrDefault();
        if (prev == null) prev = header.Parent?.ElementsBeforeSelf().LastOrDefault();
        if (prev == null) return null;

        var text = prev.Value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
    }

    private static string? GetNextContext(XElement header, XDocument doc)
    {
        var next = header.ElementsAfterSelf().FirstOrDefault();
        if (next == null) next = header.Parent?.ElementsAfterSelf().FirstOrDefault();
        if (next == null) return null;

        var text = next.Value?.Trim();
        if (string.IsNullOrEmpty(text)) return null;

        return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
    }

    private static async Task WriteError(HttpContext context, string message)
    {
        context.Response.StatusCode = 400;
        var error = new { error = message };
        var options = new JsonSerializerOptions { WriteIndented = true };
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, options));
    }
}

// Response models
public class DisambiguationAnalysis
{
    public string? ProjectId { get; set; }
    public string? HierarchyFile { get; set; }
    public string? NormalizedFile { get; set; }
    public int TotalHierarchyItems { get; set; }
    public int TotalMatches { get; set; }
    public int TotalHeadersInDocument { get; set; }
    public int TotalDuplicateGroups { get; set; }
    public List<DuplicateGroupAnalysis> DuplicateGroups { get; set; } = new();
}

public class DuplicateGroupAnalysis
{
    public string? HierarchyItemId { get; set; }
    public string? LinkName { get; set; }
    public int HierarchyIndex { get; set; }
    public int LastProcessedHeaderPosition { get; set; }
    public string? LastProcessedHeaderText { get; set; }
    public string? AutoSelectionResult { get; set; }
    public string? AutoSelectionReason { get; set; }
    public List<CandidateAnalysis> Candidates { get; set; } = new();
}

public class CandidateAnalysis
{
    public int DuplicateIndex { get; set; }
    public string? MatchedText { get; set; }
    public int Position { get; set; }
    public bool HasMatchedHeader { get; set; }
    public bool IncludedAfterFilter { get; set; }
    public string? FilterReason { get; set; }
    public int ForwardLookingScore { get; set; }
    public List<string>? ExpectedHeaders { get; set; }
    public List<string>? FoundHeaders { get; set; }
    public string? PreviousContext { get; set; }
    public string? NextContext { get; set; }
}

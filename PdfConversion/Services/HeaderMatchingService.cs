using PdfConversion.Models;
using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Implementation of header matching service
/// </summary>
public class HeaderMatchingService : IHeaderMatchingService
{
    private readonly ILogger<HeaderMatchingService> _logger;

    public HeaderMatchingService(ILogger<HeaderMatchingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<HeaderMatch>> FindExactMatchesAsync(
        XDocument transformedXhtml,
        List<HierarchyItem> hierarchyItems,
        bool enableFuzzyMatch = true,
        double minConfidenceThreshold = 0.65)
    {
        try
        {
            _logger.LogInformation("Starting header matching for {Count} hierarchy items", hierarchyItems.Count);

            // Extract all headers from XHTML
            var headers = ExtractHeaders(transformedXhtml);
            _logger.LogInformation("Found {Count} headers in transformed XHTML", headers.Count);

            // Perform exact matching - find ALL matches (including duplicates)
            var matches = new List<HeaderMatch>();

            foreach (var item in hierarchyItems)
            {
                var itemMatches = await FindAllMatchesForItemAsync(item, headers);
                matches.AddRange(itemMatches);
            }

            // Perform fuzzy matching for unmatched items
            if (enableFuzzyMatch)
            {
                var unmatchedItems = matches.Where(m => !m.IsExactMatch && m.MatchedHeader == null).ToList();

                if (unmatchedItems.Any())
                {
                    _logger.LogInformation("Attempting fuzzy matching for {Count} unmatched items (threshold: {Threshold:P0})",
                        unmatchedItems.Count, minConfidenceThreshold);

                    var fuzzyMatchGroups = await FindFuzzyMatchesAsync(transformedXhtml, unmatchedItems, minConfidenceThreshold);

                    // Remove unmatched items and add fuzzy matches
                    foreach (var fuzzyMatchGroup in fuzzyMatchGroups)
                    {
                        if (fuzzyMatchGroup.Any())
                        {
                            // Remove the unmatched placeholder
                            var hierarchyItemId = fuzzyMatchGroup.First().HierarchyItem.Id;
                            matches.RemoveAll(m => m.HierarchyItem.Id == hierarchyItemId && m.MatchedHeader == null);
                            // Add the fuzzy match(es)
                            matches.AddRange(fuzzyMatchGroup);
                        }
                    }
                }
            }

            // Detect duplicate matches
            matches = DetectDuplicateMatches(matches);

            // Log statistics
            LogMatchStatistics(matches);

            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during header matching");
            throw;
        }
    }

    private List<XElement> ExtractHeaders(XDocument xhtml)
    {
        // Get all header elements (h1-h6) from the document
        var headerNames = new[] { "h1", "h2", "h3", "h4", "h5", "h6" };

        var headers = xhtml.Descendants()
            .Where(e => headerNames.Contains(e.Name.LocalName))
            .ToList();

        return headers;
    }

    private Task<List<HeaderMatch>> FindAllMatchesForItemAsync(HierarchyItem item, List<XElement> headers)
    {
        var normalizedSearchText = NormalizeText(item.LinkName);

        // Find ALL exact matches (not just the first one)
        var matchedHeaders = headers.Where(h =>
            NormalizeText(h.Value) == normalizedSearchText).ToList();

        if (matchedHeaders.Any())
        {
            var matches = matchedHeaders.Select(matchedHeader =>
            {
                _logger.LogDebug("Exact match: '{SearchText}' -> '{MatchedText}'",
                    item.LinkName, matchedHeader.Value);

                return new HeaderMatch
                {
                    HierarchyItem = item,
                    MatchedHeader = matchedHeader,
                    MatchedText = matchedHeader.Value,
                    IsExactMatch = true,
                    ConfidenceScore = 1.0
                };
            }).ToList();

            return Task.FromResult(matches);
        }
        else
        {
            // No exact match found - will be handled by fuzzy matching in Phase 5
            _logger.LogDebug("No exact match for '{SearchText}'", item.LinkName);

            return Task.FromResult(new List<HeaderMatch>
            {
                new HeaderMatch
                {
                    HierarchyItem = item,
                    MatchedHeader = null,
                    MatchedText = null,
                    IsExactMatch = false,
                    ConfidenceScore = 0.0
                }
            });
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove line breaks, tabs, carriage returns
        text = text.Replace("\r", " ")
                   .Replace("\n", " ")
                   .Replace("\t", " ");

        // Convert to lowercase and trim
        text = text.ToLowerInvariant().Trim();

        // Replace multiple spaces with single space
        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        return text;
    }

    private async Task<List<List<HeaderMatch>>> FindFuzzyMatchesAsync(
        XDocument transformedXhtml,
        List<HeaderMatch> unmatchedItems,
        double minConfidenceThreshold = 0.65)
    {
        var fuzzyMatchGroups = new List<List<HeaderMatch>>();

        // Extract all headers from XHTML
        var headers = transformedXhtml.Descendants()
            .Where(e => new[] { "h1", "h2", "h3", "h4", "h5", "h6" }
                .Contains(e.Name.LocalName))
            .ToList();

        foreach (var unmatchedItem in unmatchedItems.Where(m => !m.IsExactMatch))
        {
            var searchText = NormalizeText(unmatchedItem.HierarchyItem.LinkName);

            // Find all headers that match above threshold (for duplicates)
            var matches = new List<HeaderMatch>();

            foreach (var header in headers)
            {
                var headerText = NormalizeText(header.Value);
                var distance = CalculateLevenshteinDistance(searchText, headerText);
                var maxLength = Math.Max(searchText.Length, headerText.Length);
                var similarity = maxLength > 0 ? 1.0 - ((double)distance / maxLength) : 0.0;

                if (similarity >= minConfidenceThreshold)
                {
                    matches.Add(new HeaderMatch
                    {
                        HierarchyItem = unmatchedItem.HierarchyItem,
                        MatchedHeader = header,
                        MatchedText = header.Value,
                        IsExactMatch = false,
                        ConfidenceScore = similarity
                    });
                }
            }

            if (matches.Any())
            {
                // Keep only the best match(es) - all with the highest score
                var bestScore = matches.Max(m => m.ConfidenceScore);
                var bestMatches = matches.Where(m => m.ConfidenceScore == bestScore).ToList();

                fuzzyMatchGroups.Add(bestMatches);

                foreach (var match in bestMatches)
                {
                    _logger.LogInformation(
                        "Fuzzy match ({ConfidenceScore:P0}): '{SearchText}' -> '{MatchedText}'",
                        match.ConfidenceScore, unmatchedItem.HierarchyItem.LinkName, match.MatchedText);
                }
            }
        }

        return await Task.FromResult(fuzzyMatchGroups);
    }

    private int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        // Initialize first column and row
        for (var i = 0; i <= sourceLength; i++)
            distance[i, 0] = i;

        for (var j = 0; j <= targetLength; j++)
            distance[0, j] = j;

        // Calculate distances
        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = (source[i - 1] == target[j - 1]) ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[sourceLength, targetLength];
    }

    public List<HeaderMatch> DetectDuplicateMatches(List<HeaderMatch> matches)
    {
        // Group by hierarchy item ID (only matched items)
        var groupedMatches = matches
            .Where(m => m.MatchedHeader != null)
            .GroupBy(m => m.HierarchyItem.Id)
            .ToList();

        foreach (var group in groupedMatches)
        {
            var matchesInGroup = group.ToList();
            var isDuplicate = matchesInGroup.Count > 1;

            if (isDuplicate)
            {
                _logger.LogWarning(
                    "Duplicate match detected for '{LinkName}': {Count} headers match",
                    group.First().HierarchyItem.LinkName,
                    matchesInGroup.Count);

                for (int i = 0; i < matchesInGroup.Count; i++)
                {
                    matchesInGroup[i].IsDuplicate = true;
                    matchesInGroup[i].DuplicateCount = matchesInGroup.Count;
                    matchesInGroup[i].DuplicateIndex = i;

                    _logger.LogDebug(
                        "  [{Index}] Header: '{MatchedText}' (confidence: {ConfidenceScore:P0})",
                        i,
                        matchesInGroup[i].MatchedText,
                        matchesInGroup[i].ConfidenceScore);
                }
            }
        }

        var duplicateMatchCount = matches.Count(m => m.IsDuplicate);
        if (duplicateMatchCount > 0)
        {
            _logger.LogWarning("Total duplicate matches: {DuplicateCount}", duplicateMatchCount);
        }

        return matches;
    }

    private void LogMatchStatistics(List<HeaderMatch> matches)
    {
        var totalItems = matches.Count;
        var exactMatches = matches.Count(m => m.IsExactMatch);
        var fuzzyMatches = matches.Count(m => !m.IsExactMatch && m.MatchedHeader != null);
        var unmatched = matches.Count(m => !m.IsExactMatch && m.MatchedHeader == null);
        var duplicates = matches.Count(m => m.IsDuplicate);

        _logger.LogInformation("Header matching complete:");
        _logger.LogInformation("  Total hierarchy items: {Total}", totalItems);
        _logger.LogInformation("  Exact matches: {Exact} ({Percentage:P0})",
            exactMatches, totalItems > 0 ? (double)exactMatches / totalItems : 0);
        _logger.LogInformation("  Fuzzy matches: {Fuzzy} ({Percentage:P0})",
            fuzzyMatches, totalItems > 0 ? (double)fuzzyMatches / totalItems : 0);
        _logger.LogInformation("  Unmatched items: {Unmatched} ({Percentage:P0})",
            unmatched, totalItems > 0 ? (double)unmatched / totalItems : 0);

        if (duplicates > 0)
        {
            _logger.LogInformation("  Duplicate matches: {Duplicates} ({Percentage:P0})",
                duplicates, totalItems > 0 ? (double)duplicates / totalItems : 0);
        }

        // Log unmatched items at warning level
        var unmatchedItems = matches
            .Where(m => !m.IsExactMatch && m.MatchedHeader == null)
            .Select(m => m.HierarchyItem.LinkName)
            .ToList();

        if (unmatchedItems.Any())
        {
            _logger.LogWarning("Unmatched hierarchy items (after fuzzy matching):");
            foreach (var itemName in unmatchedItems)
            {
                _logger.LogWarning("  - {ItemName}", itemName);
            }
        }
    }
}

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

            // Perform exact matching
            var matches = new List<HeaderMatch>();

            foreach (var item in hierarchyItems)
            {
                var match = await FindMatchForItemAsync(item, headers);
                matches.Add(match);
            }

            // Perform fuzzy matching for unmatched items
            if (enableFuzzyMatch)
            {
                var unmatchedItems = matches.Where(m => !m.IsExactMatch && m.MatchedHeader == null).ToList();

                if (unmatchedItems.Any())
                {
                    _logger.LogInformation("Attempting fuzzy matching for {Count} unmatched items (threshold: {Threshold:P0})",
                        unmatchedItems.Count, minConfidenceThreshold);

                    var fuzzyMatches = await FindFuzzyMatchesAsync(transformedXhtml, unmatchedItems, minConfidenceThreshold);

                    // Replace unmatched items with fuzzy matches
                    foreach (var fuzzyMatch in fuzzyMatches)
                    {
                        var index = matches.FindIndex(m => m.HierarchyItem.Id == fuzzyMatch.HierarchyItem.Id);
                        if (index >= 0)
                        {
                            matches[index] = fuzzyMatch;
                        }
                    }
                }
            }

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

    private Task<HeaderMatch> FindMatchForItemAsync(HierarchyItem item, List<XElement> headers)
    {
        var normalizedSearchText = NormalizeText(item.LinkName);

        // Try to find exact match
        var matchedHeader = headers.FirstOrDefault(h =>
            NormalizeText(h.Value) == normalizedSearchText);

        if (matchedHeader != null)
        {
            _logger.LogDebug("Exact match: '{SearchText}' -> '{MatchedText}'",
                item.LinkName, matchedHeader.Value);

            return Task.FromResult(new HeaderMatch
            {
                HierarchyItem = item,
                MatchedHeader = matchedHeader,
                MatchedText = matchedHeader.Value,
                IsExactMatch = true,
                ConfidenceScore = 1.0
            });
        }
        else
        {
            // No exact match found - will be handled by fuzzy matching in Phase 5
            _logger.LogDebug("No exact match for '{SearchText}'", item.LinkName);

            return Task.FromResult(new HeaderMatch
            {
                HierarchyItem = item,
                MatchedHeader = null,
                MatchedText = null,
                IsExactMatch = false,
                ConfidenceScore = 0.0
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

    private async Task<List<HeaderMatch>> FindFuzzyMatchesAsync(
        XDocument transformedXhtml,
        List<HeaderMatch> unmatchedItems,
        double minConfidenceThreshold = 0.65)
    {
        var fuzzyMatches = new List<HeaderMatch>();

        // Extract all headers from XHTML
        var headers = transformedXhtml.Descendants()
            .Where(e => new[] { "h1", "h2", "h3", "h4", "h5", "h6" }
                .Contains(e.Name.LocalName))
            .ToList();

        foreach (var unmatchedItem in unmatchedItems.Where(m => !m.IsExactMatch))
        {
            var searchText = NormalizeText(unmatchedItem.HierarchyItem.LinkName);
            HeaderMatch? bestMatch = null;
            double bestScore = 0.0;

            foreach (var header in headers)
            {
                var headerText = NormalizeText(header.Value);
                var distance = CalculateLevenshteinDistance(searchText, headerText);
                var maxLength = Math.Max(searchText.Length, headerText.Length);
                var similarity = maxLength > 0 ? 1.0 - ((double)distance / maxLength) : 0.0;

                if (similarity > bestScore && similarity >= minConfidenceThreshold)
                {
                    bestScore = similarity;
                    bestMatch = new HeaderMatch
                    {
                        HierarchyItem = unmatchedItem.HierarchyItem,
                        MatchedHeader = header,
                        MatchedText = header.Value,
                        IsExactMatch = false,
                        ConfidenceScore = similarity
                    };
                }
            }

            if (bestMatch != null)
            {
                fuzzyMatches.Add(bestMatch);
                _logger.LogInformation(
                    "Fuzzy match ({ConfidenceScore:P0}): '{SearchText}' -> '{MatchedText}'",
                    bestMatch.ConfidenceScore, unmatchedItem.HierarchyItem.LinkName, bestMatch.MatchedText);
            }
        }

        return await Task.FromResult(fuzzyMatches);
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

    private void LogMatchStatistics(List<HeaderMatch> matches)
    {
        var totalItems = matches.Count;
        var exactMatches = matches.Count(m => m.IsExactMatch);
        var fuzzyMatches = matches.Count(m => !m.IsExactMatch && m.MatchedHeader != null);
        var unmatched = matches.Count(m => !m.IsExactMatch && m.MatchedHeader == null);

        _logger.LogInformation("Header matching complete:");
        _logger.LogInformation("  Total hierarchy items: {Total}", totalItems);
        _logger.LogInformation("  Exact matches: {Exact} ({Percentage:P0})",
            exactMatches, totalItems > 0 ? (double)exactMatches / totalItems : 0);
        _logger.LogInformation("  Fuzzy matches: {Fuzzy} ({Percentage:P0})",
            fuzzyMatches, totalItems > 0 ? (double)fuzzyMatches / totalItems : 0);
        _logger.LogInformation("  Unmatched items: {Unmatched} ({Percentage:P0})",
            unmatched, totalItems > 0 ? (double)unmatched / totalItems : 0);

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

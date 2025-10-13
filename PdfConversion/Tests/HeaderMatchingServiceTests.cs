using Microsoft.Extensions.Logging;
using Moq;
using PdfConversion.Models;
using PdfConversion.Services;
using System.Xml.Linq;
using Xunit;

namespace PdfConversion.Tests;

/// <summary>
/// Unit tests for HeaderMatchingService
/// </summary>
public class HeaderMatchingServiceTests
{
    private readonly Mock<ILogger<HeaderMatchingService>> _mockLogger;
    private readonly HeaderMatchingService _service;

    public HeaderMatchingServiceTests()
    {
        _mockLogger = new Mock<ILogger<HeaderMatchingService>>();
        _service = new HeaderMatchingService(_mockLogger.Object);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithExactMatches_ReturnsMatches()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Financial Summary</h2>
                    <h3>Market Risk</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            },
            new HierarchyItem
            {
                Id = "market-risk",
                Level = 3,
                DataRef = "market-risk.xml",
                LinkName = "Market Risk"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.All(matches, m => Assert.True(m.IsExactMatch));
        Assert.All(matches, m => Assert.Equal(1.0, m.ConfidenceScore));
        Assert.All(matches, m => Assert.NotNull(m.MatchedHeader));
        Assert.All(matches, m => Assert.NotNull(m.MatchedText));
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithNoMatches_ReturnsUnmatchedItems()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Some Other Header</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Single(matches);
        Assert.False(matches[0].IsExactMatch);
        Assert.Equal(0.0, matches[0].ConfidenceScore);
        Assert.Null(matches[0].MatchedHeader);
        Assert.Null(matches[0].MatchedText);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithWhitespaceVariations_MatchesCorrectly()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors   Report</h1>
                    <h2>Financial
                    Summary</h2>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.True(m.IsExactMatch));
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithCaseVariations_MatchesCorrectly()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>DIRECTORS REPORT</h1>
                    <h2>financial summary</h2>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.True(m.IsExactMatch));
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithMixedResults_ReturnsCorrectMatches()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Some Other Header</h2>
                    <h3>Market Risk</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            },
            new HierarchyItem
            {
                Id = "market-risk",
                Level = 3,
                DataRef = "market-risk.xml",
                LinkName = "Market Risk"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.Equal(2, matches.Count(m => m.IsExactMatch));
        Assert.Single(matches.Where(m => !m.IsExactMatch));

        // Verify the matched ones
        var directorsMatch = matches.First(m => m.HierarchyItem.Id == "directors-report");
        Assert.True(directorsMatch.IsExactMatch);
        Assert.NotNull(directorsMatch.MatchedHeader);

        var marketRiskMatch = matches.First(m => m.HierarchyItem.Id == "market-risk");
        Assert.True(marketRiskMatch.IsExactMatch);
        Assert.NotNull(marketRiskMatch.MatchedHeader);

        // Verify the unmatched one
        var financialMatch = matches.First(m => m.HierarchyItem.Id == "financial-summary");
        Assert.False(financialMatch.IsExactMatch);
        Assert.Null(financialMatch.MatchedHeader);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithEmptyHierarchyList_ReturnsEmptyList()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>();

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Empty(matches);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithNoHeaders_ReturnsUnmatchedItems()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <p>Some paragraph text</p>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Single(matches);
        Assert.False(matches[0].IsExactMatch);
        Assert.Null(matches[0].MatchedHeader);
    }

    // ===== FUZZY MATCHING TESTS =====

    [Fact]
    public async Task FindExactMatchesAsync_WithFuzzyMatches_ReturnsFuzzyMatches()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Director's Report</h1>
                    <h2>Finacial Summary</h2>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: true);

        // Assert
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.NotNull(m.MatchedHeader));
        Assert.All(matches, m => Assert.False(m.IsExactMatch)); // Both are fuzzy matches
        Assert.All(matches, m => Assert.True(m.ConfidenceScore > 0.65));
        Assert.All(matches, m => Assert.True(m.ConfidenceScore < 1.0));
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithFuzzyMatchDisabled_ReturnsUnmatched()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Director's Report</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: false);

        // Assert
        Assert.Single(matches);
        Assert.False(matches[0].IsExactMatch);
        Assert.Null(matches[0].MatchedHeader);
        Assert.Equal(0.0, matches[0].ConfidenceScore);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithConfidenceThreshold_FiltersLowScores()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Completely Different Title</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(
            xhtml,
            hierarchyItems,
            enableFuzzyMatch: true,
            minConfidenceThreshold: 0.65);

        // Assert
        Assert.Single(matches);
        // Should not match due to low similarity
        Assert.Null(matches[0].MatchedHeader);
        Assert.Equal(0.0, matches[0].ConfidenceScore);
    }

    [Fact]
    public async Task FindExactMatchesAsync_ExactMatchTakesPrecedence_OverFuzzyMatch()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Director's Report</h2>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: true);

        // Assert
        Assert.Single(matches);
        Assert.True(matches[0].IsExactMatch);
        Assert.Equal(1.0, matches[0].ConfidenceScore);
        Assert.Equal("Directors Report", matches[0].MatchedText);
    }

    [Fact]
    public async Task FindExactMatchesAsync_FuzzyMatchingSelectsBestMatch()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Completely Different</h1>
                    <h2>Director's Report</h2>
                    <h3>Directors Reports</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: true);

        // Assert
        // Both "Director's Report" and "Directors Reports" have the same similarity score (94%)
        // so both are returned as duplicate fuzzy matches
        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.False(m.IsExactMatch));
        Assert.All(matches, m => Assert.NotNull(m.MatchedHeader));
        Assert.All(matches, m => Assert.True(m.ConfidenceScore > 0.65));

        // Both should be flagged as duplicates with same confidence score
        Assert.All(matches, m => Assert.True(m.IsDuplicate));
        Assert.All(matches, m => Assert.Equal(2, m.DuplicateCount));

        // Check that we got both headers
        var matchedTexts = matches.Select(m => m.MatchedText).OrderBy(t => t).ToList();
        Assert.Contains("Director's Report", matchedTexts);
        Assert.Contains("Directors Reports", matchedTexts);
    }

    [Fact]
    public async Task FindExactMatchesAsync_MixedExactAndFuzzyMatches_ReturnsCorrectly()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Finacial Summary</h2>
                    <h3>Market Risk</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            },
            new HierarchyItem
            {
                Id = "market-risk",
                Level = 3,
                DataRef = "market-risk.xml",
                LinkName = "Market Risk"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: true);

        // Assert
        Assert.Equal(3, matches.Count);

        // First match should be exact
        var directorsMatch = matches.First(m => m.HierarchyItem.Id == "directors-report");
        Assert.True(directorsMatch.IsExactMatch);
        Assert.Equal(1.0, directorsMatch.ConfidenceScore);

        // Second match should be fuzzy
        var financialMatch = matches.First(m => m.HierarchyItem.Id == "financial-summary");
        Assert.False(financialMatch.IsExactMatch);
        Assert.True(financialMatch.ConfidenceScore > 0.65 && financialMatch.ConfidenceScore < 1.0);

        // Third match should be exact
        var marketMatch = matches.First(m => m.HierarchyItem.Id == "market-risk");
        Assert.True(marketMatch.IsExactMatch);
        Assert.Equal(1.0, marketMatch.ConfidenceScore);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithLowConfidenceThreshold_MatchesMoreItems()
    {
        // Arrange
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            }
        };

        // Act
        var matchesHighThreshold = await _service.FindExactMatchesAsync(
            xhtml, hierarchyItems, enableFuzzyMatch: true, minConfidenceThreshold: 0.8);

        var matchesLowThreshold = await _service.FindExactMatchesAsync(
            xhtml, hierarchyItems, enableFuzzyMatch: true, minConfidenceThreshold: 0.5);

        // Assert - with high threshold, shouldn't match
        Assert.Single(matchesHighThreshold);
        Assert.Null(matchesHighThreshold[0].MatchedHeader);

        // Assert - with low threshold, should match
        Assert.Single(matchesLowThreshold);
        Assert.NotNull(matchesLowThreshold[0].MatchedHeader);
        Assert.Equal("Directors", matchesLowThreshold[0].MatchedText);
    }

    // ===== DUPLICATE DETECTION TESTS =====

    [Fact]
    public async Task FindExactMatchesAsync_WithDuplicateMatches_FlagsAllDuplicates()
    {
        // Arrange - two different headers matching the same hierarchy item
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Notes</h1>
                    <p>First section of notes...</p>
                    <h2>Notes</h2>
                    <p>Second section of notes...</p>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "notes",
                Level = 1,
                DataRef = "notes.xml",
                LinkName = "Notes"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        // Should have 2 matches (one for each header)
        Assert.Equal(2, matches.Count);

        // Both matches should be flagged as duplicates
        Assert.All(matches, m => Assert.True(m.IsDuplicate));
        Assert.All(matches, m => Assert.Equal(2, m.DuplicateCount));

        // Check indices are correct
        Assert.Equal(0, matches[0].DuplicateIndex);
        Assert.Equal(1, matches[1].DuplicateIndex);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithThreeDuplicates_FlagsAllWithCorrectIndices()
    {
        // Arrange - three headers matching the same hierarchy item
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Notes</h1>
                    <h2>Notes</h2>
                    <h3>Notes</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "notes",
                Level = 1,
                DataRef = "notes.xml",
                LinkName = "Notes"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        // Should have 3 matches (one for each header)
        Assert.Equal(3, matches.Count);

        // All matches should be flagged as duplicates
        Assert.All(matches, m => Assert.True(m.IsDuplicate));
        Assert.All(matches, m => Assert.Equal(3, m.DuplicateCount));

        // Check indices are correct
        Assert.Equal(0, matches[0].DuplicateIndex);
        Assert.Equal(1, matches[1].DuplicateIndex);
        Assert.Equal(2, matches[2].DuplicateIndex);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithNoDuplicates_DoesNotFlagAseDuplicate()
    {
        // Arrange - unique matches for each hierarchy item
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Financial Summary</h2>
                    <h3>Market Risk</h3>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "financial-summary",
                Level = 2,
                DataRef = "financial-summary.xml",
                LinkName = "Financial Summary"
            },
            new HierarchyItem
            {
                Id = "market-risk",
                Level = 3,
                DataRef = "market-risk.xml",
                LinkName = "Market Risk"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        Assert.Equal(3, matches.Count);
        Assert.All(matches, m => Assert.False(m.IsDuplicate));
        Assert.All(matches, m => Assert.Equal(0, m.DuplicateCount));
        Assert.All(matches, m => Assert.Equal(0, m.DuplicateIndex));
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithMixedDuplicatesAndUnique_FlagsCorrectly()
    {
        // Arrange - mix of unique and duplicate matches
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                    <h2>Notes</h2>
                    <h3>Market Risk</h3>
                    <h4>Notes</h4>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "notes",
                Level = 2,
                DataRef = "notes.xml",
                LinkName = "Notes"
            },
            new HierarchyItem
            {
                Id = "market-risk",
                Level = 3,
                DataRef = "market-risk.xml",
                LinkName = "Market Risk"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems);

        // Assert
        // Should have 4 matches: 1 for directors, 2 for notes, 1 for market risk
        Assert.Equal(4, matches.Count);

        // Directors Report - unique
        var directorsMatch = matches.First(m => m.HierarchyItem.Id == "directors-report");
        Assert.False(directorsMatch.IsDuplicate);
        Assert.Equal(0, directorsMatch.DuplicateCount);

        // Notes - duplicates (2 matches)
        var notesMatches = matches.Where(m => m.HierarchyItem.Id == "notes").ToList();
        Assert.Equal(2, notesMatches.Count);
        Assert.All(notesMatches, m => Assert.True(m.IsDuplicate));
        Assert.All(notesMatches, m => Assert.Equal(2, m.DuplicateCount));
        Assert.Equal(0, notesMatches[0].DuplicateIndex);
        Assert.Equal(1, notesMatches[1].DuplicateIndex);

        // Market Risk - unique
        var marketMatch = matches.First(m => m.HierarchyItem.Id == "market-risk");
        Assert.False(marketMatch.IsDuplicate);
        Assert.Equal(0, marketMatch.DuplicateCount);
    }

    [Fact]
    public async Task FindExactMatchesAsync_WithUnmatchedItems_DoesNotProcessForDuplicates()
    {
        // Arrange - one matched, one unmatched
        var xhtml = XDocument.Parse(@"
            <html>
                <body>
                    <h1>Directors Report</h1>
                </body>
            </html>
        ");

        var hierarchyItems = new List<HierarchyItem>
        {
            new HierarchyItem
            {
                Id = "directors-report",
                Level = 1,
                DataRef = "directors-report.xml",
                LinkName = "Directors Report"
            },
            new HierarchyItem
            {
                Id = "notes",
                Level = 2,
                DataRef = "notes.xml",
                LinkName = "Notes"
            }
        };

        // Act
        var matches = await _service.FindExactMatchesAsync(xhtml, hierarchyItems, enableFuzzyMatch: false);

        // Assert
        Assert.Equal(2, matches.Count);

        // Matched item - not duplicate (only one match)
        var directorsMatch = matches.First(m => m.HierarchyItem.Id == "directors-report");
        Assert.True(directorsMatch.IsExactMatch);
        Assert.False(directorsMatch.IsDuplicate);

        // Unmatched item - should not be processed for duplicates
        var notesMatch = matches.First(m => m.HierarchyItem.Id == "notes");
        Assert.False(notesMatch.IsExactMatch);
        Assert.Null(notesMatch.MatchedHeader);
        Assert.False(notesMatch.IsDuplicate);
    }
}

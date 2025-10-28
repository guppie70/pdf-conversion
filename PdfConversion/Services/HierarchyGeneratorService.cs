using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfConversion.Models;
using PdfConversion.Utils;

namespace PdfConversion.Services;

/// <summary>
/// Validation result for hierarchy whitelist matching
/// </summary>
public class HierarchyValidationResult
{
    public bool IsValid { get; set; }
    public List<string> HallucinatedItems { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}

public interface IHierarchyGeneratorService
{
    /// <summary>
    /// Generate a hierarchy proposal from normalized XML using AI/LLM
    /// </summary>
    /// <param name="normalizedXml">The normalized XHTML document to analyze</param>
    /// <param name="exampleHierarchies">2-3 user-selected training examples showing hierarchy structure</param>
    /// <param name="modelName">The Ollama model to use (default: llama3.1:70b)</param>
    /// <param name="cancellationToken">Cancellation token for timeout control</param>
    /// <returns>A hierarchy proposal with confidence scoring</returns>
    Task<HierarchyProposal> GenerateHierarchyAsync(
        string normalizedXml,
        List<string> exampleHierarchies,
        string modelName = "llama3.1:70b",
        CancellationToken cancellationToken = default);
}

public class HierarchyGeneratorService : IHierarchyGeneratorService
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<HierarchyGeneratorService> _logger;

    public HierarchyGeneratorService(
        IOllamaService ollamaService,
        ILogger<HierarchyGeneratorService> logger)
    {
        _ollamaService = ollamaService;
        _logger = logger;
    }

    public async Task<HierarchyProposal> GenerateHierarchyAsync(
        string normalizedXml,
        List<string> exampleHierarchies,
        string modelName = "llama3.1:70b",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[HierarchyGenerator] Starting hierarchy generation with model: {Model}, " +
                                   "XML length: {XmlLength} chars, Examples: {ExampleCount}",
                modelName, normalizedXml.Length, exampleHierarchies.Count);

            // Extract headers to build whitelist (needed for both prompt and validation)
            var headers = ExtractHeadersFromXml(normalizedXml);
            var whitelistHeaders = headers.Select(h => h.Text).ToList();

            // Build 4-part prompt
            var prompt = BuildPrompt(normalizedXml, exampleHierarchies);

            _logger.LogInformation("[HierarchyGenerator] Generated prompt size: {Size} chars (~{Tokens} tokens)",
                prompt.Length, prompt.Length / 4); // Rough token estimate

            // Log prompt structure breakdown
            var parts = prompt.Split("## PART");
            _logger.LogDebug("[HierarchyGenerator] Prompt has {PartCount} parts. " +
                "Part sizes: {PartSizes}",
                parts.Length - 1, // -1 because first element is before "PART 1"
                string.Join(", ", parts.Skip(1).Select((p, i) => $"Part {i+1}: {p.Length} chars")));

            // Call LLM with deterministic temperature
            var startTime = DateTime.UtcNow;
            var jsonResponse = await _ollamaService.GenerateAsync(
                modelName,
                prompt,
                temperature: 0.3, // Deterministic for consistent results
                cancellationToken);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("[HierarchyGenerator] LLM generation completed in {Duration:F1}s",
                duration.TotalSeconds);

            // Log response details immediately
            _logger.LogInformation("[HierarchyGenerator] LLM returned {Length} characters",
                jsonResponse.Length);

            // Log first 1000 chars for quick inspection
            var preview = jsonResponse.Length > 1000
                ? jsonResponse.Substring(0, 1000) + "..."
                : jsonResponse;
            _logger.LogDebug("[HierarchyGenerator] Response preview: {Preview}", preview);

            // Save debug files for troubleshooting (save FULL prompt, not truncated)
            await SaveDebugResponseAsync(jsonResponse, prompt);

            // Parse and validate JSON response (with whitelist validation)
            var proposal = ParseJsonResponse(jsonResponse, whitelistHeaders);

            _logger.LogInformation("[HierarchyGenerator] Successfully generated hierarchy: " +
                                   "{TotalItems} items, {Confidence}% confidence, {Uncertainties} uncertain items",
                proposal.TotalItems, proposal.OverallConfidence, proposal.Uncertainties.Count);

            return proposal;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[HierarchyGenerator] Hierarchy generation cancelled or timed out");
            throw new TimeoutException("Hierarchy generation exceeded time limit");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HierarchyGenerator] Failed to generate hierarchy. " +
                "Exception type: {Type}, Message: {Message}",
                ex.GetType().Name, ex.Message);

            // Log inner exception details if present
            if (ex.InnerException != null)
            {
                _logger.LogError("[HierarchyGenerator] Inner exception: {Type} - {Message}",
                    ex.InnerException.GetType().Name, ex.InnerException.Message);
            }

            throw new InvalidOperationException(
                $"Hierarchy generation failed: {ex.Message}. " +
                $"Check logs and /app/data/debug/ollama-responses/ for details.",
                ex);
        }
    }

    /// <summary>
    /// Builds the whitelist-first prompt for LLM hierarchy generation.
    /// Uses extracted headers as primary input instead of full XML.
    /// </summary>
    private string BuildPrompt(string normalizedXml, List<string> exampleHierarchies)
    {
        // Extract headers to build whitelist
        var headers = ExtractHeadersFromXml(normalizedXml);

        if (!headers.Any())
        {
            throw new InvalidOperationException(
                "No headers found in normalized XML. Cannot generate hierarchy from empty document.");
        }

        // Build markdown-formatted whitelist
        var whitelist = BuildMarkdownWhitelist(headers);

        // Simplify example hierarchies (remove unnecessary attributes)
        // Then anonymize to prevent LLM from copying example header names
        var simplifiedExamples = exampleHierarchies
            .Select(SimplifyHierarchyXml)
            .Select(AnonymizeHierarchyXml)  // Add anonymization step
            .ToList();

        // Build example hierarchies section
        var examplesBuilder = new StringBuilder();
        for (int i = 0; i < simplifiedExamples.Count; i++)
        {
            examplesBuilder.AppendLine($"### Example {i + 1}:");
            examplesBuilder.AppendLine("```xml");
            examplesBuilder.AppendLine(simplifiedExamples[i]);
            examplesBuilder.AppendLine("```");
            examplesBuilder.AppendLine();
        }

        // Load prompt template from markdown file
        var promptTemplatePath = "/app/data/llm-hierarchy-generation.md";
        string promptTemplate;

        try
        {
            promptTemplate = File.ReadAllText(promptTemplatePath);
            _logger.LogInformation("[HierarchyGenerator] Loaded prompt template from {Path} ({Length} chars)",
                promptTemplatePath, promptTemplate.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HierarchyGenerator] Failed to load prompt template from {Path}",
                promptTemplatePath);
            throw new InvalidOperationException(
                $"Could not load prompt template from {promptTemplatePath}. Ensure file exists.", ex);
        }

        // Replace placeholders in template
        var prompt = promptTemplate
            .Replace("{{EXAMPLE_HIERARCHIES}}", examplesBuilder.ToString())
            .Replace("{{DOCUMENT_STRUCTURE_PREVIEW}}", whitelist)
            .Replace("{COUNT}", headers.Count.ToString());

        _logger.LogInformation("[HierarchyGenerator] Built whitelist-first prompt: " +
            "{TotalLength} chars, {Headers} headers, {Examples} examples",
            prompt.Length, headers.Count, exampleHierarchies.Count);

        // Log size comparison
        var xmlSize = normalizedXml.Length;
        var promptSize = prompt.Length;
        var savings = xmlSize - promptSize;
        var savingsPercent = xmlSize > 0 ? (savings / (double)xmlSize) * 100 : 0;

        if (savings > 0)
        {
            _logger.LogInformation("[HierarchyGenerator] Whitelist approach saved {Savings:N0} chars ({Percent:F1}%): " +
                "Would have been {XmlSize:N0}, now {PromptSize:N0}",
                savings, savingsPercent, xmlSize, promptSize);
        }

        return prompt;
    }

    /// <summary>
    /// Cleans normalized XML for LLM consumption by removing irrelevant content.
    /// Removes style elements and tables without note references to reduce token count.
    /// </summary>
    /// <param name="normalizedXml">The normalized XHTML document</param>
    /// <returns>Cleaned XML with reduced content</returns>
    private string CleanXmlForPrompt(string normalizedXml)
    {
        try
        {
            var doc = XDocument.Parse(normalizedXml);

            var originalElementCount = doc.Descendants().Count();

            // Remove all <style> elements (styling is irrelevant for structure analysis)
            var styleElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var style in styleElements)
            {
                style.Remove();
            }

            // Remove tables without "Note" references
            // (these are typically organizational charts, layouts, etc. - not financial data)
            var tablesToRemove = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("table", StringComparison.OrdinalIgnoreCase))
                .Where(table => !ContainsNoteReference(table))
                .ToList();

            foreach (var table in tablesToRemove)
            {
                table.Remove();
            }

            var cleanedElementCount = doc.Descendants().Count();
            var removedElements = originalElementCount - cleanedElementCount;

            _logger.LogInformation("[HierarchyGenerator] XML cleaning: removed {StyleCount} <style> elements, " +
                                   "{TableCount} tables without note references, " +
                                   "{TotalRemoved} total elements removed",
                styleElements.Count, tablesToRemove.Count, removedElements);

            return doc.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HierarchyGenerator] Failed to clean XML for prompt, using original. " +
                                   "This may result in larger prompts and slower generation.");
            return normalizedXml; // Fallback to original if cleaning fails
        }
    }

    /// <summary>
    /// Checks if a table element contains a reference to financial statement notes.
    /// </summary>
    /// <param name="table">The table element to check</param>
    /// <returns>True if the table contains the word "Note" in any cell</returns>
    private bool ContainsNoteReference(XElement table)
    {
        // Check if any descendant element contains the word "Note" (case-insensitive)
        // This catches "Note 12", "See Note 5", "Notes", etc.
        return table.Descendants()
            .Any(e => !string.IsNullOrWhiteSpace(e.Value) &&
                      e.Value.Contains("Note", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts all header elements (h1, h2, h3) from the normalized XML to build whitelist.
    /// Returns list of header text with data-number attributes if present.
    /// Parses trailing numbers from header text (e.g., "Financial performance 2" → "Financial performance" with data-number="2").
    /// </summary>
    /// <param name="normalizedXml">The normalized XHTML document</param>
    /// <returns>List of header information objects</returns>
    private List<HeaderInfo> ExtractHeadersFromXml(string normalizedXml)
    {
        try
        {
            var doc = XDocument.Parse(normalizedXml);
            var headers = new List<HeaderInfo>();

            // Pattern to match trailing section numbers: "Header Text 1.2.3"
            var trailingNumberPattern = new Regex(@"^(.+?)\s+([\d\.]+)$", RegexOptions.Compiled);

            // Get all h1, h2, h3, h4, h5, h6 elements
            var headerElements = doc.Descendants()
                .Where(e => e.Name.LocalName.ToLower().StartsWith("h") &&
                           e.Name.LocalName.Length == 2 &&
                           char.IsDigit(e.Name.LocalName[1]))
                .ToList();

            foreach (var element in headerElements)
            {
                var text = element.Value.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue; // Skip empty headers

                // Check for data-number attribute first
                var dataNumber = element.Attribute("data-number")?.Value;

                // If no data-number attribute, try to extract from trailing text
                if (string.IsNullOrEmpty(dataNumber))
                {
                    var match = trailingNumberPattern.Match(text);
                    if (match.Success)
                    {
                        text = match.Groups[1].Value.Trim();  // Clean header text
                        dataNumber = match.Groups[2].Value;   // Extracted number
                    }
                }

                headers.Add(new HeaderInfo
                {
                    Text = text,
                    DataNumber = dataNumber,
                    Level = element.Name.LocalName.ToLower()
                });
            }

            _logger.LogInformation("[HierarchyGenerator] Extracted {Count} headers from normalized XML, " +
                "{WithDataNumber} with data-number attributes",
                headers.Count,
                headers.Count(h => !string.IsNullOrEmpty(h.DataNumber)));

            return headers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HierarchyGenerator] Failed to extract headers from XML");
            throw new InvalidOperationException("Failed to extract headers from normalized XML", ex);
        }
    }

    /// <summary>
    /// Builds markdown-formatted whitelist of headers for LLM prompt.
    /// Returns numbered list with data-number attributes shown.
    /// </summary>
    /// <param name="headers">List of extracted headers</param>
    /// <returns>Markdown formatted whitelist string</returns>
    private string BuildMarkdownWhitelist(List<HeaderInfo> headers)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var lineNumber = i + 1;

            // Truncate very long headers
            var displayText = header.Text.Length > 100
                ? header.Text.Substring(0, 97) + "..."
                : header.Text;

            // Format with or without data-number
            if (!string.IsNullOrEmpty(header.DataNumber))
            {
                sb.AppendLine($"{lineNumber}. {displayText} (data-number=\"{header.DataNumber}\")");
            }
            else
            {
                sb.AppendLine($"{lineNumber}. {displayText}");
            }
        }

        _logger.LogInformation("[HierarchyGenerator] Built markdown whitelist: {Count} headers",
            headers.Count);

        return sb.ToString();
    }

    /// <summary>
    /// Simplifies hierarchy XML by removing attributes that LLM doesn't need.
    /// Keeps: level, linkName, data-tocnumber, data-tocstart, data-tocend
    /// Removes: id, dataRef, path, confidence, reasoning
    /// </summary>
    /// <param name="fullHierarchyXml">Full hierarchy XML</param>
    /// <returns>Simplified hierarchy XML</returns>
    private string SimplifyHierarchyXml(string fullHierarchyXml)
    {
        try
        {
            var doc = XDocument.Parse(fullHierarchyXml);

            // Attributes to remove (LLM doesn't need these, C# calculates them)
            var attributesToRemove = new[]
            {
                "id", "data-ref", "path", "confidence",
                "reasoning", "is-uncertain"
            };

            // Process all item elements
            foreach (var element in doc.Descendants().Where(e => e.Name.LocalName == "item"))
            {
                foreach (var attrName in attributesToRemove)
                {
                    element.Attribute(attrName)?.Remove();
                }
            }

            // Remove web_page elements (redundant with linkName)
            foreach (var webPageElement in doc.Descendants().Where(e => e.Name.LocalName == "web_page").ToList())
            {
                // Extract linkname before removing
                var linknameElement = webPageElement.Element("linkname");
                if (linknameElement != null)
                {
                    // Add linkName as attribute to parent item if not present
                    var parentItem = webPageElement.Parent;
                    if (parentItem != null && parentItem.Attribute("linkName") == null)
                    {
                        parentItem.Add(new XAttribute("linkName", linknameElement.Value));
                    }
                }

                webPageElement.Remove();
            }

            // Remove sub_items wrapper, keep items directly under parent
            foreach (var subItemsElement in doc.Descendants().Where(e => e.Name.LocalName == "sub_items").ToList())
            {
                var parentItem = subItemsElement.Parent;
                if (parentItem != null)
                {
                    // Move children directly to parent
                    var children = subItemsElement.Elements().ToList();
                    subItemsElement.Remove();

                    foreach (var child in children)
                    {
                        parentItem.Add(child);
                    }
                }
            }

            var simplified = doc.ToString();

            _logger.LogInformation("[HierarchyGenerator] Simplified hierarchy XML: " +
                "{Original} → {Simplified} chars ({Reduction}% reduction)",
                fullHierarchyXml.Length, simplified.Length,
                ((fullHierarchyXml.Length - simplified.Length) / (double)fullHierarchyXml.Length) * 100);

            return simplified;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HierarchyGenerator] Failed to simplify hierarchy XML, using original");
            return fullHierarchyXml; // Fallback to original if simplification fails
        }
    }

    /// <summary>
    /// Anonymizes header names in example hierarchies to prevent LLM from copying them.
    /// Replaces actual header text with generic placeholders like "Section A", "Section B", "Subsection A.1".
    /// This allows the LLM to learn nesting patterns without being tempted to use example headers.
    /// </summary>
    /// <param name="hierarchyXml">Simplified hierarchy XML</param>
    /// <returns>Anonymized hierarchy XML with placeholder names</returns>
    private string AnonymizeHierarchyXml(string hierarchyXml)
    {
        try
        {
            var doc = XDocument.Parse(hierarchyXml);
            var sectionCounter = 0;
            var levelCounters = new Dictionary<int, int>();

            void AnonymizeItem(XElement item, int parentLevel = 0)
            {
                var level = int.Parse(item.Attribute("level")?.Value ?? "1");

                // Reset child counters when we go up levels
                foreach (var key in levelCounters.Keys.Where(k => k > level).ToList())
                {
                    levelCounters.Remove(key);
                }

                // Increment counter for this level
                if (!levelCounters.ContainsKey(level))
                {
                    levelCounters[level] = 0;
                }
                levelCounters[level]++;

                // Generate placeholder name based on level
                string placeholder;
                if (level == 1)
                {
                    // Top level: "Section A", "Section B", etc.
                    placeholder = $"Section {(char)('A' + sectionCounter)}";
                    sectionCounter++;
                }
                else
                {
                    // Nested: "Subsection A.1", "Subsection A.2", etc.
                    var parentLetter = (char)('A' + (sectionCounter - 1));
                    placeholder = $"Subsection {parentLetter}.{levelCounters[level]}";
                }

                // Replace linkName attribute
                var linkNameAttr = item.Attribute("linkName");
                if (linkNameAttr != null)
                {
                    linkNameAttr.Value = placeholder;
                }

                // Recursively anonymize children (check both direct children and those in sub_items)
                var childItems = item.Elements("item").ToList();
                var subItemsContainer = item.Element("sub_items");
                if (subItemsContainer != null)
                {
                    childItems.AddRange(subItemsContainer.Elements("item"));
                }

                foreach (var child in childItems)
                {
                    AnonymizeItem(child, level);
                }
            }

            // Anonymize all items (find level=0 or level=1 root items and process recursively)
            var rootItems = doc.Descendants("item")
                .Where(e => e.Attribute("level")?.Value == "0" ||
                           (e.Attribute("level")?.Value == "1" && !e.Ancestors("item").Any()))
                .ToList();

            if (rootItems.Count == 0)
            {
                // Fallback: just find all top-level items (no item ancestors)
                rootItems = doc.Descendants("item")
                    .Where(e => !e.Ancestors("item").Any())
                    .ToList();
            }

            _logger.LogDebug("[HierarchyGenerator] Anonymizing {Count} root items", rootItems.Count);

            foreach (var rootItem in rootItems)
            {
                AnonymizeItem(rootItem);
            }

            var result = doc.ToString(SaveOptions.DisableFormatting);

            // Verify anonymization worked
            if (result.Contains("Section A") || result.Contains("Subsection"))
            {
                _logger.LogInformation("[HierarchyGenerator] Successfully anonymized hierarchy XML");
            }
            else
            {
                _logger.LogWarning("[HierarchyGenerator] Anonymization may have failed - no placeholders found");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HierarchyGenerator] Failed to anonymize hierarchy XML, returning simplified version");
            return hierarchyXml; // Fallback to non-anonymized
        }
    }

    /// <summary>
    /// Helper class for header information extracted from XML
    /// </summary>
    private class HeaderInfo
    {
        public string Text { get; set; } = string.Empty;
        public string? DataNumber { get; set; }
        public string Level { get; set; } = string.Empty; // "h1", "h2", etc.
    }

    /// <summary>
    /// Saves the raw LLM response and full prompt to debug files for troubleshooting
    /// </summary>
    private async Task SaveDebugResponseAsync(string jsonResponse, string fullPrompt)
    {
        try
        {
            var debugDir = "/app/data/debug/ollama-responses";
            Directory.CreateDirectory(debugDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var responseFile = Path.Combine(debugDir, $"response-{timestamp}.json");
            var promptFile = Path.Combine(debugDir, $"prompt-{timestamp}.txt");

            // Save raw LLM response
            await File.WriteAllTextAsync(responseFile, jsonResponse);

            // Save FULL prompt (no truncation) - users need to see everything sent to the LLM
            await File.WriteAllTextAsync(promptFile, fullPrompt);

            _logger.LogInformation("[HierarchyGenerator] Saved debug files: response={ResponseFile} ({ResponseSize} chars), prompt={PromptFile} ({PromptSize} chars)",
                responseFile, jsonResponse.Length, promptFile, fullPrompt.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HierarchyGenerator] Failed to save debug files");
        }
    }

    /// <summary>
    /// Parses and validates the LLM JSON response into a HierarchyProposal.
    /// Handles minimal JSON format (whitelist-first approach).
    /// </summary>
    private HierarchyProposal ParseJsonResponse(string jsonResponse, List<string> whitelistedHeaders)
    {
        try
        {
            // Clean up response (remove any markdown code blocks if present)
            var cleanJson = jsonResponse.Trim();
            if (cleanJson.StartsWith("```json"))
            {
                cleanJson = cleanJson.Substring(7);
            }
            if (cleanJson.StartsWith("```"))
            {
                cleanJson = cleanJson.Substring(3);
            }
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            }
            cleanJson = cleanJson.Trim();

            _logger.LogDebug("[HierarchyGenerator] Cleaned JSON length: {Length} chars", cleanJson.Length);

            // Deserialize to minimal DTO structure
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var dto = JsonSerializer.Deserialize<HierarchyResponseDto>(cleanJson, options);

            if (dto == null || dto.Structure == null || !dto.Structure.Any())
            {
                throw new InvalidOperationException(
                    "LLM returned null or empty hierarchy structure");
            }

            // Create root item
            var rootItem = new HierarchyItem
            {
                Id = "report-root",
                Level = 0,
                LinkName = "Annual Report",
                DataRef = "report-root.xml",
                Path = "/",
                SubItems = dto.Structure
                    .Select(child => EnrichFromDto(child, currentLevel: 1))
                    .ToList()
            };

            // Calculate statistics
            var allItems = new List<HierarchyItem>();
            CollectAllItems(rootItem, allItems);

            // Validate whitelist match (collect issues, don't throw)
            var validationResult = ValidateWhitelistMatch(allItems, whitelistedHeaders);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "[HierarchyGenerator] Validation detected issues: {Summary}. " +
                    "Returning hierarchy with warnings for user review.",
                    validationResult.Summary);
            }

            var uncertainties = allItems
                .Where(item => item.Confidence.HasValue && item.Confidence.Value < 70)
                .ToList();

            // Mark uncertain items
            foreach (var item in uncertainties)
            {
                item.IsUncertain = true;
            }

            // Calculate overall confidence
            var itemsWithConfidence = allItems
                .Where(item => item.Confidence.HasValue)
                .ToList();

            int overallConfidence = 100;
            if (itemsWithConfidence.Any())
            {
                overallConfidence = (int)itemsWithConfidence.Average(item => item.Confidence!.Value);
            }

            _logger.LogInformation("[HierarchyGenerator] Parsed hierarchy: {Total} items, " +
                                   "{WithConfidence} with confidence scores, {Uncertain} uncertain",
                allItems.Count, itemsWithConfidence.Count, uncertainties.Count);

            return new HierarchyProposal
            {
                Root = rootItem,
                OverallConfidence = overallConfidence,
                Uncertainties = uncertainties,
                Reasoning = dto.Reasoning ?? string.Empty,
                TotalItems = allItems.Count,
                ValidationResult = validationResult
            };
        }
        catch (JsonException ex)
        {
            // Log full response length and preview
            _logger.LogError(ex, "[HierarchyGenerator] JSON parsing failed. " +
                "Response length: {Length} chars. Exception: {Message}",
                jsonResponse.Length, ex.Message);

            // Log more context
            var lines = jsonResponse.Split('\n');
            _logger.LogError("[HierarchyGenerator] Response has {LineCount} lines. First 10 lines:\n{FirstLines}",
                lines.Length, string.Join("\n", lines.Take(10)));

            if (lines.Length > 10)
            {
                _logger.LogError("[HierarchyGenerator] Last 10 lines:\n{LastLines}",
                    string.Join("\n", lines.TakeLast(10)));
            }

            // Log where parsing failed if possible
            if (ex.Message.Contains("LineNumber"))
            {
                _logger.LogError("[HierarchyGenerator] Parse error details: {Details}", ex.Message);
            }

            throw new InvalidOperationException(
                $"Failed to parse LLM JSON response ({jsonResponse.Length} chars). " +
                $"Check debug file in /app/data/debug/ollama-responses/ for full response. " +
                $"Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Enriches a minimal DTO by calculating C#-generated fields using existing utilities.
    /// Converts from LLM's minimal format to full HierarchyItem with all required fields.
    /// </summary>
    private HierarchyItem EnrichFromDto(HierarchyItemSimpleDto dto, int currentLevel)
    {
        // Use existing FilenameUtils to generate ID
        var normalizedId = FilenameUtils.NormalizeFileName(dto.Name);

        return new HierarchyItem
        {
            // FROM LLM
            LinkName = dto.Name.Trim(),
            Confidence = dto.Confidence,
            Reasoning = dto.UncertaintyReason,
            IsUncertain = dto.Confidence.HasValue && dto.Confidence.Value < 70,

            // CALCULATED BY C#
            Id = normalizedId,
            DataRef = $"{normalizedId}.xml",
            Level = currentLevel,
            Path = "/",

            // RECURSIVELY ENRICH CHILDREN
            SubItems = dto.Children?
                .Select(child => EnrichFromDto(child, currentLevel + 1))
                .ToList() ?? new List<HierarchyItem>()
        };
    }

    /// <summary>
    /// Validates that LLM output is a valid subset of the whitelist (no hallucinations).
    /// LLM can OMIT headers (they become in-section content), but CANNOT ADD headers not in whitelist.
    /// Returns HierarchyValidationResult with details rather than throwing exceptions.
    /// </summary>
    private HierarchyValidationResult ValidateWhitelistMatch(List<HierarchyItem> allItems, List<string> whitelistedHeaders)
    {
        var result = new HierarchyValidationResult { IsValid = true };

        // Extract output names (excluding root)
        var outputNames = allItems
            .Where(item => item.Level > 0) // Skip root (level 0)
            .Select(item => item.LinkName.Trim())
            .ToList();

        var whitelistSet = new HashSet<string>(whitelistedHeaders, StringComparer.Ordinal);

        // Check for hallucinated items (items NOT in whitelist)
        var hallucinated = outputNames
            .Where(name => !whitelistSet.Contains(name))
            .Distinct()
            .ToList();

        if (hallucinated.Any())
        {
            result.IsValid = false;
            result.HallucinatedItems = hallucinated;
            result.Summary = $"⚠️ Found {hallucinated.Count} hallucinated item(s) not in whitelist";

            // Mark items as hallucinated in the hierarchy
            MarkHallucinatedItems(allItems, whitelistSet);
        }

        // Calculate omissions (headers in whitelist but not in output - these become in-section headers)
        var outputSet = new HashSet<string>(outputNames, StringComparer.Ordinal);
        var omitted = whitelistedHeaders
            .Where(name => !outputSet.Contains(name))
            .ToList();

        // Log statistics
        _logger.LogInformation(
            "[HierarchyGenerator] Validation result: {Valid}, {HierarchyCount} items, " +
            "{OmittedCount} omitted, {HallucinatedCount} hallucinated",
            result.IsValid, outputNames.Count, omitted.Count, hallucinated.Count);

        if (omitted.Any())
        {
            _logger.LogDebug(
                "[HierarchyGenerator] In-section headers ({Count}): {Headers}",
                omitted.Count,
                string.Join(", ", omitted.Take(20).Select(h => $"\"{h}\"")));
        }

        return result;
    }

    /// <summary>
    /// Marks hallucinated items in the hierarchy tree
    /// </summary>
    private void MarkHallucinatedItems(List<HierarchyItem> items, HashSet<string> whitelistSet)
    {
        foreach (var item in items)
        {
            if (item.Level > 0 && !whitelistSet.Contains(item.LinkName.Trim()))
            {
                item.IsHallucinated = true;
                item.Reasoning = "⚠️ This item was not found in the source document (hallucination)";
            }

            if (item.SubItems != null && item.SubItems.Any())
            {
                MarkHallucinatedItems(item.SubItems, whitelistSet);
            }
        }
    }

    /// <summary>
    /// Collects all items from the hierarchy tree into a flat list
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
    /// Minimal DTO classes for JSON deserialization (whitelist-first approach).
    /// LLM only provides name, confidence, uncertaintyReason, and children.
    /// C# calculates: id, dataRef, level, path, etc.
    /// </summary>
    private class HierarchyResponseDto
    {
        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("structure")]
        public List<HierarchyItemSimpleDto>? Structure { get; set; }
    }

    private class HierarchyItemSimpleDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public int? Confidence { get; set; }

        [JsonPropertyName("uncertaintyReason")]
        public string? UncertaintyReason { get; set; }

        [JsonPropertyName("children")]
        public List<HierarchyItemSimpleDto>? Children { get; set; }
    }
}

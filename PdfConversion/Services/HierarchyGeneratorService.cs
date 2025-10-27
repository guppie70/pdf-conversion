using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PdfConversion.Models;

namespace PdfConversion.Services;

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

            // Parse and validate JSON response
            var proposal = ParseJsonResponse(jsonResponse);

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
    /// Builds the 4-part prompt for LLM hierarchy generation by loading template from markdown file
    /// and replacing placeholders with actual content
    /// </summary>
    private string BuildPrompt(string normalizedXml, List<string> exampleHierarchies)
    {
        // Clean XML to reduce prompt size and improve generation speed
        var cleanedXml = CleanXmlForPrompt(normalizedXml);

        var originalSize = normalizedXml.Length;
        var cleanedSize = cleanedXml.Length;
        var savings = originalSize - cleanedSize;
        var savingsPercent = originalSize > 0 ? (savings / (double)originalSize) * 100 : 0;

        _logger.LogInformation("[HierarchyGenerator] XML cleaning saved {Savings:N0} chars ({Percent:F1}%): " +
                               "{Original:N0} → {Cleaned:N0}",
            savings, savingsPercent, originalSize, cleanedSize);

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
            _logger.LogError(ex, "[HierarchyGenerator] Failed to load prompt template from {Path}, using fallback",
                promptTemplatePath);
            throw new InvalidOperationException(
                $"Could not load prompt template from {promptTemplatePath}. Ensure file exists.", ex);
        }

        // Build example hierarchies section
        var examplesBuilder = new StringBuilder();
        for (int i = 0; i < exampleHierarchies.Count; i++)
        {
            examplesBuilder.AppendLine($"### Example {i + 1}:");
            examplesBuilder.AppendLine("```xml");
            examplesBuilder.AppendLine(exampleHierarchies[i]);
            examplesBuilder.AppendLine("```");
            examplesBuilder.AppendLine();
        }

        // Extract document structure preview
        var structurePreview = ExtractDocumentStructurePreview(cleanedXml);

        // Replace placeholders in template
        var prompt = promptTemplate
            .Replace("{{EXAMPLE_HIERARCHIES}}", examplesBuilder.ToString())
            .Replace("{{DOCUMENT_STRUCTURE_PREVIEW}}", structurePreview)
            .Replace("{{NORMALIZED_XHTML}}", cleanedXml);

        _logger.LogInformation("[HierarchyGenerator] Built prompt from template: {Length} chars total",
            prompt.Length);

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
    /// Extracts all heading elements from the document to provide structure preview.
    /// This helps LLMs identify all sections before processing full content.
    /// </summary>
    /// <param name="cleanedXml">The cleaned XHTML document</param>
    /// <returns>Formatted structure preview showing all headings</returns>
    private string ExtractDocumentStructurePreview(string cleanedXml)
    {
        try
        {
            var doc = XDocument.Parse(cleanedXml);
            var sb = new StringBuilder();

            // Get all h1, h2, h3 elements
            var headings = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("h1", StringComparison.OrdinalIgnoreCase) ||
                           e.Name.LocalName.Equals("h2", StringComparison.OrdinalIgnoreCase) ||
                           e.Name.LocalName.Equals("h3", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (headings.Count == 0)
            {
                return "No structured headings found in document.";
            }

            foreach (var heading in headings)
            {
                var level = heading.Name.LocalName.ToLower();
                var text = heading.Value.Trim();
                var dataNumber = heading.Attribute("data-number")?.Value;

                // Truncate very long headings
                if (text.Length > 100)
                {
                    text = text.Substring(0, 97) + "...";
                }

                // Indent based on heading level
                var indent = level switch
                {
                    "h1" => "",
                    "h2" => "  ",
                    "h3" => "    ",
                    _ => ""
                };

                // Format with or without data-number
                if (!string.IsNullOrEmpty(dataNumber))
                {
                    sb.AppendLine($"{indent}- <{level} data-number=\"{dataNumber}\">{text}</{level}>");
                }
                else
                {
                    sb.AppendLine($"{indent}- <{level}>{text}</{level}>");
                }
            }

            var preview = sb.ToString();
            _logger.LogInformation("[HierarchyGenerator] Extracted {Count} headings for structure preview", headings.Count);

            return preview;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HierarchyGenerator] Failed to extract document structure preview");
            return "Could not extract document structure preview.";
        }
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
    /// Parses and validates the LLM JSON response into a HierarchyProposal
    /// </summary>
    private HierarchyProposal ParseJsonResponse(string jsonResponse)
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

            // Deserialize to DTO structure
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var dto = JsonSerializer.Deserialize<HierarchyResponseDto>(cleanJson, options);

            if (dto == null || dto.Root == null)
            {
                throw new InvalidOperationException("LLM returned null or invalid hierarchy structure");
            }

            // Convert DTO to HierarchyItem tree
            var rootItem = ConvertDtoToHierarchyItem(dto.Root);

            // Calculate statistics
            var allItems = new List<HierarchyItem>();
            CollectAllItems(rootItem, allItems);

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
                                   "{WithConfidence} with scores, {Uncertain} uncertain",
                allItems.Count, itemsWithConfidence.Count, uncertainties.Count);

            return new HierarchyProposal
            {
                Root = rootItem,
                OverallConfidence = overallConfidence,
                Uncertainties = uncertainties,
                Reasoning = dto.Reasoning ?? string.Empty,
                TotalItems = allItems.Count
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
    /// Converts a DTO item to a HierarchyItem recursively
    /// </summary>
    private HierarchyItem ConvertDtoToHierarchyItem(HierarchyItemDto dto)
    {
        var item = new HierarchyItem
        {
            Id = dto.Id ?? string.Empty,
            Level = dto.Level,
            LinkName = dto.LinkName ?? string.Empty,
            DataRef = dto.DataRef ?? string.Empty,
            Path = dto.Path ?? "/",
            Confidence = dto.Confidence,
            Reasoning = dto.Reasoning,
            TocStart = dto.TocStart ?? false,
            TocEnd = dto.TocEnd ?? false,
            TocNumber = dto.TocNumber,
            TocStyle = dto.TocStyle
        };

        // Recursively convert sub items
        if (dto.SubItems != null)
        {
            item.SubItems = dto.SubItems
                .Select(ConvertDtoToHierarchyItem)
                .ToList();
        }

        return item;
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
    /// DTO classes for JSON deserialization
    /// Uses nullable types to handle optional fields from LLM response
    /// </summary>
    private class HierarchyResponseDto
    {
        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("root")]
        public HierarchyItemDto? Root { get; set; }
    }

    private class HierarchyItemDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("linkName")]
        public string? LinkName { get; set; }

        [JsonPropertyName("dataRef")]
        public string? DataRef { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("confidence")]
        public int? Confidence { get; set; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("tocStart")]
        public bool? TocStart { get; set; }

        [JsonPropertyName("tocEnd")]
        public bool? TocEnd { get; set; }

        [JsonPropertyName("tocNumber")]
        public string? TocNumber { get; set; }

        [JsonPropertyName("tocStyle")]
        public string? TocStyle { get; set; }

        [JsonPropertyName("subItems")]
        public List<HierarchyItemDto>? SubItems { get; set; }
    }
}

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

    /// <summary>
    /// Build the prompt that would be sent to the LLM (for testing/debugging)
    /// </summary>
    /// <param name="normalizedXml">The normalized XHTML document to analyze</param>
    /// <param name="exampleHierarchies">2-3 user-selected training examples showing hierarchy structure</param>
    /// <param name="anonymize">Whether to anonymize example header names (default: true)</param>
    /// <returns>The full prompt string</returns>
    string BuildPromptForTesting(
        string normalizedXml,
        List<string> exampleHierarchies,
        bool anonymize = true);
}

public class HierarchyGeneratorService : IHierarchyGeneratorService
{
    private readonly IOllamaService _ollamaService;
    private readonly ILogger<HierarchyGeneratorService> _logger;
    private readonly RuleBasedHierarchyGenerator _ruleBasedGenerator;
    private readonly IConfiguration _configuration;

    public HierarchyGeneratorService(
        IOllamaService ollamaService,
        ILogger<HierarchyGeneratorService> logger,
        RuleBasedHierarchyGenerator ruleBasedGenerator,
        IConfiguration configuration)
    {
        _ollamaService = ollamaService;
        _logger = logger;
        _ruleBasedGenerator = ruleBasedGenerator;
        _configuration = configuration;
    }

    public string BuildPromptForTesting(
        string normalizedXml,
        List<string> exampleHierarchies,
        bool anonymize = true)
    {
        return BuildPrompt(normalizedXml, exampleHierarchies, anonymize);
    }

    public async Task<HierarchyProposal> GenerateHierarchyAsync(
        string normalizedXml,
        List<string> exampleHierarchies,
        string modelName = "llama3.1:70b",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check hierarchy generation mode from configuration
            var mode = _configuration["HierarchyGeneration:Mode"] ?? "RuleBased";

            if (mode.Equals("RuleBased", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("[HierarchyGenerator] Using rule-based generation (fast, deterministic)");

                // Extract headers from XML
                var extractedHeaders = ExtractHeadersFromXml(normalizedXml);

                // Call rule-based generator (returns GenerationResult with Root, GenericLogs, TechnicalLogs)
                var ruleBasedStartTime = DateTime.UtcNow;
                var result = _ruleBasedGenerator.GenerateHierarchy(extractedHeaders);
                var rootItem = result.Root;
                var ruleBasedDuration = DateTime.UtcNow - ruleBasedStartTime;

                // Log the generic logs (user-friendly)
                foreach (var log in result.GenericLogs)
                {
                    _logger.LogInformation("[HierarchyGenerator] {Log}", log);
                }

                // Build proposal with same structure as LLM approach
                var allItems = new List<HierarchyItem>();
                CollectAllItems(rootItem, allItems);

                _logger.LogInformation("[HierarchyGenerator] Rule-based generation completed in {Duration:F3}s: " +
                                       "{TotalItems} items, 100% confidence",
                    ruleBasedDuration.TotalSeconds, allItems.Count);

                return new HierarchyProposal
                {
                    Root = rootItem,
                    OverallConfidence = 100,
                    Uncertainties = new List<HierarchyItem>(),
                    Reasoning = "Generated using deterministic rule-based approach",
                    TotalItems = allItems.Count,
                    ValidationResult = new HierarchyValidationResult { IsValid = true },
                    RuleBasedGenerationResult = result // Include logs for UI display
                };
            }

            // Fall through to LLM-based generation
            _logger.LogInformation("[HierarchyGenerator] Starting LLM-based hierarchy generation with model: {Model}, " +
                                   "XML length: {XmlLength} chars, Examples: {ExampleCount}",
                modelName, normalizedXml.Length, exampleHierarchies.Count);

            // Extract headers to build whitelist (needed for both prompt and validation)
            var headers = ExtractHeadersFromXml(normalizedXml);
            var whitelistHeaders = headers.Select(h => h.Text).ToList();

            // Build 4-part prompt (anonymize=false to show real example headers for better pattern learning)
            var prompt = BuildPrompt(normalizedXml, exampleHierarchies, anonymize: false);

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
            var proposal = ParseJsonResponse(jsonResponse, headers);

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
    /// <param name="normalizedXml">Normalized XHTML document</param>
    /// <param name="exampleHierarchies">List of example hierarchy XMLs</param>
    /// <param name="anonymize">Whether to anonymize example header names (default: true)</param>
    /// <returns>Full prompt string ready for LLM</returns>
    private string BuildPrompt(string normalizedXml, List<string> exampleHierarchies, bool anonymize = true)
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

        // Generate labeled training examples instead of anonymized hierarchies
        var labeledExamples = GenerateLabeledExamples(exampleHierarchies);

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

        // Calculate target boundary count (50% of headers, middle of 45-60% range)
        var targetCount = (int)Math.Round(headers.Count * 0.50);

        // Replace placeholders in template
        var prompt = promptTemplate
            .Replace("{{LABELED_EXAMPLES}}", labeledExamples)
            .Replace("{{DOCUMENT_STRUCTURE_PREVIEW}}", whitelist)
            .Replace("{COUNT}", headers.Count.ToString())
            .Replace("{TARGET_COUNT}", targetCount.ToString());

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
    /// Generates labeled training examples from reference hierarchies.
    /// Shows which headers WERE selected vs NOT selected to teach the LLM selection patterns.
    /// </summary>
    /// <param name="exampleHierarchyPaths">List of paths to training material hierarchy XML files</param>
    /// <returns>Formatted string with labeled examples showing SELECTED/NOT SELECTED headers</returns>
    private string GenerateLabeledExamples(List<string> exampleHierarchyPaths)
    {
        var examplesBuilder = new StringBuilder();

        _logger.LogInformation("[HierarchyGenerator] GenerateLabeledExamples called with {Count} paths", exampleHierarchyPaths.Count);

        for (int i = 0; i < exampleHierarchyPaths.Count; i++)
        {
            try
            {
                var hierarchyPath = exampleHierarchyPaths[i];
                _logger.LogInformation("[HierarchyGenerator] Example {Index} path: {Path}", i + 1, hierarchyPath);

                // Extract customer and projectId from path
                // Path format: /app/data/input/{customer}/projects/{projectId}/metadata/hierarchy-ar-pdf-en.xml
                var pathParts = hierarchyPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length < 7)
                {
                    _logger.LogWarning("[HierarchyGenerator] Invalid hierarchy path format: {Path}", hierarchyPath);
                    continue;
                }

                // Find 'input' in the path and extract customer/projectId relative to it
                var inputIndex = Array.IndexOf(pathParts, "input");
                if (inputIndex == -1 || inputIndex + 3 >= pathParts.Length)
                {
                    _logger.LogWarning("[HierarchyGenerator] Could not find 'input' in path: {Path}", hierarchyPath);
                    continue;
                }

                var customer = pathParts[inputIndex + 1]; // After 'input'
                var projectId = pathParts[inputIndex + 3]; // After 'projects'

                _logger.LogInformation("[HierarchyGenerator] Processing example {Index}: {Customer}/{ProjectId}",
                    i + 1, customer, projectId);

                // Load normalized XML for this project
                var normalizedXmlPath = $"/app/data/output/{customer}/projects/{projectId}/normalized.xml";
                if (!File.Exists(normalizedXmlPath))
                {
                    _logger.LogWarning("[HierarchyGenerator] Normalized XML not found: {Path}", normalizedXmlPath);
                    continue;
                }

                var normalizedXml = File.ReadAllText(normalizedXmlPath);
                var whitelistHeaders = ExtractHeadersFromXml(normalizedXml);

                // Load reference hierarchy to get selected items
                if (!File.Exists(hierarchyPath))
                {
                    _logger.LogWarning("[HierarchyGenerator] Hierarchy file not found: {Path}", hierarchyPath);
                    continue;
                }

                var hierarchyXml = File.ReadAllText(hierarchyPath);
                var selectedItems = ExtractSelectedItemsFromHierarchy(hierarchyXml);

                // Match whitelist against selected items
                var labeledLines = GenerateLabeledLinesForExample(whitelistHeaders, selectedItems);

                // Build example section
                examplesBuilder.AppendLine($"### Example {i + 1}: {customer}/{projectId} ({whitelistHeaders.Count} headers, {selectedItems.Count} selected)");
                examplesBuilder.AppendLine();
                examplesBuilder.Append(labeledLines);
                examplesBuilder.AppendLine();

                _logger.LogInformation("[HierarchyGenerator] Generated example {Index}: {Total} headers, {Selected} selected",
                    i + 1, whitelistHeaders.Count, selectedItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HierarchyGenerator] Failed to process example {Index}: {Path}",
                    i + 1, exampleHierarchyPaths[i]);
            }
        }

        if (examplesBuilder.Length == 0)
        {
            _logger.LogWarning("[HierarchyGenerator] No labeled examples generated - returning placeholder");
            return "(No training examples available)";
        }

        return examplesBuilder.ToString();
    }

    /// <summary>
    /// Extracts selected item names from a reference hierarchy XML.
    /// Returns a dictionary mapping item names to their hierarchy levels.
    /// </summary>
    /// <param name="hierarchyXml">The reference hierarchy XML</param>
    /// <returns>Dictionary of selected item names with their levels</returns>
    private Dictionary<string, int> ExtractSelectedItemsFromHierarchy(string hierarchyXml)
    {
        var selectedItems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var doc = XDocument.Parse(hierarchyXml);

            // Find all item elements with linkname
            var items = doc.Descendants()
                .Where(e => e.Name.LocalName == "item")
                .ToList();

            foreach (var item in items)
            {
                var linkNameElement = item.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "linkname");

                if (linkNameElement != null)
                {
                    var linkName = linkNameElement.Value.Trim();
                    var levelAttr = item.Attribute("level")?.Value;

                    if (int.TryParse(levelAttr, out var level) && !string.IsNullOrWhiteSpace(linkName))
                    {
                        // Store with case-insensitive key
                        selectedItems[linkName] = level;
                    }
                }
            }

            _logger.LogDebug("[HierarchyGenerator] Extracted {Count} selected items from hierarchy",
                selectedItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HierarchyGenerator] Failed to parse hierarchy XML");
        }

        return selectedItems;
    }

    /// <summary>
    /// Generates labeled lines for a single training example.
    /// Shows first ~30 lines with SELECTED/NOT SELECTED labels.
    /// </summary>
    /// <param name="whitelistHeaders">All headers from normalized XML</param>
    /// <param name="selectedItems">Dictionary of selected items with levels from hierarchy</param>
    /// <returns>Formatted string with labeled lines</returns>
    private string GenerateLabeledLinesForExample(
        List<HeaderInfo> whitelistHeaders,
        Dictionary<string, int> selectedItems)
    {
        var sb = new StringBuilder();
        var maxLinesToShow = Math.Min(30, whitelistHeaders.Count);

        for (int i = 0; i < maxLinesToShow; i++)
        {
            var header = whitelistHeaders[i];
            var lineNumber = i + 1;
            var headerText = header.Text;

            // Truncate long headers for readability
            var displayText = headerText.Length > 80
                ? headerText.Substring(0, 77) + "..."
                : headerText;

            // Check if this header was selected in the reference hierarchy
            if (selectedItems.TryGetValue(headerText, out var level))
            {
                // Format level description
                var levelDesc = level switch
                {
                    0 => "root",
                    1 => "level 1",
                    2 => "level 2 note",
                    3 => "level 3 sub-note",
                    _ => $"level {level}"
                };

                sb.Append($"Line {lineNumber}: \"{displayText}\"");
                if (!string.IsNullOrEmpty(header.DataNumber))
                {
                    sb.Append($" (data-number=\"{header.DataNumber}\")");
                }
                sb.AppendLine($" → SELECTED ({levelDesc})");
            }
            else
            {
                // Not selected - this is in-section content
                sb.Append($"Line {lineNumber}: \"{displayText}\"");
                if (!string.IsNullOrEmpty(header.DataNumber))
                {
                    sb.Append($" (data-number=\"{header.DataNumber}\")");
                }
                sb.AppendLine(" → NOT SELECTED");
            }
        }

        // Add ellipsis if there are more lines
        if (whitelistHeaders.Count > maxLinesToShow)
        {
            sb.AppendLine($"... ({whitelistHeaders.Count - maxLinesToShow} more lines)");
        }

        return sb.ToString();
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
    /// TEMPORARY: Parses LEADING section number prefixes from header text:
    ///   - "1. Directors" → "Directors" with data-number="1."
    ///   - "(i) Foreign currency" → "Foreign currency" with data-number="(i)"
    /// Also parses trailing numbers: "Financial performance 2" → "Financial performance" with data-number="2"
    /// NOW INCLUDES: Rich context extraction (word counts, child headers, content preview, etc.)
    /// </summary>
    /// <param name="normalizedXml">The normalized XHTML document</param>
    /// <returns>List of header information objects with rich context</returns>
    private List<HeaderInfo> ExtractHeadersFromXml(string normalizedXml)
    {
        try
        {
            var doc = XDocument.Parse(normalizedXml);
            var headers = new List<HeaderInfo>();

            // TEMPORARY: Patterns to match LEADING section number prefixes
            // Pattern 1: "1." or "1.2." or "1.2.3." - numbered prefixes with dot
            var leadingNumberPattern = new Regex(@"^([\d\.]+\.)\s+(.+)$", RegexOptions.Compiled);

            // Pattern 2: "(i)" or "(ii)" or "(a)" or "(b)" - parenthesized prefixes
            var leadingParenPattern = new Regex(@"^(\([a-z]+\)|\([ivxlcdm]+\))\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Pattern 3: "a." or "i." - letter/roman numeral with dot
            var leadingLetterPattern = new Regex(@"^([a-z]+\.|[ivxlcdm]+\.)\s+(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // Pattern to match trailing section numbers: "Header Text 1.2.3"
            var trailingNumberPattern = new Regex(@"^(.+?)\s+([\d\.]+)$", RegexOptions.Compiled);

            // Get all h1, h2, h3, h4, h5, h6 elements
            var headerElements = doc.Descendants()
                .Where(e => e.Name.LocalName.ToLower().StartsWith("h") &&
                           e.Name.LocalName.Length == 2 &&
                           char.IsDigit(e.Name.LocalName[1]))
                .ToList();

            // Track parent stack for hierarchy context
            var parentStack = new Stack<(int depth, string text)>();

            for (int i = 0; i < headerElements.Count; i++)
            {
                var element = headerElements[i];
                var text = element.Value.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue; // Skip empty headers

                // Check for data-number attribute first
                var dataNumber = element.Attribute("data-number")?.Value;

                // TEMPORARY: If no data-number attribute, try to extract from LEADING prefix
                if (string.IsNullOrEmpty(dataNumber))
                {
                    // Try pattern 1: "1. Text" or "1.2. Text"
                    var match = leadingNumberPattern.Match(text);
                    if (match.Success)
                    {
                        dataNumber = match.Groups[1].Value;  // e.g., "1." or "1.2."
                        text = match.Groups[2].Value.Trim(); // Clean header text
                    }
                    else
                    {
                        // Try pattern 2: "(i) Text" or "(a) Text"
                        match = leadingParenPattern.Match(text);
                        if (match.Success)
                        {
                            dataNumber = match.Groups[1].Value;  // e.g., "(i)" or "(a)"
                            text = match.Groups[2].Value.Trim(); // Clean header text
                        }
                        else
                        {
                            // Try pattern 3: "a. Text" or "i. Text"
                            match = leadingLetterPattern.Match(text);
                            if (match.Success)
                            {
                                dataNumber = match.Groups[1].Value;  // e.g., "a." or "i."
                                text = match.Groups[2].Value.Trim(); // Clean header text
                            }
                            else
                            {
                                // Try pattern 4: trailing numbers (original logic)
                                match = trailingNumberPattern.Match(text);
                                if (match.Success)
                                {
                                    text = match.Groups[1].Value.Trim();  // Clean header text
                                    dataNumber = match.Groups[2].Value;   // Extracted number
                                }
                            }
                        }
                    }
                }

                var info = new HeaderInfo
                {
                    Text = text,
                    DataNumber = dataNumber,
                    Level = element.Name.LocalName.ToLower()
                };

                // Extract depth level from tag name (h1=1, h2=2, etc.)
                info.DepthLevel = int.Parse(info.Level.Substring(1));

                // Extract content between this header and the next header
                var contentNodes = GetContentUntilNextHeader(element);

                // Count words (strip HTML, split by whitespace)
                var textContent = string.Join(" ", contentNodes.Select(n => StripHtml(n.ToString())));
                info.WordCount = textContent.Split(new[] { ' ', '\n', '\r', '\t' },
                    StringSplitOptions.RemoveEmptyEntries).Length;

                // Count paragraphs
                info.ParagraphCount = contentNodes.Count(n => n.Name?.LocalName == "p");

                // Content preview (first 150 chars)
                info.ContentPreview = textContent.Length > 150
                    ? textContent.Substring(0, 150) + "..."
                    : textContent;

                // Count child headers (headers with higher depth before next same-level header)
                info.ChildHeaderCount = CountChildHeaders(i, headerElements);

                // Detect parent header
                while (parentStack.Count > 0 && parentStack.Peek().depth >= info.DepthLevel)
                {
                    parentStack.Pop();
                }
                if (parentStack.Count > 0)
                {
                    info.ParentHeaderText = parentStack.Peek().text;
                }
                parentStack.Push((info.DepthLevel, info.Text));

                // Detect numbered sequence
                info.IsPartOfNumberedSequence = DetectNumberedSequence(i, headers, info.DepthLevel, dataNumber);

                // Count siblings at same level
                var siblingsInfo = CountSiblingsAtSameLevel(i, headerElements, info.DepthLevel);
                info.SiblingPosition = siblingsInfo.position;
                info.TotalSiblings = siblingsInfo.total;

                // Check for tables in content
                info.HasTables = contentNodes.Any(n => n.Descendants().Any(d => d.Name?.LocalName == "table"));
                info.TableCount = contentNodes.SelectMany(n => n.Descendants()).Count(d => d.Name?.LocalName == "table");

                headers.Add(info);
            }

            _logger.LogInformation("[HierarchyGenerator] Extracted {Count} headers from normalized XML, " +
                "{WithDataNumber} with data-number attributes, with rich context (word counts, child headers, etc.)",
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
    /// Gets all content nodes between this header and the next header
    /// </summary>
    private List<XElement> GetContentUntilNextHeader(XElement headerElement)
    {
        var contentNodes = new List<XElement>();
        var currentNode = headerElement.NextNode;

        while (currentNode != null)
        {
            if (currentNode is XElement element)
            {
                // Stop if we hit another header
                var localName = element.Name.LocalName.ToLower();
                if (localName.StartsWith("h") && localName.Length == 2 && char.IsDigit(localName[1]))
                {
                    break;
                }
                contentNodes.Add(element);
            }
            currentNode = currentNode.NextNode;
        }

        return contentNodes;
    }

    /// <summary>
    /// Strips HTML tags from a string, keeping only text content
    /// </summary>
    private string StripHtml(string html)
    {
        try
        {
            var doc = XDocument.Parse($"<root>{html}</root>");
            return doc.Root?.Value ?? string.Empty;
        }
        catch
        {
            // Fallback: simple regex-based stripping
            return Regex.Replace(html, "<.*?>", string.Empty);
        }
    }

    /// <summary>
    /// Counts child headers (headers with higher depth before next same-level header)
    /// </summary>
    private int CountChildHeaders(int currentIndex, List<XElement> headerElements)
    {
        if (currentIndex >= headerElements.Count - 1)
            return 0;

        var currentDepth = int.Parse(headerElements[currentIndex].Name.LocalName.Substring(1));
        var childCount = 0;

        for (int i = currentIndex + 1; i < headerElements.Count; i++)
        {
            var depth = int.Parse(headerElements[i].Name.LocalName.Substring(1));

            // Stop if we hit a header at same or lower level
            if (depth <= currentDepth)
                break;

            childCount++;
        }

        return childCount;
    }

    /// <summary>
    /// Detects if this header is part of a numbered sequence (1., 2., 3... or a., b., c...)
    /// </summary>
    private bool DetectNumberedSequence(int currentIndex, List<HeaderInfo> previousHeaders, int currentDepth, string? dataNumber)
    {
        if (string.IsNullOrEmpty(dataNumber) || currentIndex == 0)
            return false;

        // Look back for previous header at same depth
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var prevHeader = previousHeaders[i];

            // Skip headers at deeper levels
            if (prevHeader.DepthLevel > currentDepth)
                continue;

            // Stop if we hit a shallower level
            if (prevHeader.DepthLevel < currentDepth)
                break;

            // Same level - check if data-numbers are sequential
            if (!string.IsNullOrEmpty(prevHeader.DataNumber))
            {
                return IsSequential(prevHeader.DataNumber, dataNumber);
            }

            break;
        }

        return false;
    }

    /// <summary>
    /// Checks if two data-numbers are sequential (e.g., "1." and "2." or "a." and "b.")
    /// </summary>
    private bool IsSequential(string prev, string current)
    {
        // Handle numeric sequences: "1." -> "2."
        if (int.TryParse(prev.TrimEnd('.'), out int prevNum) &&
            int.TryParse(current.TrimEnd('.'), out int currentNum))
        {
            return currentNum == prevNum + 1;
        }

        // Handle letter sequences: "a." -> "b."
        var prevLetter = prev.TrimEnd('.').ToLower();
        var currentLetter = current.TrimEnd('.').ToLower();

        if (prevLetter.Length == 1 && currentLetter.Length == 1 &&
            char.IsLetter(prevLetter[0]) && char.IsLetter(currentLetter[0]))
        {
            return currentLetter[0] == prevLetter[0] + 1;
        }

        return false;
    }

    /// <summary>
    /// Counts siblings at the same level and returns position/total
    /// </summary>
    private (int position, int total) CountSiblingsAtSameLevel(int currentIndex, List<XElement> headerElements, int targetDepth)
    {
        // Find the parent header (first header with lower depth before current)
        int parentIndex = -1;
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            var depth = int.Parse(headerElements[i].Name.LocalName.Substring(1));
            if (depth < targetDepth)
            {
                parentIndex = i;
                break;
            }
        }

        // Count siblings at same level under same parent
        int startIndex = parentIndex + 1;
        int endIndex = headerElements.Count;

        // Find where parent's children end
        for (int i = startIndex; i < headerElements.Count; i++)
        {
            var depth = int.Parse(headerElements[i].Name.LocalName.Substring(1));
            if (depth < targetDepth)
            {
                endIndex = i;
                break;
            }
        }

        // Count siblings at exact same depth
        var siblings = new List<int>();
        for (int i = startIndex; i < endIndex; i++)
        {
            var depth = int.Parse(headerElements[i].Name.LocalName.Substring(1));
            if (depth == targetDepth)
            {
                siblings.Add(i);
            }
        }

        var position = siblings.IndexOf(currentIndex) + 1;
        var total = siblings.Count;

        return (position, total);
    }

    /// <summary>
    /// Anonymizes numbers in text content to prevent data leakage while preserving structure.
    /// Replaces dates, money amounts, percentages, and other numeric values with random values.
    /// </summary>
    /// <param name="text">Text containing potentially sensitive numeric data</param>
    /// <returns>Text with anonymized numbers</returns>
    private string AnonymizeTextNumbers(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var random = new Random();

        // 1. Dates - Years (1900-2099)
        text = Regex.Replace(text, @"\b(19|20)\d{2}\b",
            m => {
                var year = random.Next(1900, 2100);
                return year.ToString();
            });

        // 2. Full dates (31 December 2024, December 31 2024, etc.)
        var months = new[] { "January", "February", "March", "April", "May", "June",
                            "July", "August", "September", "October", "November", "December" };
        text = Regex.Replace(text, @"\b\d{1,2}\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{4}\b",
            m => {
                var day = random.Next(1, 29);
                var month = months[random.Next(months.Length)];
                var year = random.Next(1900, 2100);
                return $"{day} {month} {year}";
            }, RegexOptions.IgnoreCase);

        // 3. Money amounts with dollar signs and commas ($138,611,000 or $92.50)
        text = Regex.Replace(text, @"\$\d{1,3}(,\d{3})*(\.\d{2})?",
            m => {
                var hasDecimals = m.Value.Contains(".");
                var value = random.Next(1000000, 999999999);
                if (hasDecimals)
                    return "$" + value.ToString("N2");
                else
                    return "$" + value.ToString("N0");
            });

        // 4. Percentages (31.5%, 100%)
        text = Regex.Replace(text, @"\b\d+\.?\d*%",
            m => {
                var hasDecimals = m.Value.Contains(".");
                if (hasDecimals)
                    return (random.Next(0, 10000) / 100.0).ToString("F1") + "%";
                else
                    return random.Next(1, 100) + "%";
            });

        // 5. Large numbers with commas (138,611,000)
        text = Regex.Replace(text, @"\b\d{1,3}(,\d{3})+\b",
            m => {
                var value = random.Next(100000, 999999999);
                return value.ToString("N0");
            });

        // 6. Decimal numbers (31.5, 1.234)
        text = Regex.Replace(text, @"\b\d+\.\d+\b",
            m => {
                var decimals = m.Value.Split('.')[1].Length;
                var value = random.Next(1, 1000) + random.NextDouble();
                return value.ToString($"F{decimals}");
            });

        // 7. Simple integers (but avoid replacing years already handled)
        // Be conservative here to avoid breaking formatting
        text = Regex.Replace(text, @"(?<!\d)\b\d{1,2}\b(?!\d)",
            m => random.Next(1, 99).ToString());

        return text;
    }

    /// <summary>
    /// Builds markdown-formatted whitelist of headers for LLM prompt with rich context.
    /// Returns numbered list with data-number attributes and context metadata.
    /// </summary>
    /// <param name="headers">List of extracted headers with rich context</param>
    /// <returns>Markdown formatted whitelist string with context</returns>
    private string BuildMarkdownWhitelist(List<HeaderInfo> headers)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            var lineNum = i + 1;

            // Truncate very long headers
            var displayText = h.Text.Length > 100
                ? h.Text.Substring(0, 97) + "..."
                : h.Text;

            // Line 1: Header text with data-number
            sb.Append($"{lineNum} | {displayText}");
            if (!string.IsNullOrEmpty(h.DataNumber))
            {
                sb.Append($" (data-number=\"{h.DataNumber}\")");
            }
            sb.AppendLine();

            // Line 2: Level, children, content stats
            sb.Append($"    Level: {h.Level} | Children: {h.ChildHeaderCount} | ");
            sb.Append($"Words: {h.WordCount} | Paragraphs: {h.ParagraphCount}");
            if (h.TableCount > 0)
            {
                sb.Append($" | Tables: {h.TableCount}");
            }
            sb.AppendLine();

            // Line 3: Parent and position info (if applicable)
            if (!string.IsNullOrEmpty(h.ParentHeaderText) || h.TotalSiblings > 1)
            {
                sb.Append("    ");
                if (!string.IsNullOrEmpty(h.ParentHeaderText))
                {
                    // Truncate parent text for display
                    var parentDisplay = h.ParentHeaderText.Length > 50
                        ? h.ParentHeaderText.Substring(0, 47) + "..."
                        : h.ParentHeaderText;
                    sb.Append($"Parent: \"{parentDisplay}\" | ");
                }
                if (h.TotalSiblings > 1)
                {
                    sb.Append($"Position: {h.SiblingPosition} of {h.TotalSiblings}");
                }
                sb.AppendLine();
            }

            // Line 4: Content preview (if available) - ANONYMIZE NUMBERS
            if (!string.IsNullOrWhiteSpace(h.ContentPreview) && h.ContentPreview.Length > 3) // More than "..."
            {
                var anonymizedPreview = AnonymizeTextNumbers(h.ContentPreview);
                sb.AppendLine($"    Preview: \"{anonymizedPreview}\"");
            }

            // Line 5: Pattern detection
            if (h.IsPartOfNumberedSequence)
            {
                sb.AppendLine("    Pattern: Numbered item in sequence");
            }
            else if (h.ChildHeaderCount > 5)
            {
                sb.AppendLine($"    Pattern: Container for {h.ChildHeaderCount} subsections");
            }

            sb.AppendLine(); // Blank line between headers
        }

        _logger.LogInformation("[HierarchyGenerator] Built markdown whitelist: {Count} headers with rich context (word counts, child counts, patterns)",
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
    /// Helper class for header information extracted from XML with rich context
    /// </summary>
    public class HeaderInfo
    {
        // Existing fields
        public string Text { get; set; } = string.Empty;
        public string? DataNumber { get; set; }
        public string Level { get; set; } = string.Empty; // "h1", "h2", etc.

        // NEW: Rich context fields
        public int DepthLevel { get; set; }  // 1 for h1, 2 for h2, etc.
        public int ChildHeaderCount { get; set; }  // Number of child headers
        public int WordCount { get; set; }  // Word count in content under this header
        public int ParagraphCount { get; set; }  // Paragraph count
        public string ContentPreview { get; set; } = string.Empty;  // First 150 chars of content
        public string? ParentHeaderText { get; set; }  // Parent header text (if any)
        public bool IsPartOfNumberedSequence { get; set; }  // Is this in a 1,2,3... or a,b,c... sequence?
        public int SiblingPosition { get; set; }  // Position among siblings (1 of 5, etc.)
        public int TotalSiblings { get; set; }  // Total siblings at same level
        public bool HasTables { get; set; }  // Contains tables
        public int TableCount { get; set; }  // Number of tables
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
    /// Uses simplified line-number approach: LLM outputs integers, C# looks up header text.
    /// </summary>
    private HierarchyProposal ParseJsonResponse(string jsonResponse, List<HeaderInfo> headers)
    {
        try
        {
            // Clean up response (remove markdown blocks if present)
            var cleanJson = jsonResponse.Trim();
            if (cleanJson.StartsWith("```json"))
                cleanJson = cleanJson.Substring(7);
            if (cleanJson.StartsWith("```"))
                cleanJson = cleanJson.Substring(3);
            if (cleanJson.EndsWith("```"))
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            _logger.LogDebug("[HierarchyGenerator] Cleaned JSON length: {Length} chars", cleanJson.Length);

            // Deserialize to simplified DTO
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var dto = JsonSerializer.Deserialize<HierarchyDecisionResponseDto>(cleanJson, options);

            if (dto == null || dto.BoundaryLines == null || !dto.BoundaryLines.Any())
            {
                throw new InvalidOperationException(
                    "LLM returned null or empty boundaryLines array");
            }

            // Validate line numbers
            var invalidLines = dto.BoundaryLines
                .Where(line => line < 1 || line > headers.Count)
                .ToList();

            if (invalidLines.Any())
            {
                throw new InvalidOperationException(
                    $"LLM returned {invalidLines.Count} invalid line numbers (range: 1-{headers.Count}): " +
                    $"{string.Join(", ", invalidLines.Take(5))}");
            }

            // Check for duplicates
            var duplicates = dto.BoundaryLines
                .GroupBy(x => x)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                _logger.LogWarning("[HierarchyGenerator] Found {Count} duplicate line numbers: {Lines}",
                    duplicates.Count, string.Join(", ", duplicates.Take(5)));
            }

            // Check boundary count is reasonable (20-80% of total headers)
            var boundaryCount = dto.BoundaryLines.Count;
            var totalHeaders = headers.Count;
            var boundaryPercent = (boundaryCount / (double)totalHeaders) * 100;

            if (boundaryPercent < 10 || boundaryPercent > 90)
            {
                _logger.LogWarning("[HierarchyGenerator] Boundary count seems unusual: {Count}/{Total} ({Percent:F1}%)",
                    boundaryCount, totalHeaders, boundaryPercent);
            }

            _logger.LogInformation("[HierarchyGenerator] Parsed {Count} boundary lines ({Percent:F1}% of {Total} headers)",
                boundaryCount, boundaryPercent, totalHeaders);

            // Build hierarchy structure from line numbers
            var rootItem = BuildHierarchyFromLineNumbers(dto.BoundaryLines, headers);

            // Collect all items and validate
            var allItems = new List<HierarchyItem>();
            CollectAllItems(rootItem, allItems);

            // Extract header text for validation
            var whitelistHeaders = headers.Select(h => h.Text).ToList();

            // Validation: NO hallucinations possible since we look up headers from whitelist
            var validationResult = ValidateDecisionBasedHierarchy(allItems, whitelistHeaders);

            // No uncertainties with this approach (100% confidence)
            var uncertainties = new List<HierarchyItem>();

            _logger.LogInformation("[HierarchyGenerator] Built hierarchy: {Total} items from {Boundaries} boundary lines",
                allItems.Count, boundaryCount);

            return new HierarchyProposal
            {
                Root = rootItem,
                OverallConfidence = 100, // Full confidence since we look up headers directly
                Uncertainties = uncertainties,
                Reasoning = dto.Reasoning ?? string.Empty,
                TotalItems = allItems.Count,
                ValidationResult = validationResult
            };
        }
        catch (JsonException ex)
        {
            // Existing error handling...
            _logger.LogError(ex, "[HierarchyGenerator] JSON parsing failed. " +
                "Response length: {Length} chars. Exception: {Message}",
                jsonResponse.Length, ex.Message);

            var lines = jsonResponse.Split('\n');
            _logger.LogError("[HierarchyGenerator] Response has {LineCount} lines. First 10 lines:\n{FirstLines}",
                lines.Length, string.Join("\n", lines.Take(10)));

            if (lines.Length > 10)
            {
                _logger.LogError("[HierarchyGenerator] Last 10 lines:\n{LastLines}",
                    string.Join("\n", lines.TakeLast(10)));
            }

            throw new InvalidOperationException(
                $"Failed to parse LLM JSON response ({jsonResponse.Length} chars). Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Builds hierarchy from line numbers by looking up headers from whitelist.
    /// Determines nesting based on data-number patterns.
    /// </summary>
    private HierarchyItem BuildHierarchyFromLineNumbers(
        List<int> boundaryLines,
        List<HeaderInfo> headers)
    {
        // Create root
        var rootItem = new HierarchyItem
        {
            Id = "report-root",
            Level = 0,
            LinkName = "Annual Report 2024",
            DataRef = "report-root.xml",
            Path = "/",
            SubItems = new List<HierarchyItem>()
        };

        // Build hierarchy items from boundary lines
        var sortedLines = boundaryLines.Distinct().OrderBy(x => x).ToList();
        var itemsByDataNumber = new Dictionary<string, HierarchyItem>();
        var lastItemAtLevel = new Dictionary<int, HierarchyItem>();

        foreach (var lineNumber in sortedLines)
        {
            if (lineNumber < 1 || lineNumber > headers.Count)
            {
                _logger.LogWarning("[HierarchyGenerator] Skipping invalid line number: {Line}", lineNumber);
                continue;
            }

            var header = headers[lineNumber - 1]; // Convert 1-based to 0-based
            var headerText = header.Text;
            var dataNumber = header.DataNumber;
            var normalizedId = FilenameUtils.NormalizeFileName(headerText);

            var item = new HierarchyItem
            {
                Id = normalizedId,
                LinkName = headerText,
                DataRef = $"{normalizedId}.xml",
                Confidence = 100, // Full confidence with lookup approach
                SubItems = new List<HierarchyItem>()
            };

            // Determine parent and level based on data-number
            HierarchyItem? parent = null;
            int level = 1;

            if (!string.IsNullOrEmpty(dataNumber))
            {
                // Parse data-number to determine hierarchy
                // Examples: "1." → level 1, "1.1." → level 2, "1.1.1." → level 3
                var parts = dataNumber.TrimEnd('.').Split('.');
                level = parts.Length;

                // Find parent based on data-number prefix
                if (level > 1)
                {
                    var parentNumber = string.Join(".", parts.Take(parts.Length - 1)) + ".";
                    if (itemsByDataNumber.TryGetValue(parentNumber, out parent))
                    {
                        level = parent.Level + 1;
                    }
                    else
                    {
                        // Try to find by last item at parent level
                        if (lastItemAtLevel.TryGetValue(level - 1, out parent))
                        {
                            level = parent.Level + 1;
                        }
                        else
                        {
                            // Fallback to root
                            level = 1;
                        }
                    }
                }

                // Track by data-number for future parent lookups
                itemsByDataNumber[dataNumber] = item;
            }
            else
            {
                // No data-number: use last item at same level as parent
                if (lastItemAtLevel.TryGetValue(level - 1, out parent))
                {
                    level = parent.Level + 1;
                }
                else if (lastItemAtLevel.Any())
                {
                    // Default to last created item as potential parent
                    parent = lastItemAtLevel.Values.LastOrDefault();
                    if (parent != null)
                    {
                        level = parent.Level + 1;
                    }
                }
            }

            // Set level and path
            item.Level = level;

            if (parent != null)
            {
                item.Path = parent.Path;
                parent.SubItems.Add(item);
            }
            else
            {
                // Top-level item
                item.Path = "/";
                rootItem.SubItems.Add(item);
            }

            lastItemAtLevel[level] = item;
        }

        _logger.LogInformation("[HierarchyGenerator] Built hierarchy from {Count} line numbers: " +
            "{TopLevel} top-level items, max depth {MaxDepth}",
            sortedLines.Count, rootItem.SubItems.Count,
            lastItemAtLevel.Keys.Any() ? lastItemAtLevel.Keys.Max() : 0);

        return rootItem;
    }

    /// <summary>
    /// Validates decision-based hierarchy (should have zero hallucinations by design).
    /// Verifies header text matches whitelist exactly.
    /// </summary>
    private HierarchyValidationResult ValidateDecisionBasedHierarchy(
        List<HierarchyItem> allItems,
        List<string> whitelistedHeaders)
    {
        var result = new HierarchyValidationResult { IsValid = true };

        // With task inversion, hallucinations are STRUCTURALLY IMPOSSIBLE
        // But verify text matching for safety
        var whitelistSet = new HashSet<string>(whitelistedHeaders, StringComparer.Ordinal);

        var mismatches = allItems
            .Where(item => item.Level > 0) // Skip root
            .Where(item => !whitelistSet.Contains(item.LinkName.Trim()))
            .ToList();

        if (mismatches.Any())
        {
            result.IsValid = false;
            result.HallucinatedItems = mismatches.Select(m => m.LinkName).Distinct().ToList();
            result.Summary = $"⚠️ Found {mismatches.Count} text mismatches (should be impossible with task inversion!)";

            _logger.LogWarning(
                "[HierarchyGenerator] UNEXPECTED: Task inversion produced mismatches: {Items}",
                string.Join(", ", result.HallucinatedItems.Take(5)));
        }

        return result;
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
    /// Simplified DTO for line-number-based approach.
    /// LLM only outputs integers, C# looks up actual header text.
    /// This prevents hallucinations since LLM never generates header names.
    /// </summary>
    private class HierarchyDecisionResponseDto
    {
        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [JsonPropertyName("boundaryLines")]
        public List<int>? BoundaryLines { get; set; }
    }

    /// <summary>
    /// Old DTO classes (kept for backward compatibility - not used with task inversion).
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

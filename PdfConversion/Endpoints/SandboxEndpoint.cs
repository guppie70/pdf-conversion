using PdfConversion.Models;
using PdfConversion.Services;

namespace PdfConversion.Endpoints;

/// <summary>
/// Sandbox endpoint for testing prompt generation and LLM comparison.
///
/// MODES:
///
/// 1. LLM Comparison (default):
///    Compares local LLM responses with Claude Sonnet 4 for hierarchy generation prompts.
///
///    Usage:
///      curl http://localhost:8085/sandbox                 # Cached mode (reads saved responses)
///      curl http://localhost:8085/sandbox?liveApi=true    # Live API mode (calls Anthropic API)
///      curl http://localhost:8085/sandbox?approach=1      # Test single approach (1-4)
///
///    Cached Mode (default):
///      - Reads existing claude-response.json files from approach directories
///      - Fast iteration without API costs
///      - Shows "No cached response found" if files don't exist
///
///    Live API Mode (?liveApi=true):
///      - Calls Anthropic API for each approach
///      - Saves responses to claude-response.json
///      - Requires ANTHROPIC_API_KEY environment variable
///
/// 2. Prompt Generation (?mode=prompt-gen):
///    Generates and returns the prompt that would be sent to the LLM (original sandbox functionality).
///
///    Usage:
///      curl http://localhost:8085/sandbox?mode=prompt-gen              # anonymized examples (default: false)
///      curl http://localhost:8085/sandbox?mode=prompt-gen&anonymize=true   # anonymized examples
///      curl http://localhost:8085/sandbox?mode=prompt-gen&anonymize=false  # real examples
///
///    How to test with different data:
///      1. Edit the hardcoded values at the top of HandlePromptGenerationAsync() method below
///      2. Hot-reload applies changes automatically (no restart needed)
///      3. Re-run curl command to see updated prompt
///
/// Benefits:
///   - Test prompt generation logic in isolation
///   - Inspect full prompt before sending to LLM
///   - Iterate quickly on prompt engineering
///   - Compare local vs Claude responses side-by-side
///   - Verify examples are loaded correctly
///   - Check anonymization behavior
/// </summary>
public static class SandboxEndpoint
{
    /// <summary>
    /// Handles the /sandbox endpoint request - routes to different utilities based on mode parameter.
    ///
    /// DEFAULT (no mode parameter): Latest active test (currently test-ollama)
    ///
    /// Available modes:
    ///   - llm-comparison: Compare local LLM with Claude Sonnet for hierarchy generation
    ///   - prompt-gen: Generate hierarchy generation prompt
    ///   - test-hierarchy: Test hierarchy XML serialization
    ///   - test-ascii: Test ASCII normalization
    ///   - test-mode-persistence: Test mode persistence for GenerateHierarchy page
    ///   - test-root-normalization: Test root element normalization
    ///   - test-fix-existing: Test fixing existing hierarchy files
    /// </summary>
    public static async Task HandleAsync(
        HttpContext context,
        IXsltTransformationService xsltService,
        IHierarchyGeneratorService hierarchyGeneratorService,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        // Check query parameters to route to different utilities
        var mode = context.Request.Query["mode"].FirstOrDefault();

        if (mode == "llm-comparison")
        {
            // LLM comparison (was the old default)
            var useLiveApi = bool.Parse(context.Request.Query["liveApi"].FirstOrDefault() ?? "false");
            await HandleLlmComparisonAsync(context, logger, hierarchyGeneratorService, hierarchyService, useLiveApi);
        }
        else if (mode == "prompt-gen")
        {
            await HandlePromptGenerationAsync(context, xsltService, hierarchyGeneratorService, logger);
        }
        else if (mode == "test-hierarchy")
        {
            await HandleTestHierarchyAsync(context, hierarchyService, logger);
        }
        else if (mode == "test-ascii")
        {
            await HandleTestAsciiNormalizationAsync(context, hierarchyService, logger);
        }
        else if (mode == "test-mode-persistence")
        {
            await HandleTestModePersistenceAsync(context, logger);
        }
        else if (mode == "test-root-normalization")
        {
            await HandleTestRootNormalizationAsync(context, hierarchyService, logger);
        }
        else if (mode == "test-fix-existing")
        {
            await HandleTestFixExistingHierarchyAsync(context, hierarchyService, logger);
        }
        else
        {
            // DEFAULT: Latest active test (currently test-tiebreakers)
            await HandleTestTiebreakersAsync(context, logger);
        }
    }

    /// <summary>
    /// Generates and returns the prompt that would be sent to the LLM (original sandbox functionality).
    /// </summary>
    private static async Task HandlePromptGenerationAsync(
        HttpContext context,
        IXsltTransformationService xsltService,
        IHierarchyGeneratorService hierarchyService,
        ILogger logger)
    {
        try
        {
            // ========================================
            // HARDCODED TEST VALUES - EDIT THESE FOR DIFFERENT TEST DATA
            // ========================================
            var project = "optiver/projects/ar24-6";
            var sourceXml = "docling-output.xml";
            var xslt = "docling/transformation.xslt";
            var examples = "optiver/projects/ar24-3,optiver/projects/ar24-6";
            // ========================================

            // Only keep anonymize as optional parameter (default false)
            var anonymize = bool.Parse(context.Request.Query["anonymize"].FirstOrDefault() ?? "false");

            logger.LogInformation(
                "Sandbox: project={Project}, sourceXml={SourceXml}, xslt={Xslt}, examples={Examples}, anonymize={Anonymize}",
                project, sourceXml, xslt, examples, anonymize);

            // Construct source XML path
            var sourceXmlPath = Path.Combine("/app/data/input", project, sourceXml);
            if (!File.Exists(sourceXmlPath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(
                    $"Source XML not found: {sourceXmlPath}\n\n" +
                    $"To use different test data, edit the hardcoded values in SandboxEndpoint.cs around line 48.");
                return;
            }

            // Read source XML
            var sourceXmlContent = await File.ReadAllTextAsync(sourceXmlPath);

            // Construct XSLT path
            var xsltPath = Path.Combine("/app/xslt", xslt);
            if (!File.Exists(xsltPath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(
                    $"XSLT file not found: {xsltPath}\n\n" +
                    $"To use different XSLT, edit the hardcoded values in SandboxEndpoint.cs around line 48.");
                return;
            }

            // Read XSLT content
            var xsltContent = await File.ReadAllTextAsync(xsltPath);

            // Transform source XML to normalized XML using XSLT3Service
            var transformOptions = new PdfConversion.Models.TransformationOptions
            {
                UseXslt3Service = true
            };

            var transformResult = await xsltService.TransformAsync(
                sourceXmlContent,
                xsltContent,
                transformOptions,
                xsltPath);

            if (!transformResult.IsSuccess)
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(
                    $"XSLT transformation failed: {transformResult.ErrorMessage}");
                return;
            }

            var normalizedXml = transformResult.OutputContent ?? "";

            // Load example hierarchies
            var examplePaths = examples.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var exampleHierarchies = new List<string>();
            var missingExamples = new List<string>();

            foreach (var examplePath in examplePaths)
            {
                var hierarchyFile = Path.Combine("/app/data/output", examplePath.Trim(), "hierarchy.xml");

                if (!File.Exists(hierarchyFile))
                {
                    missingExamples.Add(hierarchyFile);
                    continue;
                }

                var hierarchyContent = await File.ReadAllTextAsync(hierarchyFile);
                exampleHierarchies.Add(hierarchyContent);
            }

            if (missingExamples.Any())
            {
                logger.LogWarning(
                    "Some example hierarchies not found: {MissingExamples}",
                    string.Join(", ", missingExamples));
            }

            if (!exampleHierarchies.Any())
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync(
                    $"No valid example hierarchy files found.\n\n" +
                    $"Missing files:\n{string.Join("\n", missingExamples)}\n\n" +
                    $"To use different examples, edit the hardcoded values in SandboxEndpoint.cs around line 48.");
                return;
            }

            logger.LogInformation(
                "Loaded {Count} example hierarchies, anonymize={Anonymize}",
                exampleHierarchies.Count, anonymize);

            // Build prompt using HierarchyGeneratorService
            var prompt = hierarchyService.BuildPromptForTesting(
                normalizedXml,
                exampleHierarchies,
                anonymize);

            logger.LogInformation(
                "Generated prompt: {Size} chars (~{Tokens} tokens)",
                prompt.Length, prompt.Length / 4);

            // Return prompt as plain text
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(prompt);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sandbox endpoint");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                $"Error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Calls the Anthropic API to generate a hierarchy using Claude Sonnet 4.
    /// </summary>
    private static async Task<object> CallAnthropicApiAsync(
        string promptText,
        string apiKey,
        string approach,
        ILogger logger)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)  // Increased timeout for large responses
        };
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 16384,  // Increased from 8192 to handle larger hierarchies
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = promptText
                }
            }
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync(
                "https://api.anthropic.com/v1/messages",
                content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var errorInfo = new
                {
                    StatusCode = (int)response.StatusCode,
                    StatusText = response.StatusCode.ToString(),
                    ErrorBody = errorBody,
                    Timestamp = DateTime.UtcNow,
                    Approach = approach
                };

                logger.LogError(
                    "Anthropic API failed for {Approach}: {Status} - {Error}",
                    approach,
                    response.StatusCode,
                    errorBody
                );

                return errorInfo;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);

            // Extract text from content array
            var claudeText = responseJson
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString();

            // Check stop_reason to detect truncation
            if (responseJson.TryGetProperty("stop_reason", out var stopReason))
            {
                var reason = stopReason.GetString();
                logger.LogInformation(
                    "Anthropic API stop_reason for {Approach}: {Reason}",
                    approach,
                    reason);

                if (reason == "max_tokens")
                {
                    logger.LogWarning(
                        "Response for {Approach} was truncated due to max_tokens limit. Response may be incomplete.",
                        approach);
                }
            }

            return claudeText ?? string.Empty;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error calling Anthropic API for {Approach}", approach);
            return new { Error = ex.Message, Type = "NetworkError", Approach = approach };
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Timeout calling Anthropic API for {Approach}", approach);
            return new { Error = "Request timeout", Type = "Timeout", Approach = approach };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error for {Approach}", approach);
            return new { Error = ex.Message, Type = ex.GetType().Name, Approach = approach };
        }
    }

    /// <summary>
    /// Parses hierarchy XML file and returns nested <ul>/<li> HTML tree structure.
    /// </summary>
    private static string ParseHierarchyXmlToTree(string xmlPath, ILogger logger)
    {
        try
        {
            if (!File.Exists(xmlPath))
            {
                return "<div style='color: #9D9D9D; font-style: italic;'>(not found)</div>";
            }

            var xmlContent = File.ReadAllText(xmlPath);
            var doc = System.Xml.Linq.XDocument.Parse(xmlContent);

            // Find root item (level 0)
            var rootItem = doc.Descendants("item")
                .FirstOrDefault(x => x.Attribute("level")?.Value == "0");

            if (rootItem == null)
            {
                return "<div style='color: #F85149;'>Error: No root item found</div>";
            }

            // Build tree HTML recursively
            var treeHtml = new System.Text.StringBuilder();
            treeHtml.Append("<div class='hierarchy-tree'>");
            treeHtml.Append(BuildTreeHtml(rootItem));
            treeHtml.Append("</div>");

            return treeHtml.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse hierarchy XML: {Path}", xmlPath);
            return $"<div style='color: #F85149;'>Error: {ex.Message}</div>";
        }
    }

    /// <summary>
    /// Recursively builds HTML tree structure from hierarchy XML item element.
    /// </summary>
    private static string BuildTreeHtml(System.Xml.Linq.XElement itemElement)
    {
        var sb = new System.Text.StringBuilder();

        // Get linkname from <web_page><linkname>
        var linkname = itemElement.Descendants("linkname").FirstOrDefault()?.Value ?? "Unknown";

        sb.Append("<ul>");
        sb.Append($"<li>{System.Web.HttpUtility.HtmlEncode(linkname)}");

        // Process sub_items recursively
        var subItems = itemElement.Element("sub_items")?.Elements("item");
        if (subItems != null && subItems.Any())
        {
            foreach (var subItem in subItems)
            {
                sb.Append(BuildTreeHtml(subItem));
            }
        }

        sb.Append("</li>");
        sb.Append("</ul>");

        return sb.ToString();
    }

    /// <summary>
    /// Builds HTML comparison view for single approach (not currently used, kept for reference).
    /// </summary>
    private static string BuildComparisonHtml(
        string approachName,
        string localResponse,
        object claudeResponse)
    {
        var isError = claudeResponse is not string;
        var claudeContent = isError
            ? System.Text.Json.JsonSerializer.Serialize(claudeResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
            : (string)claudeResponse;

        // Format JSON for display
        string FormatJson(string json)
        {
            try
            {
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                return System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                return json;
            }
        }

        var localFormatted = FormatJson(localResponse);
        var claudeFormatted = isError ? claudeContent : FormatJson(claudeContent);

        var errorStyle = isError
            ? "background: #5A1D1D; border: 2px solid #F85149;"
            : "";

        var errorHeader = isError
            ? "<div style='color: #F85149; font-weight: bold; margin-bottom: 8px;'>❌ API ERROR</div>"
            : "";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>LLM Comparison - {approachName}</title>
    <style>
        body {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
            margin: 0;
            padding: 20px;
        }}
        h1 {{
            color: #FFFFFF;
            font-size: 24px;
            margin-bottom: 8px;
        }}
        h2 {{
            color: #FFFFFF;
            font-size: 18px;
            margin-top: 32px;
            margin-bottom: 16px;
        }}
        .approach-name {{
            color: #9D9D9D;
            font-size: 14px;
            margin-bottom: 24px;
        }}
        .comparison-container {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            margin-bottom: 40px;
        }}
        .panel {{
            background: #181818;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            overflow: hidden;
        }}
        .panel-header {{
            background: #1F1F1F;
            color: #FFFFFF;
            padding: 12px 16px;
            border-bottom: 1px solid #2B2B2B;
            font-weight: 600;
        }}
        .panel-subheader {{
            color: #9D9D9D;
            font-size: 12px;
            font-weight: normal;
            margin-top: 4px;
        }}
        .panel-content {{
            padding: 16px;
            max-height: 600px;
            overflow-y: auto;
        }}
        pre {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: Consolas, Monaco, 'Courier New', monospace;
            font-size: 12px;
            margin: 0;
            padding: 16px;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            white-space: pre-wrap;
            word-wrap: break-word;
            {errorStyle}
        }}
        .back-link {{
            display: inline-block;
            color: #4daafc;
            text-decoration: none;
            margin-top: 20px;
            padding: 8px 16px;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            background: #181818;
        }}
        .back-link:hover {{
            background: #1F1F1F;
            border-color: #0078D4;
        }}
    </style>
</head>
<body>
    <h1>LLM Comparison Results</h1>
    <div class='approach-name'>Approach: {approachName}</div>

    <div class='comparison-container'>
        <div class='panel'>
            <div class='panel-header'>
                Local LLM
                <div class='panel-subheader'>deepseek-coder:33b</div>
            </div>
            <div class='panel-content'>
                <pre>{System.Web.HttpUtility.HtmlEncode(localFormatted)}</pre>
            </div>
        </div>

        <div class='panel'>
            <div class='panel-header'>
                Claude Sonnet 4
                <div class='panel-subheader'>claude-sonnet-4-20250514</div>
            </div>
            <div class='panel-content'>
                {errorHeader}
                <pre>{System.Web.HttpUtility.HtmlEncode(claudeFormatted)}</pre>
            </div>
        </div>
    </div>

    <a href='/sandbox' class='back-link'>← Run All Approaches</a>
</body>
</html>";
    }

    /// <summary>
    /// Compares local LLM responses with Claude Sonnet 4 for hierarchy generation prompts.
    /// </summary>
    private static async Task HandleLlmComparisonAsync(
        HttpContext context,
        ILogger logger,
        IHierarchyGeneratorService hierarchyGeneratorService,
        IHierarchyService hierarchyService,
        bool useLiveApi = false)
    {
        // Check API key only if live API mode is requested
        string? apiKey = null;
        if (useLiveApi)
        {
            apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    Error = "ANTHROPIC_API_KEY environment variable not set",
                    Help = "Set it in docker-compose.yml or via: export ANTHROPIC_API_KEY='sk-ant-...'"
                });
                return;
            }
        }

        // Get approach parameter
        var approachParam = context.Request.Query["approach"].FirstOrDefault();

        var approaches = new[]
        {
            "1-full-generation-approach",
            "2-task-inversion-line-numbers",
            "3-labeled-training-examples",
            "4-context-aware-metadata"
        };

        // Filter to single approach if specified
        if (!string.IsNullOrEmpty(approachParam) && int.TryParse(approachParam, out int approachNum))
        {
            if (approachNum >= 1 && approachNum <= 4)
            {
                approaches = new[] { approaches[approachNum - 1] };
            }
        }

        var htmlParts = new List<string>();

        // Determine mode banner
        var modeBanner = useLiveApi
            ? "<div style='background: #F85149; color: white; padding: 12px; margin-bottom: 20px; border-radius: 4px; font-weight: 600;'>🔴 LIVE API MODE - Calling Anthropic API</div>"
            : "<div style='background: #0078D4; color: white; padding: 12px; margin-bottom: 20px; border-radius: 4px; font-weight: 600;'>📦 CACHED MODE - Using saved responses</div>";

        htmlParts.Add($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>LLM Comparison Results</title>
    <style>
        body {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
            margin: 0;
            padding: 20px;
        }}
        h1 {{
            color: #FFFFFF;
            font-size: 24px;
            margin-bottom: 32px;
        }}
        .approach-section {{
            margin-bottom: 60px;
        }}
        .approach-header {{
            color: #FFFFFF;
            font-size: 18px;
            margin-bottom: 16px;
            padding-bottom: 8px;
            border-bottom: 2px solid #0078D4;
        }}
        .comparison-container {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }}
        .comparison-container-triple {{
            display: grid;
            grid-template-columns: 1fr 1fr 1fr;
            gap: 15px;
        }}
        .panel {{
            background: #181818;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            overflow: hidden;
        }}
        .panel-header {{
            background: #1F1F1F;
            color: #FFFFFF;
            padding: 12px 16px;
            border-bottom: 1px solid #2B2B2B;
            font-weight: 600;
        }}
        .panel-subheader {{
            color: #9D9D9D;
            font-size: 12px;
            font-weight: normal;
            margin-top: 4px;
        }}
        .panel-content {{
            padding: 16px;
            max-height: 600px;
            overflow-y: auto;
        }}
        pre {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: Consolas, Monaco, 'Courier New', monospace;
            font-size: 12px;
            margin: 0;
            padding: 16px;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            white-space: pre-wrap;
            word-wrap: break-word;
        }}
        .error-panel pre {{
            background: #5A1D1D;
            border: 2px solid #F85149;
        }}
        .error-header {{
            color: #F85149;
            font-weight: bold;
            margin-bottom: 8px;
        }}
        .hierarchy-tree {{
            font-family: Consolas, Monaco, 'Courier New', monospace;
            font-size: 13px;
            color: #CCCCCC;
        }}
        .hierarchy-tree ul {{
            list-style-type: none;
            padding-left: 20px;
            margin: 4px 0;
        }}
        .hierarchy-tree li {{
            margin: 2px 0;
            position: relative;
        }}
        .hierarchy-tree li::before {{
            content: '├─ ';
            color: #6E7681;
            position: absolute;
            left: -20px;
        }}
        .hierarchy-tree li:last-child::before {{
            content: '└─ ';
        }}
        .hierarchy-tree > div > ul {{
            padding-left: 0;
        }}
        .hierarchy-tree > div > ul > li::before {{
            content: '';
        }}
    </style>
</head>
<body>
    {modeBanner}
    <h1>LLM Comparison Results</h1>
");

        foreach (var approach in approaches)
        {
            logger.LogInformation("Processing approach: {Approach}", approach);

            var basePath = $"/app/data/llm-development/{approach}";
            var originalPath = "/app/data/input/optiver/projects/ar24-6/metadata/hierarchy-ar-pdf-en.xml";

            // Check directory exists
            if (!Directory.Exists(basePath))
            {
                logger.LogWarning("Skipping {Approach} - directory not found", approach);
                continue;
            }

            // Read files
            var promptPath = Path.Combine(basePath, "prompt.txt");
            var localResponsePath = Path.Combine(basePath, "response.json");

            if (!File.Exists(promptPath) || !File.Exists(localResponsePath))
            {
                logger.LogWarning("Skipping {Approach} - missing prompt.txt or response.json", approach);
                continue;
            }

            var promptText = await File.ReadAllTextAsync(promptPath);
            var localResponse = await File.ReadAllTextAsync(localResponsePath);

            // Get Claude response: either from cache or live API
            object claudeResponse;
            bool isError = false;

            if (!useLiveApi)
            {
                // CACHED MODE: Read from existing file
                var cachedPath = Path.Combine(basePath, "claude-response.json");
                if (File.Exists(cachedPath))
                {
                    var cachedText = await File.ReadAllTextAsync(cachedPath);
                    claudeResponse = cachedText;
                    logger.LogInformation("Using cached response from {Path}", cachedPath);
                }
                else
                {
                    // No cached response found
                    claudeResponse = new
                    {
                        Error = "No cached response found",
                        Type = "CacheMiss",
                        Path = cachedPath,
                        Help = "Run with ?liveApi=true to call the API and generate this file"
                    };
                    isError = true;
                    logger.LogWarning("No cached response at {Path}", cachedPath);
                }
            }
            else
            {
                // LIVE API MODE: Call Anthropic API
                logger.LogInformation("Calling Anthropic API for {Approach}", approach);
                claudeResponse = await CallAnthropicApiAsync(promptText, apiKey!, approach, logger);
                isError = claudeResponse is not string;

                // Save response to cache
                var outputPath = Path.Combine(basePath,
                    isError ? "claude-response-error.json" : "claude-response.json");

                var responseTextToSave = claudeResponse is string
                    ? (string)claudeResponse
                    : System.Text.Json.JsonSerializer.Serialize(claudeResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                // Try to pretty-print if valid JSON
                if (!isError)
                {
                    try
                    {
                        var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseTextToSave);
                        responseTextToSave = System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions
                        {
                            WriteIndented = true
                        });
                    }
                    catch
                    {
                        // Keep as-is if not valid JSON
                    }
                }

                await File.WriteAllTextAsync(outputPath, responseTextToSave);
                logger.LogInformation("Saved response to {Path}", outputPath);
            }

            // Prepare response text for display
            var responseText = claudeResponse is string
                ? (string)claudeResponse
                : System.Text.Json.JsonSerializer.Serialize(claudeResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            // Format JSON for display
            string FormatJson(string json)
            {
                try
                {
                    var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                    return System.Text.Json.JsonSerializer.Serialize(jsonObj, new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                }
                catch
                {
                    return json;
                }
            }

            var localFormatted = FormatJson(localResponse);
            var claudeFormatted = FormatJson(responseText);

            // Reconstruct hierarchies from JSON responses (all approaches)
            string? localHierarchyXml = null;
            string? claudeHierarchyXml = null;

            // Parse hierarchy trees for visual comparison
            var localTreeHtml = "";
            var claudeTreeHtml = "";
            var originalTreeHtml = "";

            if (approach.Contains("1-full") || approach.Contains("2-task") || approach.Contains("3-labeled") || approach.Contains("4-context"))
            {
                try
                {
                    // Load normalized XML to get headers
                    var normalizedXmlPath = "/app/data/output/optiver/projects/ar24-6/normalized.xml";
                    if (File.Exists(normalizedXmlPath))
                    {
                        var normalizedXml = await File.ReadAllTextAsync(normalizedXmlPath);

                        // Parse local LLM response
                        if (!string.IsNullOrEmpty(localResponse) && !isError)
                        {
                            try
                            {
                                PdfConversion.Models.HierarchyProposal? localProposal = null;

                                if (approach.Contains("1-full"))
                                {
                                    // Full generation approach - complete hierarchy in JSON
                                    localProposal = await ReconstructHierarchyFromJsonAsync(
                                        localResponse,
                                        normalizedXml,
                                        hierarchyGeneratorService,
                                        logger);
                                }
                                else
                                {
                                    // Boundary line approaches (2, 3, 4) - line numbers only
                                    var approachPromptPath = Path.Combine(basePath, "prompt.txt");
                                    var approachPromptText = await File.ReadAllTextAsync(approachPromptPath);

                                    localProposal = await ReconstructHierarchyFromBoundaryLinesAsync(
                                        localResponse,
                                        approachPromptText,
                                        normalizedXml,
                                        hierarchyGeneratorService,
                                        logger);
                                }

                                if (localProposal != null)
                                {
                                    var localStructure = new PdfConversion.Models.HierarchyStructure { Root = localProposal.Root };
                                    var localXmlPath = Path.Combine(basePath, "local-llm-reconstructed-hierarchy.xml");
                                    await hierarchyService.SaveHierarchyAsync(localXmlPath, localStructure);
                                    localHierarchyXml = await File.ReadAllTextAsync(localXmlPath);
                                    logger.LogInformation("Saved local LLM hierarchy to {Path}", localXmlPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to reconstruct local LLM hierarchy");
                                localHierarchyXml = $"<!-- Error reconstructing local LLM hierarchy: {ex.Message} -->";
                            }
                        }

                        // Parse Claude response
                        if (claudeResponse is string claudeText && !string.IsNullOrEmpty(claudeText) && !isError)
                        {
                            try
                            {
                                PdfConversion.Models.HierarchyProposal? claudeProposal = null;

                                if (approach.Contains("1-full"))
                                {
                                    // Full generation approach - complete hierarchy in JSON
                                    claudeProposal = await ReconstructHierarchyFromJsonAsync(
                                        claudeText,
                                        normalizedXml,
                                        hierarchyGeneratorService,
                                        logger);
                                }
                                else
                                {
                                    // Boundary line approaches (2, 3, 4) - line numbers only
                                    var claudePromptPath = Path.Combine(basePath, "prompt.txt");
                                    var claudePromptText = await File.ReadAllTextAsync(claudePromptPath);

                                    claudeProposal = await ReconstructHierarchyFromBoundaryLinesAsync(
                                        claudeText,
                                        claudePromptText,
                                        normalizedXml,
                                        hierarchyGeneratorService,
                                        logger);
                                }

                                if (claudeProposal != null)
                                {
                                    var claudeStructure = new PdfConversion.Models.HierarchyStructure { Root = claudeProposal.Root };
                                    var claudeXmlPath = Path.Combine(basePath, "claude-sonnet-reconstructed-hierarchy.xml");
                                    await hierarchyService.SaveHierarchyAsync(claudeXmlPath, claudeStructure);
                                    claudeHierarchyXml = await File.ReadAllTextAsync(claudeXmlPath);
                                    logger.LogInformation("Saved Claude hierarchy to {Path}", claudeXmlPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.LogWarning(ex, "Failed to reconstruct Claude hierarchy");
                                claudeHierarchyXml = $"<!-- Error reconstructing Claude hierarchy: {ex.Message} -->";
                            }
                        }

                        // Generate tree HTML visualizations from saved XML files
                        localTreeHtml = ParseHierarchyXmlToTree(
                            Path.Combine(basePath, "local-llm-reconstructed-hierarchy.xml"),
                            logger);
                        claudeTreeHtml = ParseHierarchyXmlToTree(
                            Path.Combine(basePath, "claude-sonnet-reconstructed-hierarchy.xml"),
                            logger);
                        originalTreeHtml = ParseHierarchyXmlToTree(originalPath, logger);
                    }
                    else
                    {
                        logger.LogWarning("Normalized XML not found: {Path}", normalizedXmlPath);
                        localHierarchyXml = "<!-- Normalized XML not found -->";
                        claudeHierarchyXml = "<!-- Normalized XML not found -->";
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reconstruct hierarchies for {Approach}", approach);
                    localHierarchyXml = $"<!-- Error: {ex.Message} -->";
                    claudeHierarchyXml = $"<!-- Error: {ex.Message} -->";
                }
            }

            var errorClass = isError ? "error-panel" : "";
            var errorHeader = isError
                ? "<div class='error-header'>❌ API ERROR</div>"
                : "";

            var sourceIndicator = useLiveApi
                ? "<span style='color: #F85149; font-size: 11px; margin-left: 8px;'>🔴 LIVE</span>"
                : "<span style='color: #4daafc; font-size: 11px; margin-left: 8px;'>📦 CACHED</span>";

            // Determine if we should use triple-column layout (when we have reconstructed XML)
            var useTripleColumn = !string.IsNullOrEmpty(localHierarchyXml) || !string.IsNullOrEmpty(claudeHierarchyXml);
            var containerClass = useTripleColumn ? "comparison-container-triple" : "comparison-container";

            htmlParts.Add($@"
    <div class='approach-section'>
        <div class='approach-header'>{approach}</div>
        <div class='{containerClass}'>
            <div class='panel'>
                <div class='panel-header'>
                    Local LLM JSON
                    <div class='panel-subheader'>deepseek-coder:33b</div>
                </div>
                <div class='panel-content'>
                    <pre>{System.Web.HttpUtility.HtmlEncode(localFormatted)}</pre>
                </div>
            </div>

            <div class='panel {errorClass}'>
                <div class='panel-header'>
                    Claude Sonnet JSON {sourceIndicator}
                    <div class='panel-subheader'>claude-sonnet-4-20250514</div>
                </div>
                <div class='panel-content'>
                    {errorHeader}
                    <pre>{System.Web.HttpUtility.HtmlEncode(claudeFormatted)}</pre>
                </div>
            </div>");

            // Add third column for reconstructed hierarchies (only for Approach 1)
            if (useTripleColumn)
            {
                htmlParts.Add($@"
            <div class='panel'>
                <div class='panel-header'>
                    Reconstructed Hierarchy XMLs
                    <div class='panel-subheader'>Generated from JSON responses</div>
                </div>
                <div class='panel-content'>
                    <div style='margin-bottom: 20px;'>
                        <strong style='color: #0078D4;'>Local LLM:</strong>
                        <pre style='margin-top: 8px;'>{System.Web.HttpUtility.HtmlEncode(localHierarchyXml ?? "N/A")}</pre>
                    </div>
                    <div>
                        <strong style='color: #0078D4;'>Claude Sonnet:</strong>
                        <pre style='margin-top: 8px;'>{System.Web.HttpUtility.HtmlEncode(claudeHierarchyXml ?? "N/A")}</pre>
                    </div>
                </div>
            </div>");
            }

            htmlParts.Add($@"
        </div>");

            // Add hierarchy tree comparison section (only if trees were generated)
            if (!string.IsNullOrEmpty(localTreeHtml) || !string.IsNullOrEmpty(claudeTreeHtml) || !string.IsNullOrEmpty(originalTreeHtml))
            {
                htmlParts.Add($@"

        <!-- Hierarchy Tree Comparison -->
        <div class='approach-section' style='margin-top: 30px;'>
            <div class='approach-header'>Hierarchy Structure Comparison</div>
            <div class='comparison-container-triple'>
                <div class='panel'>
                    <div class='panel-header'>Local LLM Hierarchy</div>
                    <div class='panel-content'>
                        {localTreeHtml}
                    </div>
                </div>
                <div class='panel'>
                    <div class='panel-header'>Claude Sonnet Hierarchy</div>
                    <div class='panel-content'>
                        {claudeTreeHtml}
                    </div>
                </div>
                <div class='panel'>
                    <div class='panel-header'>Original Ground Truth</div>
                    <div class='panel-content'>
                        {originalTreeHtml}
                    </div>
                </div>
            </div>
        </div>");
            }

            htmlParts.Add($@"
    </div>
");
        }

        htmlParts.Add(@"
</body>
</html>");

        var html = string.Join("", htmlParts);
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }

    /// <summary>
    /// Reconstructs a HierarchyProposal from JSON response (Approach 1: full generation format).
    /// Converts JSON to HierarchyItem model objects.
    /// </summary>
    private static async Task<PdfConversion.Models.HierarchyProposal?> ReconstructHierarchyFromJsonAsync(
        string jsonResponse,
        string normalizedXml,
        IHierarchyGeneratorService hierarchyGeneratorService,
        ILogger logger)
    {
        try
        {
            // Clean up JSON response (remove markdown blocks if present)
            var cleanJson = jsonResponse.Trim();
            if (cleanJson.StartsWith("```json"))
                cleanJson = cleanJson.Substring(7);
            if (cleanJson.StartsWith("```"))
                cleanJson = cleanJson.Substring(3);
            if (cleanJson.EndsWith("```"))
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            // Deserialize to full-generation DTO (Approach 1 format)
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var dto = System.Text.Json.JsonSerializer.Deserialize<HierarchyFullResponseDto>(cleanJson, options);

            if (dto == null || dto.Root == null)
            {
                logger.LogWarning("JSON response has no root element");
                return null;
            }

            // Convert DTO to HierarchyItem model
            var rootItem = ConvertDtoToHierarchyItem(dto.Root);

            // Collect all items
            var allItems = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(rootItem, allItems);

            logger.LogInformation("Reconstructed hierarchy: {Total} items from full-generation JSON",
                allItems.Count);

            return new PdfConversion.Models.HierarchyProposal
            {
                Root = rootItem,
                OverallConfidence = 100,
                Uncertainties = new List<PdfConversion.Models.HierarchyItem>(),
                Reasoning = dto.Reasoning ?? string.Empty,
                TotalItems = allItems.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconstruct hierarchy from JSON");
            return null;
        }
    }

    /// <summary>
    /// Converts HierarchyItemDto to HierarchyItem model
    /// </summary>
    private static PdfConversion.Models.HierarchyItem ConvertDtoToHierarchyItem(HierarchyItemDto dto)
    {
        var item = new PdfConversion.Models.HierarchyItem
        {
            Id = dto.Id ?? "unknown",
            Level = dto.Level,
            LinkName = dto.LinkName ?? "Unknown",
            DataRef = dto.DataRef ?? "unknown.xml",
            Path = dto.Path ?? "/",
            Confidence = dto.Confidence ?? 100,
            Reasoning = dto.Reasoning,
            SubItems = new List<PdfConversion.Models.HierarchyItem>()
        };

        if (dto.SubItems != null)
        {
            foreach (var subDto in dto.SubItems)
            {
                item.SubItems.Add(ConvertDtoToHierarchyItem(subDto));
            }
        }

        return item;
    }

    /// <summary>
    /// Builds hierarchy from line numbers by looking up headers from whitelist.
    /// Simplified version of HierarchyGeneratorService.BuildHierarchyFromLineNumbers.
    /// </summary>
    private static PdfConversion.Models.HierarchyItem BuildHierarchyFromLineNumbers(
        List<int> boundaryLines,
        List<PdfConversion.Services.HierarchyGeneratorService.HeaderInfo> headers,
        ILogger logger)
    {
        // Create root
        var rootItem = new PdfConversion.Models.HierarchyItem
        {
            Id = "report-root",
            Level = 0,
            LinkName = "Annual Report 2024",
            DataRef = "report-root.xml",
            Path = "/",
            SubItems = new List<PdfConversion.Models.HierarchyItem>()
        };

        // Build hierarchy items from boundary lines
        var sortedLines = boundaryLines.Distinct().OrderBy(x => x).ToList();
        var itemsByDataNumber = new Dictionary<string, PdfConversion.Models.HierarchyItem>();
        var lastItemAtLevel = new Dictionary<int, PdfConversion.Models.HierarchyItem>();

        foreach (var lineNumber in sortedLines)
        {
            if (lineNumber < 1 || lineNumber > headers.Count)
            {
                logger.LogWarning("Skipping invalid line number: {Line}", lineNumber);
                continue;
            }

            var header = headers[lineNumber - 1]; // Convert 1-based to 0-based
            var headerText = header.Text;
            var dataNumber = header.DataNumber;
            var normalizedId = PdfConversion.Utils.FilenameUtils.NormalizeFileName(headerText);

            var item = new PdfConversion.Models.HierarchyItem
            {
                Id = normalizedId,
                LinkName = headerText,
                DataRef = $"{normalizedId}.xml",
                Confidence = 100,
                SubItems = new List<PdfConversion.Models.HierarchyItem>()
            };

            // Determine parent and level based on data-number
            PdfConversion.Models.HierarchyItem? parent = null;
            int level = 1;

            if (!string.IsNullOrEmpty(dataNumber))
            {
                // Parse data-number to determine hierarchy
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
                    else if (lastItemAtLevel.TryGetValue(level - 1, out parent))
                    {
                        level = parent.Level + 1;
                    }
                    else
                    {
                        level = 1;
                    }
                }

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
                item.Path = "/";
                rootItem.SubItems.Add(item);
            }

            lastItemAtLevel[level] = item;
        }

        return rootItem;
    }

    /// <summary>
    /// Collects all items from the hierarchy tree into a flat list
    /// </summary>
    private static void CollectAllItems(PdfConversion.Models.HierarchyItem item, List<PdfConversion.Models.HierarchyItem> accumulator)
    {
        accumulator.Add(item);
        foreach (var subItem in item.SubItems)
        {
            CollectAllItems(subItem, accumulator);
        }
    }

    /// <summary>
    /// Reconstructs a HierarchyProposal from boundary line JSON response (Approaches 3-4).
    /// Parses boundaryLines array and maps to headers from the prompt whitelist.
    /// </summary>
    private static async Task<PdfConversion.Models.HierarchyProposal?> ReconstructHierarchyFromBoundaryLinesAsync(
        string jsonResponse,
        string promptText,
        string normalizedXml,
        IHierarchyGeneratorService hierarchyGeneratorService,
        ILogger logger)
    {
        try
        {
            // Clean up JSON response (remove markdown blocks if present)
            var cleanJson = jsonResponse.Trim();
            if (cleanJson.StartsWith("```json"))
                cleanJson = cleanJson.Substring(7);
            if (cleanJson.StartsWith("```"))
                cleanJson = cleanJson.Substring(3);
            if (cleanJson.EndsWith("```"))
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            cleanJson = cleanJson.Trim();

            // Deserialize to decision DTO (Approaches 3-4 format)
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            var dto = System.Text.Json.JsonSerializer.Deserialize<HierarchyDecisionResponseDto>(cleanJson, options);

            if (dto == null || dto.BoundaryLines == null || !dto.BoundaryLines.Any())
            {
                logger.LogWarning("JSON response has no boundary lines");
                return null;
            }

            // Parse headers from prompt whitelist (PART 2 section)
            var headers = ParseHeadersFromPrompt(promptText, logger);
            if (headers.Count == 0)
            {
                logger.LogWarning("Failed to parse headers from prompt");
                return null;
            }

            // Build hierarchy from boundary lines
            var rootItem = BuildHierarchyFromLineNumbers(dto.BoundaryLines, headers, logger);

            // Collect all items
            var allItems = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(rootItem, allItems);

            logger.LogInformation("Reconstructed hierarchy: {Total} items from boundary lines {Lines}",
                allItems.Count, string.Join(",", dto.BoundaryLines));

            return new PdfConversion.Models.HierarchyProposal
            {
                Root = rootItem,
                OverallConfidence = 100,
                Uncertainties = new List<PdfConversion.Models.HierarchyItem>(),
                Reasoning = dto.Reasoning ?? string.Empty,
                TotalItems = allItems.Count
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconstruct hierarchy from boundary lines");
            return null;
        }
    }

    /// <summary>
    /// Parses headers from prompt PART 2 whitelist section.
    /// Returns list of HeaderInfo objects.
    /// </summary>
    private static List<PdfConversion.Services.HierarchyGeneratorService.HeaderInfo> ParseHeadersFromPrompt(
        string promptText,
        ILogger logger)
    {
        var headers = new List<PdfConversion.Services.HierarchyGeneratorService.HeaderInfo>();

        try
        {
            // Find PART 2 section
            var part2Start = promptText.IndexOf("## PART 2: CANDIDATE HEADERS", StringComparison.Ordinal);
            if (part2Start == -1)
            {
                logger.LogWarning("Could not find 'PART 2: CANDIDATE HEADERS' in prompt");
                return headers;
            }

            // Find next section (PART 3)
            var part2End = promptText.IndexOf("## PART 3:", part2Start, StringComparison.Ordinal);
            if (part2End == -1)
                part2End = promptText.IndexOf("## PART 4:", part2Start, StringComparison.Ordinal);
            if (part2End == -1)
                part2End = promptText.Length;

            var part2Text = promptText.Substring(part2Start, part2End - part2Start);

            // Parse lines: "1 | Directors' report"
            var lines = part2Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || !trimmed.Contains('|'))
                    continue;

                var parts = trimmed.Split('|', 2);
                if (parts.Length != 2)
                    continue;

                var lineNumber = parts[0].Trim();
                var headerText = parts[1].Trim();

                // Extract data-number if present
                var dataNumber = "";
                var dataNumberMatch = System.Text.RegularExpressions.Regex.Match(headerText, @"\(data-number=""([^""]+)""\)");
                if (dataNumberMatch.Success)
                {
                    dataNumber = dataNumberMatch.Groups[1].Value;
                    headerText = headerText.Replace(dataNumberMatch.Value, "").Trim();
                }

                if (int.TryParse(lineNumber, out var _))
                {
                    headers.Add(new PdfConversion.Services.HierarchyGeneratorService.HeaderInfo
                    {
                        Text = headerText,
                        DataNumber = dataNumber
                    });
                }
            }

            logger.LogInformation("Parsed {Count} headers from prompt PART 2", headers.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse headers from prompt");
        }

        return headers;
    }

    /// <summary>
    /// DTO for parsing line-number-based JSON responses (Approaches 2-4)
    /// </summary>
    private class HierarchyDecisionResponseDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("boundaryLines")]
        public List<int>? BoundaryLines { get; set; }
    }

    /// <summary>
    /// DTO for parsing full-generation JSON responses (Approach 1)
    /// </summary>
    private class HierarchyFullResponseDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("root")]
        public HierarchyItemDto? Root { get; set; }
    }

    /// <summary>
    /// DTO for individual hierarchy items
    /// </summary>
    private class HierarchyItemDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("level")]
        public int Level { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("linkName")]
        public string? LinkName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("dataRef")]
        public string? DataRef { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("path")]
        public string? Path { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("confidence")]
        public int? Confidence { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reasoning")]
        public string? Reasoning { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("subItems")]
        public List<HierarchyItemDto>? SubItems { get; set; }
    }

    /// <summary>
    /// Tests hierarchy XML serialization to verify:
    /// 1. Root element has id="report-root" and data-ref="report-root.xml"
    /// 2. LinkName is "Annual Report 2024"
    /// 3. Confidence attributes are NOT included in XML output
    ///
    /// Usage: curl http://localhost:8085/sandbox?mode=test-hierarchy
    /// </summary>
    private static async Task HandleTestHierarchyAsync(
        HttpContext context,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing hierarchy XML serialization");

            // Create test hierarchy with confidence values (which should NOT appear in XML)
            var testHierarchy = new PdfConversion.Models.HierarchyStructure
            {
                Root = new PdfConversion.Models.HierarchyItem
                {
                    Id = "report-root",
                    Level = 0,
                    LinkName = "Annual Report 2024",
                    DataRef = "report-root.xml",
                    Path = "/",
                    Confidence = 95, // This should NOT appear in XML
                    SubItems = new List<PdfConversion.Models.HierarchyItem>
                    {
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "directors-report",
                            Level = 1,
                            LinkName = "Directors Report",
                            DataRef = "directors-report.xml",
                            Path = "/directors-report",
                            Confidence = 85, // This should NOT appear in XML
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        },
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "financial-statements",
                            Level = 1,
                            LinkName = "Financial Statements",
                            DataRef = "financial-statements.xml",
                            Path = "/financial-statements",
                            Confidence = 90, // This should NOT appear in XML
                            IsUncertain = true, // This SHOULD appear as is-uncertain="true"
                            Reasoning = "Test reasoning",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        }
                    }
                },
                OverallConfidence = 88 // This is not saved in XML
            };

            // Save to temporary file and read back
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-hierarchy-{Guid.NewGuid()}.xml");
            await hierarchyService.SaveHierarchyAsync(tempPath, testHierarchy);
            var xml = await File.ReadAllTextAsync(tempPath);

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            // Add verification header
            var verificationResults = new System.Text.StringBuilder();
            verificationResults.AppendLine("<!-- HIERARCHY XML TEST RESULTS -->");
            verificationResults.AppendLine("<!-- ========================== -->");

            // Check for correct root attributes
            if (xml.Contains("id=\"report-root\""))
                verificationResults.AppendLine("<!-- ✅ Root ID is 'report-root' -->");
            else
                verificationResults.AppendLine("<!-- ❌ Root ID is NOT 'report-root' -->");

            if (xml.Contains("data-ref=\"report-root.xml\""))
                verificationResults.AppendLine("<!-- ✅ Data-ref is 'report-root.xml' -->");
            else
                verificationResults.AppendLine("<!-- ❌ Data-ref is NOT 'report-root.xml' -->");

            if (xml.Contains("<linkname>Annual Report 2024</linkname>"))
                verificationResults.AppendLine("<!-- ✅ LinkName is 'Annual Report 2024' -->");
            else
                verificationResults.AppendLine("<!-- ❌ LinkName is NOT 'Annual Report 2024' -->");

            // Check that confidence attribute is NOT present
            if (xml.Contains("confidence="))
                verificationResults.AppendLine("<!-- ❌ Confidence attribute found (should be removed) -->");
            else
                verificationResults.AppendLine("<!-- ✅ Confidence attribute NOT present (correct) -->");

            // Check that is-uncertain IS present when true
            if (xml.Contains("is-uncertain=\"true\""))
                verificationResults.AppendLine("<!-- ✅ is-uncertain attribute preserved (correct) -->");
            else
                verificationResults.AppendLine("<!-- ❌ is-uncertain attribute missing (should be preserved) -->");

            verificationResults.AppendLine("<!-- ========================== -->");
            verificationResults.AppendLine();

            // Return the XML with verification results
            context.Response.ContentType = "application/xml";
            await context.Response.WriteAsync(verificationResults.ToString() + xml);

            logger.LogInformation("[Sandbox] Hierarchy XML test completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Error testing hierarchy XML");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests ASCII normalization for linknames and filenames.
    /// Verifies that special Unicode characters are converted to ASCII equivalents.
    ///
    /// Usage: curl "http://localhost:8085/sandbox?mode=test-ascii"
    /// </summary>
    private static async Task HandleTestAsciiNormalizationAsync(
        HttpContext context,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing ASCII normalization");

            // Create test hierarchy with special characters
            var testHierarchy = new PdfConversion.Models.HierarchyStructure
            {
                Root = new PdfConversion.Models.HierarchyItem
                {
                    Id = "report-root",
                    Level = 0,
                    LinkName = "Annual Report 2024",
                    DataRef = "report-root.xml",
                    Path = "/",
                    SubItems = new List<PdfConversion.Models.HierarchyItem>
                    {
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "directors-report",
                            Level = 1,
                            LinkName = "Directors' report",  // Curly apostrophe
                            DataRef = "directors-report.xml",
                            Path = "/directors-report",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        },
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "ceos-message",
                            Level = 1,
                            LinkName = "CEO\u2019s \u201CSpecial\u201D Message\u20142024",  // Curly apostrophe, curly quotes, em dash
                            DataRef = "ceos-message.xml",
                            Path = "/ceos-message",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        },
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "financial-review",
                            Level = 1,
                            LinkName = "Financial Review – Year End",  // En dash, non-breaking spaces
                            DataRef = "financial-review.xml",
                            Path = "/financial-review",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        },
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "notes",
                            Level = 1,
                            LinkName = "Notes • References © 2024 ®",  // Bullet, copyright, registered
                            DataRef = "notes.xml",
                            Path = "/notes",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        }
                    }
                }
            };

            // Save to temporary file and read back
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-ascii-{Guid.NewGuid()}.xml");
            await hierarchyService.SaveHierarchyAsync(tempPath, testHierarchy);
            var xml = await File.ReadAllTextAsync(tempPath);

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            // Test filename normalization too
            var testResults = new System.Text.StringBuilder();
            testResults.AppendLine("<!-- ASCII NORMALIZATION TEST RESULTS -->");
            testResults.AppendLine("<!-- ================================= -->");
            testResults.AppendLine();
            testResults.AppendLine("<!-- LinkName Normalization Tests: -->");

            // Check each linkname in the XML
            var testCases = new[]
            {
                ("Directors\u2019 report", "Directors' report"),
                ("CEO\u2019s \u201CSpecial\u201D Message\u20142024", "CEO's \"Special\" Message-2024"),
                ("Financial Review \u2013 Year End", "Financial Review - Year End"),
                ("Notes \u2022 References \u00A9 2024 \u00AE", "Notes * References c 2024 r")
            };

            foreach (var (original, expected) in testCases)
            {
                var normalized = PdfConversion.Utils.FilenameUtils.NormalizeToAscii(original);
                if (xml.Contains($"<linkname>{expected}</linkname>"))
                {
                    testResults.AppendLine($"<!-- ✅ '{original}' → '{expected}' (found in XML) -->");
                }
                else
                {
                    testResults.AppendLine($"<!-- ❌ '{original}' → expected '{expected}' but got '{normalized}' -->");
                }
            }

            testResults.AppendLine();
            testResults.AppendLine("<!-- Filename Normalization Tests: -->");

            // Test filename normalization
            var filenameTests = new[]
            {
                ("Directors\u2019 report", "directors-report"),
                ("CEO\u2019s \u201CMessage\u201D", "ceos-message"),
                ("Year\u2013End Report", "year-end-report"),
                ("Notes \u2022 References", "notes-references")
            };

            foreach (var (original, expected) in filenameTests)
            {
                var normalized = PdfConversion.Utils.FilenameUtils.NormalizeFileName(original);
                var status = normalized == expected ? "✅" : "❌";
                testResults.AppendLine($"<!-- {status} Filename: '{original}' → '{normalized}' (expected: '{expected}') -->");
            }

            // Check that no special Unicode characters remain
            var hasSpecialChars = xml.Any(c => c == '\u2018' || c == '\u2019' || c == '\u201C' || c == '\u201D' ||
                                               c == '\u2013' || c == '\u2014' || c == '\u00B4' || c == '\u2022');

            if (hasSpecialChars)
            {
                testResults.AppendLine();
                testResults.AppendLine("<!-- ❌ WARNING: Special Unicode characters still found in XML! -->");
            }
            else
            {
                testResults.AppendLine();
                testResults.AppendLine("<!-- ✅ No special Unicode characters found in XML (all ASCII) -->");
            }

            testResults.AppendLine("<!-- ================================= -->");
            testResults.AppendLine();

            // Return the XML with test results
            context.Response.ContentType = "application/xml";
            await context.Response.WriteAsync(testResults.ToString() + xml);

            logger.LogInformation("[Sandbox] ASCII normalization test completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Error testing ASCII normalization");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests mode persistence for the GenerateHierarchy page.
    /// Returns JavaScript code to test localStorage save/restore functionality.
    ///
    /// Usage: curl "http://localhost:8085/sandbox?mode=test-mode-persistence"
    /// </summary>
    private static async Task HandleTestModePersistenceAsync(
        HttpContext context,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing mode persistence functionality");

            var html = @"<!DOCTYPE html>
<html>
<head>
    <title>Mode Persistence Test</title>
    <style>
        body {
            font-family: 'Segoe UI', sans-serif;
            padding: 20px;
            max-width: 800px;
            margin: 0 auto;
            background: #1F1F1F;
            color: #CCCCCC;
        }
        h1 { color: #FFFFFF; }
        .test-section {
            background: #2B2B2B;
            border: 1px solid #3C3C3C;
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }
        .success { color: #2EA043; }
        .error { color: #F85149; }
        .info { color: #0078D4; }
        button {
            background: #0078D4;
            color: white;
            border: none;
            padding: 10px 20px;
            margin: 5px;
            border-radius: 3px;
            cursor: pointer;
        }
        button:hover { background: #026EC1; }
        .current-value {
            background: #313131;
            padding: 5px 10px;
            border-radius: 3px;
            display: inline-block;
            margin: 10px 0;
        }
        .log-entry {
            margin: 5px 0;
            padding: 5px;
            background: #313131;
            border-left: 3px solid #0078D4;
        }
    </style>
</head>
<body>
    <h1>🧪 Mode Persistence Test</h1>

    <div class='test-section'>
        <h2>Current State</h2>
        <p>Current localStorage value: <span id='currentValue' class='current-value'>Loading...</span></p>
        <p>Page URL: <a href='/generate-hierarchy' target='_blank' style='color:#4daafc'>/generate-hierarchy</a></p>
    </div>

    <div class='test-section'>
        <h2>Test Operations</h2>
        <button onclick='saveMode(""LoadExisting"")'>Save LoadExisting Mode</button>
        <button onclick='saveMode(""Manual"")'>Save Manual Mode</button>
        <button onclick='clearMode()'>Clear Saved Mode</button>
        <button onclick='readMode()'>Read Current Mode</button>
    </div>

    <div class='test-section'>
        <h2>Test Instructions</h2>
        <ol>
            <li>Click ""Save Manual Mode"" button above</li>
            <li>Open <a href='/generate-hierarchy' target='_blank' style='color:#4daafc'>/generate-hierarchy</a> in a new tab</li>
            <li>The page should load with ""Manual Mode"" selected (blue button)</li>
            <li>Change the mode in the UI and refresh the page</li>
            <li>The selected mode should persist across page reloads</li>
        </ol>
    </div>

    <div class='test-section'>
        <h2>Activity Log</h2>
        <div id='log'></div>
    </div>

    <script>
        function log(message, type = 'info') {
            const logDiv = document.getElementById('log');
            const entry = document.createElement('div');
            entry.className = 'log-entry';
            const time = new Date().toLocaleTimeString();
            entry.innerHTML = `<span class='${type}'>[${time}]</span> ${message}`;
            logDiv.insertBefore(entry, logDiv.firstChild);
        }

        function updateCurrentValue() {
            const value = localStorage.getItem('hierarchyMode');
            const display = value || '(not set)';
            document.getElementById('currentValue').textContent = display;
            return value;
        }

        function saveMode(mode) {
            localStorage.setItem('hierarchyMode', mode);
            log(`✅ Saved mode: <strong>${mode}</strong>`, 'success');
            updateCurrentValue();
        }

        function clearMode() {
            localStorage.removeItem('hierarchyMode');
            log('🗑️ Cleared saved mode', 'info');
            updateCurrentValue();
        }

        function readMode() {
            const value = updateCurrentValue();
            if (value) {
                log(`📖 Current mode: <strong>${value}</strong>`, 'info');
            } else {
                log('📖 No mode saved in localStorage', 'info');
            }
        }

        // Initial load
        window.addEventListener('load', function() {
            const value = updateCurrentValue();
            if (value) {
                log(`📋 Found existing mode on load: <strong>${value}</strong>`, 'success');
            } else {
                log('📋 No saved mode found on load', 'info');
            }
        });

        // Listen for storage events from other tabs
        window.addEventListener('storage', function(e) {
            if (e.key === 'hierarchyMode') {
                updateCurrentValue();
                log(`🔄 Mode changed in another tab: <strong>${e.newValue}</strong>`, 'info');
            }
        });
    </script>
</body>
</html>";

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);

            logger.LogInformation("[Sandbox] Mode persistence test page served");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Error serving mode persistence test");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test handler for root element normalization.
    /// Tests that any root element gets normalized to id="report-root" and data-ref="report-root.xml".
    /// Usage: curl http://localhost:8085/sandbox?mode=test-root-normalization
    /// </summary>
    private static async Task HandleTestRootNormalizationAsync(
        HttpContext context,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing root element normalization");

            // Create test hierarchy with non-standard root ID (like existing manual-hierarchy.xml files)
            var testHierarchy = new PdfConversion.Models.HierarchyStructure
            {
                Root = new PdfConversion.Models.HierarchyItem
                {
                    // Use non-standard values that should be normalized
                    Id = "annual-report-2024",
                    Level = 0,
                    LinkName = "Annual Report 2024",
                    DataRef = "annual-report-2024.xml",
                    Path = "/",
                    SubItems = new List<PdfConversion.Models.HierarchyItem>
                    {
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "directors-report",
                            Level = 1,
                            LinkName = "Directors' Report",
                            DataRef = "directors-report.xml",
                            Path = "/directors-report",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        },
                        new PdfConversion.Models.HierarchyItem
                        {
                            Id = "financial-statements",
                            Level = 1,
                            LinkName = "Financial Statements",
                            DataRef = "financial-statements.xml",
                            Path = "/financial-statements",
                            SubItems = new List<PdfConversion.Models.HierarchyItem>()
                        }
                    }
                }
            };

            // Save to temp file to trigger normalization
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-normalization-{Guid.NewGuid()}.xml");
            await hierarchyService.SaveHierarchyAsync(tempPath, testHierarchy);

            // Read back the saved XML
            var savedXml = await File.ReadAllTextAsync(tempPath);

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            // Parse and validate the saved XML
            var doc = System.Xml.Linq.XDocument.Parse(savedXml);
            var rootElement = doc.Root?.Element("structured")?.Element("item");

            var results = new List<string>();
            results.Add("<!-- ROOT ELEMENT NORMALIZATION TEST RESULTS -->");
            results.Add("<!-- ======================================= -->");

            // Check root attributes
            var rootId = rootElement?.Attribute("id")?.Value;
            var rootDataRef = rootElement?.Attribute("data-ref")?.Value;
            var rootLinkName = rootElement?.Element("web_page")?.Element("linkname")?.Value;

            if (rootId == "report-root")
            {
                results.Add("<!-- ✅ Root ID normalized to 'report-root' -->");
            }
            else
            {
                results.Add($"<!-- ❌ Root ID NOT normalized: '{rootId}' -->");
            }

            if (rootDataRef == "report-root.xml")
            {
                results.Add("<!-- ✅ Root data-ref normalized to 'report-root.xml' -->");
            }
            else
            {
                results.Add($"<!-- ❌ Root data-ref NOT normalized: '{rootDataRef}' -->");
            }

            if (rootLinkName == "Annual Report 2024")
            {
                results.Add("<!-- ✅ Root linkname preserved as 'Annual Report 2024' -->");
            }
            else
            {
                results.Add($"<!-- ❌ Root linkname unexpected: '{rootLinkName}' -->");
            }

            // Check that child items are NOT normalized
            var firstChild = rootElement?.Element("sub_items")?.Elements("item").FirstOrDefault();
            var childId = firstChild?.Attribute("id")?.Value;

            if (childId == "directors-report")
            {
                results.Add("<!-- ✅ Child items preserved (not normalized) -->");
            }
            else
            {
                results.Add($"<!-- ❌ Child item unexpectedly changed: '{childId}' -->");
            }

            results.Add("<!-- ======================================= -->");
            results.Add("");

            // Add the actual XML to the response
            results.Add(savedXml);

            // Return results
            context.Response.ContentType = "text/xml";
            await context.Response.WriteAsync(string.Join("\n", results));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Failed to test root normalization");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test loading an existing hierarchy file with non-standard root and re-saving it.
    /// Simulates the case of ar24-6/metadata/manual-hierarchy.xml which has id="root".
    /// Usage: curl http://localhost:8085/sandbox?mode=test-fix-existing
    /// </summary>
    private static async Task HandleTestFixExistingHierarchyAsync(
        HttpContext context,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing fix for existing hierarchy with non-standard root");

            // Simulate loading the problematic ar24-6 manual-hierarchy.xml
            var problematicFilePath = "data/input/optiver/projects/ar24-6/metadata/manual-hierarchy.xml";

            HierarchyStructure loadedHierarchy;

            // Check if the actual file exists
            if (File.Exists(problematicFilePath))
            {
                logger.LogInformation("[Sandbox] Loading actual file: {Path}", problematicFilePath);
                loadedHierarchy = await hierarchyService.LoadHierarchyAsync(problematicFilePath);
            }
            else
            {
                // Simulate the problematic structure
                logger.LogInformation("[Sandbox] File not found, simulating problematic structure");
                loadedHierarchy = new PdfConversion.Models.HierarchyStructure
                {
                    Root = new PdfConversion.Models.HierarchyItem
                    {
                        // Problematic values from ar24-6
                        Id = "root",
                        Level = 0,
                        LinkName = "Root",
                        DataRef = "root.xml",
                        Path = "/",
                        SubItems = new List<PdfConversion.Models.HierarchyItem>
                        {
                            new PdfConversion.Models.HierarchyItem
                            {
                                Id = "directors-report",
                                Level = 1,
                                LinkName = "Directors' report",
                                DataRef = "directors-report.xml",
                                Path = "/",
                                SubItems = new List<PdfConversion.Models.HierarchyItem>()
                            }
                        }
                    }
                };
            }

            // Log the original values
            var originalId = loadedHierarchy.Root.Id;
            var originalDataRef = loadedHierarchy.Root.DataRef;
            var originalLinkName = loadedHierarchy.Root.LinkName;

            // Save to temp file (this should trigger normalization)
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-fix-existing-{Guid.NewGuid()}.xml");
            await hierarchyService.SaveHierarchyAsync(tempPath, loadedHierarchy);

            // Read back the saved XML
            var savedXml = await File.ReadAllTextAsync(tempPath);

            // Clean up temp file
            try { File.Delete(tempPath); } catch { }

            // Parse and check the normalization
            var doc = System.Xml.Linq.XDocument.Parse(savedXml);
            var rootElement = doc.Root?.Element("structured")?.Element("item");

            var results = new List<string>();
            results.Add("<!-- FIX EXISTING HIERARCHY TEST RESULTS -->");
            results.Add("<!-- ==================================== -->");
            results.Add($"<!-- Original root ID: '{originalId}' -->");
            results.Add($"<!-- Original data-ref: '{originalDataRef}' -->");
            results.Add($"<!-- Original linkname: '{originalLinkName}' -->");
            results.Add("<!-- ------------------------------------ -->");

            var newId = rootElement?.Attribute("id")?.Value;
            var newDataRef = rootElement?.Attribute("data-ref")?.Value;
            var newLinkName = rootElement?.Element("web_page")?.Element("linkname")?.Value;

            results.Add($"<!-- Normalized root ID: '{newId}' -->");
            results.Add($"<!-- Normalized data-ref: '{newDataRef}' -->");
            results.Add($"<!-- Normalized linkname: '{newLinkName}' -->");
            results.Add("<!-- ------------------------------------ -->");

            // Validation checks
            if (originalId != "report-root" && newId == "report-root")
            {
                results.Add($"<!-- ✅ Successfully normalized ID from '{originalId}' to 'report-root' -->");
            }

            if (originalDataRef != "report-root.xml" && newDataRef == "report-root.xml")
            {
                results.Add($"<!-- ✅ Successfully normalized data-ref from '{originalDataRef}' to 'report-root.xml' -->");
            }

            if (originalLinkName != "Annual Report 2024" && newLinkName == "Annual Report 2024")
            {
                results.Add($"<!-- ✅ Successfully normalized linkname from '{originalLinkName}' to 'Annual Report 2024' -->");
            }

            results.Add("<!-- ==================================== -->");
            results.Add("");
            results.Add(savedXml);

            context.Response.ContentType = "text/xml";
            await context.Response.WriteAsync(string.Join("\n", results));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Failed to test fix existing hierarchy");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tests tiebreaker scoring for duplicate header resolution (Phases 2 & 3).
    /// Loads ar24-4 test data and applies element type + continuation marker scoring.
    ///
    /// Usage: curl http://localhost:8085/sandbox
    /// </summary>
    private static async Task HandleTestTiebreakersAsync(
        HttpContext context,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing tiebreaker scoring with ar24-4 data");

            // Load normalized XML
            var normalizedPath = "/app/data/input/optiver/projects/ar24-4/_normalized-for-development.xml";
            if (!File.Exists(normalizedPath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync($"Test file not found: {normalizedPath}");
                return;
            }

            var xml = System.Xml.Linq.XDocument.Load(normalizedPath);
            var allElements = xml.Descendants()
                .Where(e => IsHeaderOrParagraphElement(e))
                .ToList();

            // For backward header matching, we need ONLY actual headers (h1-h6)
            var actualHeaders = xml.Descendants()
                .Where(e => IsActualHeader(e))
                .ToList();

            logger.LogInformation("[Sandbox] Loaded {HeaderCount} headers, {TotalCount} headers+paragraphs", actualHeaders.Count, allElements.Count);

            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head><meta charset='UTF-8'><title>Tiebreaker Test Results</title>");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: 'Segoe UI', sans-serif; background: #1F1F1F; color: #CCCCCC; padding: 20px; }");
            html.AppendLine("h1 { color: #FFFFFF; border-bottom: 2px solid #0078D4; padding-bottom: 10px; }");
            html.AppendLine("h2 { color: #6CADDF; margin-top: 30px; }");
            html.AppendLine(".case { background: #2B2B2B; border: 1px solid #3C3C3C; border-radius: 4px; padding: 15px; margin: 20px 0; }");
            html.AppendLine(".candidate { background: #313131; border-left: 3px solid #868686; padding: 10px; margin: 10px 0; }");
            html.AppendLine(".winner { border-left-color: #2EA043; background: #1a3a1a; }");
            html.AppendLine(".loser { border-left-color: #868686; }");
            html.AppendLine(".score { font-family: 'Consolas', monospace; color: #85B6FF; }");
            html.AppendLine(".reason { color: #9D9D9D; font-size: 0.9em; font-style: italic; }");
            html.AppendLine(".context { background: #181818; border: 1px solid #2B2B2B; padding: 8px; margin: 5px 0; font-size: 0.85em; }");
            html.AppendLine("code { background: #0D0D0D; color: #E2C08D; padding: 2px 6px; border-radius: 3px; }");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            html.AppendLine("<h1>🎯 Tiebreaker Scoring Test Results</h1>");
            html.AppendLine($"<p>Test Data: <code>{normalizedPath}</code></p>");

            // Case 1: "Statement of financial position" duplicates
            html.AppendLine("<h2>Case 1: \"Statement of financial position as at 31 December 2024\"</h2>");
            html.AppendLine("<div class='case'>");

            var financialHeaders = actualHeaders
                .Where(e => e.Name.LocalName.ToLower() == "h2" &&
                           e.Value.Contains("Statement of financial position"))
                .ToList();

            if (financialHeaders.Count >= 2)
            {
                var candidates = new List<(System.Xml.Linq.XElement header, int elementScore, int continuationScore, string reason)>();

                foreach (var header in financialHeaders.Take(2))
                {
                    var headerIndex = actualHeaders.IndexOf(header);

                    // PRIORITY 1: Backward header pattern matching (pure header-to-header)
                    int backwardScore = 0;
                    string backwardReason = "";

                    if (headerIndex >= 0)
                    {
                        var previousHeaders = actualHeaders.Take(headerIndex).TakeLast(3).ToList();

                        if (previousHeaders.Count == 0)
                        {
                            backwardScore = 2;
                            backwardReason = "FirstHeader(+2)";
                        }
                        else
                        {
                            var prevHeader = previousHeaders.LastOrDefault();
                            if (prevHeader != null &&
                                prevHeader.Value.Trim() == header.Value.Trim())
                            {
                                backwardScore = -5;
                                backwardReason = "RepeatedHeader(-5)";
                            }
                        }
                    }

                    // PRIORITY 2: Non-header fallback (DISABLED - for comparison only)
                    int elementScore = GetElementTypeScore(header);
                    int continuationScore = IsFollowedByContinuationMarker(header) ? -8 : 0;

                    int totalScore = backwardScore;
                    string reason = backwardReason != "" ? backwardReason : "NoBackwardSignal";
                    string fallbackInfo = $"[Fallback would add: Element({elementScore})";
                    if (continuationScore != 0)
                        fallbackInfo += $", Continuation({continuationScore})";
                    fallbackInfo += $"]";

                    candidates.Add((header, backwardScore, 0, reason));

                    html.AppendLine($"<div class='candidate'>");
                    html.AppendLine($"<strong>Candidate {candidates.Count}</strong>");
                    html.AppendLine($"<div class='context'>Element: <code>&lt;{header.Name.LocalName}&gt;</code></div>");
                    html.AppendLine($"<div class='context'>Text: \"{header.Value.Trim().Substring(0, Math.Min(60, header.Value.Trim().Length))}...\"</div>");

                    // Show previous header
                    if (headerIndex > 0)
                    {
                        var prevHeader = actualHeaders.Take(headerIndex).LastOrDefault();
                        if (prevHeader != null)
                        {
                            var prevPreview = prevHeader.Value.Trim();
                            if (prevPreview.Length > 60)
                                prevPreview = prevPreview.Substring(0, 60) + "...";
                            html.AppendLine($"<div class='context'>Previous header: <code>&lt;{prevHeader.Name.LocalName}&gt;</code> \"{System.Web.HttpUtility.HtmlEncode(prevPreview)}\"</div>");
                        }
                    }

                    // Show following element
                    var nextElem = header.ElementsAfterSelf().FirstOrDefault();
                    if (nextElem != null)
                    {
                        var preview = nextElem.Value.Length > 80 ? nextElem.Value.Substring(0, 80) + "..." : nextElem.Value;
                        html.AppendLine($"<div class='context'>Followed by: <code>&lt;{nextElem.Name.LocalName}&gt;</code> \"{System.Web.HttpUtility.HtmlEncode(preview)}\"</div>");
                    }

                    html.AppendLine($"<div class='score'>Backward Header Score: {totalScore} {reason}</div>");
                    html.AppendLine($"<div class='reason'>{fallbackInfo}</div>");
                    html.AppendLine("</div>");
                }

                // Determine winner based on backward header score
                var winner = candidates.OrderByDescending(c => c.elementScore).First();
                var winnerIndex = candidates.IndexOf(winner);
                var scoreDiff = Math.Abs(candidates[0].elementScore - candidates[1].elementScore);

                html.AppendLine($"<p><strong>Result: Candidate {winnerIndex + 1}</strong> (Score: {winner.elementScore})</p>");
                if (scoreDiff >= 2)
                {
                    html.AppendLine($"<p class='reason'>✓ AUTO-RESOLVED by backward header matching (difference: {scoreDiff})</p>");
                }
                else
                {
                    html.AppendLine($"<p class='reason'>⚠ INCONCLUSIVE - scores too close (difference: {scoreDiff}), would require fallback</p>");
                }
                html.AppendLine($"<p class='reason'>Expected: Candidate 1 (the header after main table, not the continuation page)</p>");
            }
            else
            {
                html.AppendLine("<p>⚠ Could not find duplicate headers for this case</p>");
            }

            html.AppendLine("</div>");

            // Case 2: "Current" duplicates
            html.AppendLine("<h2>Case 2: \"Current\"</h2>");
            html.AppendLine("<div class='case'>");

            var currentHeaders = allElements
                .Where(e => e.Value.Trim() == "Current")
                .ToList();

            if (currentHeaders.Count >= 2)
            {
                var candidates = new List<(System.Xml.Linq.XElement header, int backwardScore, string reason)>();

                foreach (var header in currentHeaders.Take(2))
                {
                    var headerIndex = actualHeaders.IndexOf(header);

                    // PRIORITY 1: Backward header pattern matching (pure header-to-header)
                    int backwardScore = 0;
                    string backwardReason = "";

                    if (headerIndex >= 0)
                    {
                        var previousHeaders = actualHeaders.Take(headerIndex).TakeLast(3).ToList();

                        if (previousHeaders.Count == 0)
                        {
                            backwardScore = 2;
                            backwardReason = "FirstHeader(+2)";
                        }
                        else
                        {
                            var prevHeader = previousHeaders.LastOrDefault();
                            if (prevHeader != null &&
                                prevHeader.Value.Trim() == header.Value.Trim())
                            {
                                backwardScore = -5;
                                backwardReason = "RepeatedHeader(-5)";
                            }
                        }
                    }

                    string reason = backwardReason != "" ? backwardReason : "NoBackwardSignal";

                    // PRIORITY 2: Non-header fallback (DISABLED - for comparison only)
                    int elementScore = GetElementTypeScore(header);
                    string fallbackInfo = $"[Fallback would add: Element({elementScore})]";

                    candidates.Add((header, backwardScore, reason));

                    html.AppendLine($"<div class='candidate'>");
                    html.AppendLine($"<strong>Candidate {candidates.Count}</strong>");
                    html.AppendLine($"<div class='context'>Element: <code>&lt;{header.Name.LocalName}&gt;</code></div>");
                    html.AppendLine($"<div class='context'>Text: \"{header.Value.Trim()}\"</div>");

                    // Show previous header
                    if (headerIndex > 0)
                    {
                        var prevHeader = actualHeaders.Take(headerIndex).LastOrDefault();
                        if (prevHeader != null)
                        {
                            var prevPreview = prevHeader.Value.Trim();
                            if (prevPreview.Length > 60)
                                prevPreview = prevPreview.Substring(0, 60) + "...";
                            html.AppendLine($"<div class='context'>Previous header: <code>&lt;{prevHeader.Name.LocalName}&gt;</code> \"{System.Web.HttpUtility.HtmlEncode(prevPreview)}\"</div>");
                        }
                    }

                    html.AppendLine($"<div class='score'>Backward Header Score: {backwardScore} {reason}</div>");
                    html.AppendLine($"<div class='reason'>{fallbackInfo}</div>");
                    html.AppendLine("</div>");
                }

                // Determine winner based on backward header score
                var winner = candidates.OrderByDescending(c => c.backwardScore).First();
                var winnerIndex = candidates.IndexOf(winner);
                var scoreDiff = Math.Abs(candidates[0].backwardScore - candidates[1].backwardScore);

                html.AppendLine($"<p><strong>Result: Candidate {winnerIndex + 1}</strong> (Score: {winner.backwardScore})</p>");
                if (scoreDiff >= 2)
                {
                    html.AppendLine($"<p class='reason'>✓ AUTO-RESOLVED by backward header matching (difference: {scoreDiff})</p>");
                }
                else
                {
                    html.AppendLine($"<p class='reason'>⚠ INCONCLUSIVE - scores too close (difference: {scoreDiff}), would require fallback</p>");
                }
                html.AppendLine($"<p class='reason'>Expected: Candidate 2 (the &lt;h4&gt; header, not &lt;p&gt; paragraph text)</p>");
            }
            else
            {
                html.AppendLine("<p>⚠ Could not find duplicate 'Current' items</p>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</body></html>");

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Error during tiebreaker test");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"Error: {ex.Message}\n\n{ex.StackTrace}");
        }
    }

    private static bool IsHeaderOrParagraphElement(System.Xml.Linq.XElement element)
    {
        var name = element.Name.LocalName.ToLower();
        return (name.Length == 2 && name[0] == 'h' && char.IsDigit(name[1])) || name == "p";
    }

    private static bool IsActualHeader(System.Xml.Linq.XElement element)
    {
        var name = element.Name.LocalName.ToLower();
        return name.Length == 2 && name[0] == 'h' && char.IsDigit(name[1]);
    }

    private static int GetElementTypeScore(System.Xml.Linq.XElement element)
    {
        var name = element.Name.LocalName.ToLower();
        return name switch
        {
            "h1" => 10,
            "h2" => 9,
            "h3" => 8,
            "h4" => 7,
            "h5" => 6,
            "h6" => 5,
            "p" => -5,
            "span" => -5,
            _ => 0
        };
    }

    private static bool IsFollowedByContinuationMarker(System.Xml.Linq.XElement header)
    {
        // Check next few elements for tables with "(continued)" marker
        // Stop when we encounter another header (indicates boundary of this header's content)
        var nextElements = header.ElementsAfterSelf().Take(5);

        foreach (var elem in nextElements)
        {
            // Stop if we hit another header - that's the next section
            var elemName = elem.Name.LocalName.ToLower();
            if (elemName.Length == 2 && elemName[0] == 'h' && char.IsDigit(elemName[1]))
            {
                break; // Stop searching, we've hit the next header
            }

            // Look for div with table-wrapper class
            if (elemName == "div" &&
                elem.Attribute("class")?.Value.Contains("table-wrapper") == true)
            {
                // Check if table contains "(continued)" text
                var hasContinuation = elem.Descendants()
                    .Any(d => d.Value.Contains("(continued)"));

                if (hasContinuation)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tests local Ollama LLM with header ambiguity resolution prompt.
    /// Reads prompt from data/llm-development/_convert-prompts/resolve-header-ambiguity.md
    /// and sends it to local Ollama instance (deepseek-coder:33b).
    ///
    /// Usage: curl http://localhost:8085/sandbox?mode=test-ollama
    /// </summary>
    private static async Task HandleTestOllamaAsync(
        HttpContext context,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Testing Ollama with header ambiguity resolution prompt");

            // Read the prompt file
            var promptPath = "/app/data/llm-development/_convert-prompts/resolve-header-ambiguity.md";
            if (!File.Exists(promptPath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "text/plain";
                await context.Response.WriteAsync($"Prompt file not found: {promptPath}");
                return;
            }

            var promptText = await File.ReadAllTextAsync(promptPath);
            logger.LogInformation("[Sandbox] Loaded prompt: {Size} chars", promptText.Length);

            // Call Ollama API
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // Ollama can be slow on complex prompts
            };

            var requestBody = new
            {
                model = "deepseek-coder:33b",
                prompt = promptText,
                stream = false
            };

            var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            logger.LogInformation("[Sandbox] Calling Ollama API at http://host.docker.internal:11434/api/generate");
            var startTime = DateTime.UtcNow;

            var response = await httpClient.PostAsync("http://host.docker.internal:11434/api/generate", content);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                logger.LogError("[Sandbox] Ollama API failed: {Status} - {Error}", response.StatusCode, errorBody);

                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Ollama Test - Error</title>
    <style>
        body {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
            padding: 40px;
            max-width: 1000px;
            margin: 0 auto;
        }}
        h1 {{ color: #F85149; }}
        .error-box {{
            background: #5A1D1D;
            border: 2px solid #F85149;
            padding: 20px;
            border-radius: 8px;
            margin: 20px 0;
        }}
        pre {{
            background: #1F1F1F;
            color: #CCCCCC;
            padding: 15px;
            border-radius: 4px;
            overflow-x: auto;
            white-space: pre-wrap;
        }}
    </style>
</head>
<body>
    <h1>❌ Ollama API Error</h1>
    <div class='error-box'>
        <p><strong>Status:</strong> {response.StatusCode}</p>
        <p><strong>Error:</strong></p>
        <pre>{System.Web.HttpUtility.HtmlEncode(errorBody)}</pre>
    </div>
    <p>Make sure Ollama is running and the model is available:</p>
    <pre>ollama list
ollama pull deepseek-coder:33b</pre>
</body>
</html>");
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var responseJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);

            // Extract response text
            var ollamaResponse = responseJson.GetProperty("response").GetString() ?? "";
            var totalDuration = responseJson.TryGetProperty("total_duration", out var td)
                ? td.GetInt64() / 1_000_000_000.0  // Convert nanoseconds to seconds
                : duration;

            logger.LogInformation("[Sandbox] Ollama response received: {Size} chars in {Duration}s",
                ollamaResponse.Length, totalDuration);

            // Build HTML response with the result
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Ollama Test Results - deepseek-coder:33b</title>
    <style>
        body {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
            margin: 0;
            padding: 20px;
        }}
        h1 {{
            color: #FFFFFF;
            font-size: 24px;
            margin-bottom: 8px;
        }}
        .info-banner {{
            background: #0078D4;
            color: white;
            padding: 12px;
            margin-bottom: 20px;
            border-radius: 4px;
            font-weight: 600;
        }}
        .stats {{
            background: #181818;
            border: 1px solid #2B2B2B;
            padding: 15px;
            border-radius: 4px;
            margin: 20px 0;
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 15px;
        }}
        .stat-item {{
            text-align: center;
        }}
        .stat-value {{
            color: #0078D4;
            font-size: 24px;
            font-weight: bold;
            margin-bottom: 4px;
        }}
        .stat-label {{
            color: #9D9D9D;
            font-size: 12px;
        }}
        .panel {{
            background: #181818;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            overflow: hidden;
            margin: 20px 0;
        }}
        .panel-header {{
            background: #1F1F1F;
            color: #FFFFFF;
            padding: 12px 16px;
            border-bottom: 1px solid #2B2B2B;
            font-weight: 600;
        }}
        .panel-subheader {{
            color: #9D9D9D;
            font-size: 12px;
            font-weight: normal;
            margin-top: 4px;
        }}
        .panel-content {{
            padding: 16px;
            max-height: 600px;
            overflow-y: auto;
        }}
        pre {{
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: Consolas, Monaco, 'Courier New', monospace;
            font-size: 13px;
            margin: 0;
            padding: 16px;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            white-space: pre-wrap;
            word-wrap: break-word;
            line-height: 1.5;
        }}
        .success {{ color: #2EA043; }}
    </style>
</head>
<body>
    <div class='info-banner'>
        ✅ Successfully called Ollama API
    </div>

    <h1>Ollama Test Results</h1>

    <div class='stats'>
        <div class='stat-item'>
            <div class='stat-value'>{totalDuration:F2}s</div>
            <div class='stat-label'>Response Time</div>
        </div>
        <div class='stat-item'>
            <div class='stat-value'>{ollamaResponse.Length:N0}</div>
            <div class='stat-label'>Characters</div>
        </div>
        <div class='stat-item'>
            <div class='stat-value'>{promptText.Length:N0}</div>
            <div class='stat-label'>Prompt Chars</div>
        </div>
    </div>

    <div class='panel'>
        <div class='panel-header'>
            Prompt
            <div class='panel-subheader'>From: data/llm-development/_convert-prompts/resolve-header-ambiguity.md</div>
        </div>
        <div class='panel-content'>
            <pre>{System.Web.HttpUtility.HtmlEncode(promptText)}</pre>
        </div>
    </div>

    <div class='panel'>
        <div class='panel-header'>
            Ollama Response
            <div class='panel-subheader'>Model: deepseek-coder:33b</div>
        </div>
        <div class='panel-content'>
            <pre>{System.Web.HttpUtility.HtmlEncode(ollamaResponse)}</pre>
        </div>
    </div>

    <div style='margin-top: 30px; padding: 15px; background: #181818; border: 1px solid #2B2B2B; border-radius: 4px;'>
        <strong>Analysis Checklist:</strong>
        <ul style='margin-top: 10px;'>
            <li>Did it identify structural differences (H2 vs continuation, P vs H4)?</li>
            <li>Did it recognize PDF pagination patterns?</li>
            <li>Did it consider semantic meaning and document structure?</li>
            <li>Did it provide clear, justified recommendation?</li>
            <li>Did it avoid just picking the first occurrence without reasoning?</li>
        </ul>
    </div>
</body>
</html>";

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);

            logger.LogInformation("[Sandbox] Ollama test completed successfully");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "[Sandbox] Network error calling Ollama");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                $"Network error: {ex.Message}\n\n" +
                $"Make sure Ollama is running on your host machine:\n" +
                $"  ollama serve\n\n" +
                $"Check the model is available:\n" +
                $"  ollama list\n" +
                $"  ollama pull deepseek-coder:33b\n\n" +
                $"Docker is trying to reach: http://host.docker.internal:11434");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Error testing Ollama");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"Error: {ex.Message}\n\nStack trace:\n{ex.StackTrace}");
        }
    }
}

using PdfConversion.Services;

namespace PdfConversion.Endpoints;

/// <summary>
/// Sandbox endpoint for testing prompt generation in isolation.
///
/// This endpoint allows rapid iteration on LLM prompt logic without running the full AI generation workflow.
/// It performs XSLT transformation and builds the full prompt that would be sent to the LLM, then returns it
/// as plain text for inspection and testing.
///
/// Usage:
///   curl http://localhost:8085/sandbox              # anonymized examples (default: false)
///   curl http://localhost:8085/sandbox?anonymize=true   # anonymized examples
///   curl http://localhost:8085/sandbox?anonymize=false  # real examples
///
/// How to test with different data:
///   1. Edit the hardcoded values at the top of HandleAsync() method below
///   2. Hot-reload applies changes automatically (no restart needed)
///   3. Re-run curl command to see updated prompt
///
/// What this endpoint does:
///   1. Loads source XML from hardcoded project path
///   2. Transforms XML using XSLT (via XSLT3Service)
///   3. Loads example hierarchy.xml files from hardcoded paths
///   4. Builds the full LLM prompt using HierarchyGeneratorService
///   5. Returns the prompt as plain text (NOT sent to LLM)
///
/// Benefits:
///   - Test prompt generation logic in isolation
///   - Inspect full prompt before sending to LLM
///   - Iterate quickly on prompt engineering
///   - Verify examples are loaded correctly
///   - Check anonymization behavior
/// </summary>
public static class SandboxEndpoint
{
    /// <summary>
    /// Handles the /sandbox endpoint request - routes to different utilities based on mode parameter.
    /// </summary>
    public static async Task HandleAsync(
        HttpContext context,
        IXsltTransformationService xsltService,
        IHierarchyGeneratorService hierarchyService,
        ILogger logger)
    {
        // Check query parameters to route to different utilities
        var mode = context.Request.Query["mode"].FirstOrDefault();

        if (mode == "prompt-gen")
        {
            await HandlePromptGenerationAsync(context, xsltService, hierarchyService, logger);
        }
        else
        {
            // Default: LLM comparison
            await HandleLlmComparisonAsync(context, logger);
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
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var requestBody = new
        {
            model = "claude-sonnet-4-20250514",
            max_tokens = 4096,
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
        ILogger logger)
    {
        // Placeholder - will be implemented in next task
        context.Response.StatusCode = 501;
        await context.Response.WriteAsync("LLM comparison functionality coming soon...");
    }
}

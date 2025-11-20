using PdfConversion.Models;
using PdfConversion.Services;

namespace PdfConversion.Endpoints;

/// <summary>
/// Sandbox endpoint for testing hierarchy generation algorithms and LLM-based approaches.
///
/// DEFAULT MODE (no parameters):
///   - hierarchy-comparison: Side-by-side comparison of ground truth vs generated hierarchy
///
/// USAGE:
///   curl http://localhost:8085/sandbox                          # Default mode (hierarchy-comparison)
///   curl http://localhost:8085/sandbox?mode=inspect             # Inspect training data
///   curl http://localhost:8085/sandbox?mode=llm-critique        # Test LLM critique
///
/// CATEGORIES:
///
/// 1. Pattern-based Archives:
///    - inspect-training-data: Examine training data structure and patterns
///    - analyze-training-hierarchies: Statistical analysis of training hierarchies
///    - hierarchy-comparison: Compare generated vs ground truth hierarchies
///
/// 2. AI-based Archives:
///    - llm-critique: Test LLM critique functionality against ground truth
///    - generate-hierarchy: Generate rule-based hierarchy for test projects
///    - prompt-evolution: Test prompt engineering evolution
///    - model-comparison: Compare different LLM models
///    - full-workflow: Simulate full hierarchy generation workflow
///    - llm-accuracy-report: Generate comprehensive accuracy report
///
/// Benefits:
///   - Rapid iteration on hierarchy generation algorithms
///   - Compare pattern-based vs AI-based approaches
///   - Validate against ground truth data
///   - Test LLM integration and prompt engineering
/// </summary>
public static class SandboxEndpoint
{
    /// <summary>
    /// Handles the /sandbox endpoint request - routes to different utilities based on mode parameter.
    ///
    /// DEFAULT (no mode parameter): Latest active test (currently hierarchy-comparison)
    ///
    /// Available modes:
    ///   Pattern-based archives:
    ///     - inspect-training-data (alias: inspect): Inspect training data structure
    ///     - analyze-training-hierarchies (alias: analyze): Analyze training hierarchy patterns
    ///     - hierarchy-comparison (alias: compare): Side-by-side comparison of ground truth vs generated
    ///
    ///   AI-based archives:
    ///     - llm-critique: Test LLM critique functionality
    ///     - generate-hierarchy (alias: gen-hierarchy): Generate rule-based hierarchy
    ///     - prompt-evolution (alias: evolution): Test LLM critique accuracy against ground truth
    ///     - model-comparison (alias: models): Compare different LLM models
    ///     - full-workflow (alias: workflow): Full workflow simulation
    ///     - llm-accuracy-report (alias: accuracy, report): Generate LLM accuracy report
    /// </summary>
    public static async Task HandleAsync(
        HttpContext context,
        IXsltTransformationService xsltService,
        IHierarchyGeneratorService hierarchyGeneratorService,
        IHierarchyService hierarchyService,
        IOllamaService ollamaService,
        ILogger logger)
    {
        // Check query parameters to route to different utilities
        var mode = context.Request.Query["mode"].FirstOrDefault();

        if (mode == "llm-critique")
        {
            await HandleLlmCritiqueTestAsync(context, ollamaService, logger);
        }
        else if (mode == "generate-hierarchy" || mode == "gen-hierarchy")
        {
            await HandleGenerateHierarchyTestAsync(context, logger);
        }
        else if (mode == "prompt-evolution" || mode == "evolution")
        {
            await HandlePromptEvolutionTestAsync(context, logger);
        }
        else if (mode == "model-comparison" || mode == "models")
        {
            await HandleModelComparisonTestAsync(context, ollamaService, logger);
        }
        else if (mode == "full-workflow" || mode == "workflow")
        {
            await HandleFullWorkflowSimulationAsync(context, logger);
        }
        else if (mode == "llm-accuracy-report" || mode == "accuracy" || mode == "report")
        {
            await HandleLlmAccuracyReportAsync(context, logger);
        }
        else if (mode == "hierarchy-comparison" || mode == "compare")
        {
            await HandleHierarchyComparisonAsync(context, logger);
        }
        else if (mode == "inspect-training-data" || mode == "inspect")
        {
            await HandleInspectTrainingDataAsync(context, hierarchyService, logger);
        }
        else if (mode == "analyze-training-hierarchies" || mode == "analyze")
        {
            await HandleAnalyzeTrainingHierarchiesAsync(context, hierarchyService, logger);
        }
        else
        {
            // DEFAULT: Latest active test (currently hierarchy-comparison)
            await HandleHierarchyComparisonAsync(context, logger);
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

    /// <summary>
    /// Tests confidence scoring for Rule-Based Hierarchy Generator using pattern-based logic only.
    /// NO HARDCODED STRINGS - all scoring based on patterns, attributes, and metrics.
    /// </summary>
    private static async Task HandleConfidenceTestAsync(HttpContext context, ILogger logger)
    {
        var output = new System.Text.StringBuilder();
        output.AppendLine("=== RULE-BASED HIERARCHY CONFIDENCE SCORING TEST ===");
        output.AppendLine();
        output.AppendLine("CRITICAL RULES:");
        output.AppendLine("- ✓ ALL scoring MUST be pattern-based (data-number format, word count, depth, etc.)");
        output.AppendLine("- ✗ NEVER use hardcoded strings like \"Directors' report\"");
        output.AppendLine("- ✓ Use universal metrics applicable to ANY document structure");
        output.AppendLine();
        output.AppendLine("=======================================================");
        output.AppendLine();

        // Define test headers with various patterns
        var testHeaders = new[]
        {
            // HIGH CONFIDENCE: Has data-number, typical content length, numbered sequence
            new TestHeader
            {
                Text = "Major Section Title",
                DataNumber = "1.",
                WordCount = 1200,
                ChildHeaderCount = 5,
                Level = 1,
                HasPreviousSibling = true,
                HasNextSibling = true,
                ExpectedConfidence = "High (0.9-1.0)",
                Description = "Perfect: numbered, typical length, has structure"
            },

            // MEDIUM CONFIDENCE: No data-number, medium content
            new TestHeader
            {
                Text = "Subsection Without Numbering",
                DataNumber = null,
                WordCount = 450,
                ChildHeaderCount = 2,
                Level = 2,
                HasPreviousSibling = true,
                HasNextSibling = false,
                ExpectedConfidence = "Medium (0.6-0.9)",
                Description = "Missing data-number but reasonable structure"
            },

            // LOW CONFIDENCE: No data-number, very short content, isolated
            new TestHeader
            {
                Text = "Short Header",
                DataNumber = null,
                WordCount = 50,
                ChildHeaderCount = 0,
                Level = 3,
                HasPreviousSibling = false,
                HasNextSibling = false,
                ExpectedConfidence = "Low (0.0-0.6)",
                Description = "Multiple red flags: short, isolated, no structure"
            },

            // HIGH CONFIDENCE: Standard note numbering
            new TestHeader
            {
                Text = "Note to Financial Statements",
                DataNumber = "1.",
                WordCount = 800,
                ChildHeaderCount = 3,
                Level = 2,
                HasPreviousSibling = true,
                HasNextSibling = true,
                ExpectedConfidence = "High (0.9-1.0)",
                Description = "Standard note format with good structure"
            },

            // MEDIUM CONFIDENCE: Roman numeral numbering (less common)
            new TestHeader
            {
                Text = "Subsection with Roman Numeral",
                DataNumber = "(iii)",
                WordCount = 300,
                ChildHeaderCount = 0,
                Level = 4,
                HasPreviousSibling = true,
                HasNextSibling = true,
                ExpectedConfidence = "Medium (0.6-0.9)",
                Description = "Unusual numbering format lowers confidence"
            },

            // LOW CONFIDENCE: Deep nesting, very long content
            new TestHeader
            {
                Text = "Very Nested Section",
                DataNumber = null,
                WordCount = 6000,
                ChildHeaderCount = 1,
                Level = 5,
                HasPreviousSibling = false,
                HasNextSibling = false,
                ExpectedConfidence = "Low (0.0-0.6)",
                Description = "Too deep, too long, likely needs splitting"
            },

            // HIGH CONFIDENCE: Dotted notation (common in notes)
            new TestHeader
            {
                Text = "Sub-note Section",
                DataNumber = "1.1",
                WordCount = 600,
                ChildHeaderCount = 2,
                Level = 3,
                HasPreviousSibling = true,
                HasNextSibling = true,
                ExpectedConfidence = "High (0.9-1.0)",
                Description = "Standard sub-note format"
            },

            // MEDIUM CONFIDENCE: Letter prefix
            new TestHeader
            {
                Text = "Alphabetic Section",
                DataNumber = "(a)",
                WordCount = 400,
                ChildHeaderCount = 1,
                Level = 3,
                HasPreviousSibling = true,
                HasNextSibling = true,
                ExpectedConfidence = "Medium-High (0.7-0.9)",
                Description = "Letter numbering is valid but less structured"
            }
        };

        foreach (var header in testHeaders)
        {
            var (confidence, flags, reasoning) = CalculateConfidence(header);

            output.AppendLine($"TEST: {header.Text}");
            output.AppendLine($"  Description: {header.Description}");
            output.AppendLine($"  Data-Number: {header.DataNumber ?? "(none)"}");
            output.AppendLine($"  Word Count: {header.WordCount}");
            output.AppendLine($"  Level: {header.Level}");
            output.AppendLine($"  Children: {header.ChildHeaderCount}");
            output.AppendLine($"  CONFIDENCE SCORE: {confidence:F2} ({GetConfidenceCategory(confidence)})");
            output.AppendLine($"  Expected: {header.ExpectedConfidence}");
            output.AppendLine($"  Flags: {(flags.Any() ? string.Join(", ", flags) : "None")}");
            output.AppendLine($"  Reasoning: {reasoning}");

            // Check if result matches expectation
            var category = GetConfidenceCategory(confidence);
            var isMatch = header.ExpectedConfidence.Contains(category);
            output.AppendLine($"  Result: {(isMatch ? "✓ PASS" : "✗ FAIL - review scoring logic")}");
            output.AppendLine();
        }

        output.AppendLine("=======================================================");
        output.AppendLine();
        output.AppendLine("CONFIDENCE CATEGORIES:");
        output.AppendLine("  High (0.9-1.0):   Strong structural indicators, standard patterns");
        output.AppendLine("  Medium (0.6-0.9): Some uncertainty, but reasonable structure");
        output.AppendLine("  Low (0.0-0.6):    Multiple issues, needs manual review");
        output.AppendLine();
        output.AppendLine("NEXT STEPS:");
        output.AppendLine("1. Review test results - all should PASS");
        output.AppendLine("2. Adjust scoring weights if needed in CalculateConfidence()");
        output.AppendLine("3. Once satisfied, move to Phase 2 (integrate into service)");

        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync(output.ToString());

        logger.LogInformation("[Sandbox] Confidence test completed - check output for results");
    }

    private class TestHeader
    {
        public string Text { get; set; } = string.Empty;
        public string? DataNumber { get; set; }
        public int WordCount { get; set; }
        public int ChildHeaderCount { get; set; }
        public int Level { get; set; }
        public bool HasPreviousSibling { get; set; }
        public bool HasNextSibling { get; set; }
        public string ExpectedConfidence { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    private enum UncertaintyFlag
    {
        NoDataNumber,          // Missing data-number attribute
        UnusualNumbering,      // data-number doesn't match standard patterns
        LongContent,           // >5000 words
        ShortContent,          // <100 words
        DeepNesting,           // Level > 4
        UnclearParent,         // Could belong to multiple parents
        IsolatedHeader,        // No siblings at same level
        MissingSequence        // Gap in numbered sequence
    }

    private static (double confidence, List<UncertaintyFlag> flags, string reasoning)
        CalculateConfidence(TestHeader header)
    {
        var score = 0.5; // Base score
        var flags = new List<UncertaintyFlag>();
        var reasons = new List<string>();

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

                // Penalize roman numerals and letters (less common than numeric)
                if (System.Text.RegularExpressions.Regex.IsMatch(header.DataNumber, @"^\([ivxlcdm]+\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    flags.Add(UncertaintyFlag.UnusualNumbering);
                    score -= 0.15;
                    reasons.Add("Roman numeral format (less common)");
                }
                else if (System.Text.RegularExpressions.Regex.IsMatch(header.DataNumber, @"^\([a-z]\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
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
        if (header.WordCount >= 500 && header.WordCount <= 3000)
        {
            score += 0.2;
            reasons.Add("Typical content length");
        }
        else if (header.WordCount >= 200 && header.WordCount < 500)
        {
            score += 0.15;
            reasons.Add("Moderate content length");
        }
        else if (header.WordCount > 5000)
        {
            flags.Add(UncertaintyFlag.LongContent);
            score -= 0.15;
            reasons.Add($"Long content ({header.WordCount} words)");
        }
        else if (header.WordCount < 100)
        {
            flags.Add(UncertaintyFlag.ShortContent);
            score -= 0.2;
            reasons.Add($"Short content ({header.WordCount} words)");
        }

        // PATTERN 3: Nesting depth
        if (header.Level > 4)
        {
            flags.Add(UncertaintyFlag.DeepNesting);
            score -= 0.2;
            reasons.Add($"Deep nesting (level {header.Level})");
        }
        else if (header.Level <= 2)
        {
            score += 0.1;
            reasons.Add("Shallow hierarchy");
        }
        else if (header.Level == 4)
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
        else if (header.Level <= 2 && header.ChildHeaderCount == 0)
        {
            score -= 0.05;
            reasons.Add("Top-level with no children (unusual)");
        }

        // PATTERN 5: Isolation (no siblings)
        if (!header.HasPreviousSibling && !header.HasNextSibling)
        {
            flags.Add(UncertaintyFlag.IsolatedHeader);
            score -= 0.15;
            reasons.Add("Isolated (no siblings)");
        }

        // Clamp to valid range
        var finalScore = Math.Clamp(score, 0.0, 1.0);
        var reasoning = string.Join("; ", reasons);

        return (finalScore, flags, reasoning);
    }

    private static bool IsStandardNumberingFormat(string dataNumber)
    {
        // Standard patterns: "1", "1.", "1.1", "1.1.1", "(a)", "(i)", "Note 1"
        return System.Text.RegularExpressions.Regex.IsMatch(
            dataNumber,
            @"^(\d+\.?|\d+\.\d+(\.\d+)*|\([a-z]\)|\([ivxlcdm]+\)|Note \d+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
    }

    private static string GetConfidenceCategory(double confidence)
    {
        if (confidence >= 0.9) return "High";
        if (confidence >= 0.6) return "Medium";
        return "Low";
    }

    /// <summary>
    /// Test LLM critique system for uncertain hierarchy decisions.
    /// Tests constrained multiple-choice critique (not generation).
    ///
    /// Usage: curl http://localhost:8085/sandbox?mode=llm-critique
    /// </summary>
    private static async Task HandleLlmCritiqueTestAsync(
        HttpContext context,
        IOllamaService ollamaService,
        ILogger logger)
    {
        // ========================================
        // HARDCODED TEST CASES - uncertain decisions that need LLM critique
        // ========================================
        var testDecisions = new List<UncertainDecision>
        {
            // Case 1: Missing data-number, medium content
            new UncertainDecision
            {
                HeaderText = "Risk Management Framework",
                CurrentLevel = 2,
                CurrentParent = "Directors' report",
                DataNumber = null,
                WordCount = 650,
                ChildHeaderCount = 3,
                PreviousHeader = "Corporate Governance",
                PreviousSiblingLevel = 2,
                NextHeader = "Financial Statements",
                NextSiblingLevel = 1,
                UncertaintyReason = "Missing data-number attribute; Could be Level 1 or Level 2"
            },

            // Case 2: Very short content, unclear parent
            new UncertainDecision
            {
                HeaderText = "Summary",
                CurrentLevel = 3,
                CurrentParent = "Financial risk management",
                DataNumber = null,
                WordCount = 85,
                ChildHeaderCount = 0,
                PreviousHeader = "Interest rate risk",
                PreviousSiblingLevel = 3,
                NextHeader = "Contingent liabilities",
                NextSiblingLevel = 2,
                UncertaintyReason = "Short content (<100 words); Could be subsection or should be merged"
            },

            // Case 3: Unusual numbering, ambiguous level
            new UncertainDecision
            {
                HeaderText = "Deferred tax assets and liabilities",
                CurrentLevel = 3,
                CurrentParent = "Income tax in the statement of financial position",
                DataNumber = "(ii)",
                WordCount = 420,
                ChildHeaderCount = 0,
                PreviousHeader = "Current taxation represents",
                PreviousSiblingLevel = 3,
                NextHeader = "Capital, reserves and dividends",
                NextSiblingLevel = 2,
                UncertaintyReason = "Roman numeral numbering (unusual); Could be promoted to Level 2"
            },

            // Case 4: No children but long content
            new UncertainDecision
            {
                HeaderText = "Going concern",
                CurrentLevel = 2,
                CurrentParent = "Directors' report",
                DataNumber = null,
                WordCount = 1250,
                ChildHeaderCount = 0,
                PreviousHeader = "Principal risks and uncertainties",
                PreviousSiblingLevel = 2,
                NextHeader = "Sustainability",
                NextSiblingLevel = 2,
                UncertaintyReason = "No child headers but substantial content (1250 words); Could be broken into subsections"
            },

            // Case 5: Potential merge candidate
            new UncertainDecision
            {
                HeaderText = "Conclusion",
                CurrentLevel = 3,
                CurrentParent = "Risk assessment",
                DataNumber = null,
                WordCount = 120,
                ChildHeaderCount = 0,
                PreviousHeader = "Mitigation strategies",
                PreviousSiblingLevel = 3,
                NextHeader = "Audit committee report",
                NextSiblingLevel = 1,
                UncertaintyReason = "Short concluding section; Could be merged with previous section"
            }
        };

        var results = new System.Text.StringBuilder();
        results.AppendLine("=== LLM CRITIQUE PROMPT TESTING ===\n");
        results.AppendLine("Testing constrained multiple-choice critique (not generation)");
        results.AppendLine("Key Innovation: Ask LLM to CRITIQUE uncertain decisions, not generate hierarchy\n");
        results.AppendLine("=================================================\n\n");

        // Check Ollama health first
        var isHealthy = await ollamaService.CheckHealthAsync();
        if (!isHealthy)
        {
            results.AppendLine("❌ OLLAMA SERVICE NOT AVAILABLE");
            results.AppendLine("Please ensure Ollama is running on host system:");
            results.AppendLine("  ollama serve");
            await context.Response.WriteAsync(results.ToString());
            return;
        }

        // Get available models
        var models = await ollamaService.GetAvailableModelsAsync();
        if (models.Count == 0)
        {
            results.AppendLine("❌ NO OLLAMA MODELS AVAILABLE");
            results.AppendLine("Please pull a model first:");
            results.AppendLine("  ollama pull deepseek-coder:6.7b");
            results.AppendLine("  ollama pull llama2");
            await context.Response.WriteAsync(results.ToString());
            return;
        }

        // Use first available model (or prefer deepseek-coder if available)
        var preferredModel = models.FirstOrDefault(m => m.Name.Contains("deepseek-coder"))?.Name
                           ?? models.FirstOrDefault(m => m.Name.Contains("llama"))?.Name
                           ?? models.First().Name;

        results.AppendLine($"✓ Ollama service connected");
        results.AppendLine($"✓ Using model: {preferredModel}");
        results.AppendLine($"✓ Available models: {string.Join(", ", models.Select(m => m.Name))}");
        results.AppendLine($"\n");

        int testNumber = 1;
        foreach (var decision in testDecisions)
        {
            results.AppendLine($"TEST {testNumber}: {decision.HeaderText}");
            results.AppendLine($"  Current: Level {decision.CurrentLevel} under \"{decision.CurrentParent}\"");
            results.AppendLine($"  Content: {decision.WordCount} words, {decision.ChildHeaderCount} child headers");
            results.AppendLine($"  Issue: {decision.UncertaintyReason}");
            results.AppendLine();

            // Build prompt
            var prompt = BuildCritiquePrompt(decision);

            // Log prompt (first time only, to see what we're sending)
            if (testNumber == 1)
            {
                results.AppendLine("--- PROMPT SENT TO LLM (first test only) ---");
                results.AppendLine(prompt);
                results.AppendLine("--- END PROMPT ---\n");
            }

            // Call Ollama
            try
            {
                var response = await ollamaService.GenerateAsync(
                    model: preferredModel,
                    prompt: prompt,
                    temperature: 0.1 // Low temperature for consistency
                );

                results.AppendLine($"  Raw LLM Response:");
                results.AppendLine($"  {response.Trim()}\n");

                // Try to parse JSON
                var critique = ParseCritiqueResponse(response);
                if (critique != null)
                {
                    results.AppendLine($"  ✓ Valid JSON parsed");
                    results.AppendLine($"  Recommendation: {critique.Recommendation}");
                    results.AppendLine($"  Reasoning: {critique.Reasoning}");
                    results.AppendLine($"  Confidence: {critique.Confidence}%");
                }
                else
                {
                    results.AppendLine($"  ✗ Failed to parse JSON response");
                    results.AppendLine($"  This may indicate the LLM didn't follow instructions.");
                }
            }
            catch (Exception ex)
            {
                results.AppendLine($"  ✗ Error: {ex.Message}");
                logger.LogError(ex, "LLM critique test failed for case {TestNumber}", testNumber);
            }

            results.AppendLine("\n" + new string('=', 80) + "\n");
            testNumber++;
        }

        results.AppendLine("\n=== SUMMARY ===\n");
        results.AppendLine("Key Observations:");
        results.AppendLine("1. Does LLM return valid JSON consistently?");
        results.AppendLine("2. Does LLM stick to A/B/C/D options (no hallucinated options)?");
        results.AppendLine("3. Is reasoning specific to the context (not generic)?");
        results.AppendLine("4. Are recommendations sensible given the document structure?");
        results.AppendLine("\nNext Steps:");
        results.AppendLine("- If JSON parsing fails: Strengthen prompt instructions");
        results.AppendLine("- If hallucinating options: Add more explicit constraints");
        results.AppendLine("- If reasoning too generic: Add more contextual signals");
        results.AppendLine("- If recommendations poor: Test different models");

        await context.Response.WriteAsync(results.ToString());
    }

    /// <summary>
    /// Compare LLM critique quality across different models.
    /// Tests same decisions with multiple models and provides recommendations.
    /// </summary>
    private static async Task HandleModelComparisonTestAsync(
        HttpContext context,
        IOllamaService ollamaService,
        ILogger logger)
    {
        var results = new System.Text.StringBuilder();
        results.AppendLine("=== LLM MODEL COMPARISON TEST ===\n");
        results.AppendLine("Testing critique quality across different models");
        results.AppendLine("=================================================\n\n");

        // Check Ollama health first
        var isHealthy = await ollamaService.CheckHealthAsync();
        if (!isHealthy)
        {
            results.AppendLine("❌ OLLAMA SERVICE NOT AVAILABLE");
            results.AppendLine("Please ensure Ollama is running on host system:");
            results.AppendLine("  ollama serve");
            await context.Response.WriteAsync(results.ToString());
            return;
        }

        // Get available models
        var availableModels = await ollamaService.GetAvailableModelsAsync();
        if (availableModels.Count == 0)
        {
            results.AppendLine("❌ NO OLLAMA MODELS AVAILABLE");
            results.AppendLine("Please pull models first:");
            results.AppendLine("  ollama pull deepseek-coder:33b");
            results.AppendLine("  ollama pull mistral:latest");
            results.AppendLine("  ollama pull qwen2.5-coder:1.5b-base");
            await context.Response.WriteAsync(results.ToString());
            return;
        }

        results.AppendLine($"✓ Ollama service connected");
        results.AppendLine($"✓ Available models: {string.Join(", ", availableModels.Select(m => m.Name))}");
        results.AppendLine();

        // Test decisions (use 3 from existing test cases)
        var testDecisions = new List<UncertainDecision>
        {
            new UncertainDecision
            {
                HeaderText = "Risk Management Framework",
                CurrentLevel = 2,
                CurrentParent = "Directors' report",
                DataNumber = null,
                WordCount = 650,
                ChildHeaderCount = 3,
                PreviousHeader = "Corporate Governance",
                PreviousSiblingLevel = 2,
                NextHeader = "Financial Statements",
                NextSiblingLevel = 1,
                UncertaintyReason = "Missing data-number; Could be Level 1 or Level 2"
            },
            new UncertainDecision
            {
                HeaderText = "Summary",
                CurrentLevel = 3,
                CurrentParent = "Financial risk management",
                DataNumber = null,
                WordCount = 85,
                ChildHeaderCount = 0,
                PreviousHeader = "Interest rate risk",
                PreviousSiblingLevel = 3,
                NextHeader = "Contingent liabilities",
                NextSiblingLevel = 2,
                UncertaintyReason = "Short content (<100 words); Could be merged"
            },
            new UncertainDecision
            {
                HeaderText = "Deferred tax assets and liabilities",
                CurrentLevel = 3,
                CurrentParent = "Income tax in financial position",
                DataNumber = "(ii)",
                WordCount = 420,
                ChildHeaderCount = 0,
                PreviousHeader = "Current taxation",
                PreviousSiblingLevel = 3,
                NextHeader = "Capital and reserves",
                NextSiblingLevel = 2,
                UncertaintyReason = "Roman numeral numbering; Could be Level 2"
            }
        };

        // Models to test (filter to only available ones)
        var targetModels = new[] { "deepseek-coder:33b", "mistral:latest", "qwen2.5-coder:1.5b-base" };
        var modelsToTest = targetModels
            .Where(m => availableModels.Any(am => am.Name.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (modelsToTest.Count == 0)
        {
            results.AppendLine("❌ NONE OF THE TARGET MODELS ARE AVAILABLE");
            results.AppendLine("\nTarget models:");
            foreach (var model in targetModels)
            {
                results.AppendLine($"  - {model}");
            }
            results.AppendLine("\nPlease pull at least one:");
            results.AppendLine("  ollama pull deepseek-coder:33b");
            results.AppendLine("  ollama pull mistral:latest");
            results.AppendLine("  ollama pull qwen2.5-coder:1.5b-base");
            await context.Response.WriteAsync(results.ToString());
            return;
        }

        results.AppendLine($"Testing {modelsToTest.Count} model(s): {string.Join(", ", modelsToTest)}\n");
        results.AppendLine();

        // Results tracking
        var modelResults = new Dictionary<string, ModelTestResult>();

        foreach (var model in modelsToTest)
        {
            results.AppendLine($"═══ TESTING MODEL: {model} ═══\n");

            var modelResult = new ModelTestResult { ModelName = model };

            int testNum = 1;
            foreach (var decision in testDecisions)
            {
                results.AppendLine($"TEST {testNum}: {decision.HeaderText}");

                try
                {
                    var prompt = BuildCritiquePrompt(decision);
                    var startTime = DateTime.UtcNow;

                    var response = await ollamaService.GenerateAsync(
                        model: model,
                        prompt: prompt,
                        temperature: 0.1
                    );

                    var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                    modelResult.TotalResponseTime += elapsed;

                    results.AppendLine($"  Response Time: {elapsed:F1}s");

                    // Try to parse
                    var critique = ParseCritiqueResponse(response);

                    if (critique != null)
                    {
                        modelResult.SuccessfulParses++;
                        results.AppendLine($"  ✓ Valid JSON");
                        results.AppendLine($"  Recommendation: {critique.Recommendation}");
                        results.AppendLine($"  Confidence: {critique.Confidence}%");

                        // Check reasoning quality
                        bool citesMetrics = ContainsMetricCitation(critique.Reasoning);
                        if (citesMetrics)
                        {
                            modelResult.ReasoningWithMetrics++;
                            results.AppendLine($"  ✓ Cites metrics");
                        }
                        else
                        {
                            results.AppendLine($"  ✗ Generic reasoning");
                        }

                        results.AppendLine($"  Reasoning: {critique.Reasoning}");
                    }
                    else
                    {
                        modelResult.FailedParses++;
                        results.AppendLine($"  ✗ Failed to parse JSON");
                        results.AppendLine($"  Raw: {response.Substring(0, Math.Min(200, response.Length))}...");
                    }
                }
                catch (Exception ex)
                {
                    modelResult.Errors++;
                    results.AppendLine($"  ✗ Error: {ex.Message}");
                    logger.LogError(ex, "Model comparison test failed for {Model} on case {TestNumber}", model, testNum);
                }

                results.AppendLine();
                testNum++;
            }

            modelResult.TestsRun = testDecisions.Count;
            modelResults[model] = modelResult;

            results.AppendLine();
        }

        // Summary
        results.AppendLine("\n═══════════════════════════════════════════════════");
        results.AppendLine("COMPARISON SUMMARY");
        results.AppendLine("═══════════════════════════════════════════════════\n");

        results.AppendLine($"{"Model",-30} {"JSON%",-10} {"Metrics%",-10} {"Avg Time",-10}");
        results.AppendLine(new string('-', 60));

        foreach (var kvp in modelResults)
        {
            var result = kvp.Value;
            var jsonPercent = (result.SuccessfulParses * 100.0 / result.TestsRun);
            var metricsPercent = result.SuccessfulParses > 0
                ? (result.ReasoningWithMetrics * 100.0 / result.SuccessfulParses)
                : 0;
            var avgTime = result.TotalResponseTime / result.TestsRun;

            results.AppendLine($"{kvp.Key,-30} {jsonPercent,-9:F1}% {metricsPercent,-9:F1}% {avgTime,-9:F1}s");
        }

        results.AppendLine();
        results.AppendLine("=== RECOMMENDATION ===");

        // Recommend best model
        var bestModel = modelResults
            .OrderByDescending(kvp => kvp.Value.SuccessfulParses)
            .ThenByDescending(kvp => kvp.Value.ReasoningWithMetrics)
            .ThenBy(kvp => kvp.Value.TotalResponseTime / kvp.Value.TestsRun)
            .First();

        results.AppendLine($"Best Model: {bestModel.Key}");
        results.AppendLine($"  - JSON Compliance: {(bestModel.Value.SuccessfulParses * 100.0 / bestModel.Value.TestsRun):F1}%");
        if (bestModel.Value.SuccessfulParses > 0)
        {
            results.AppendLine($"  - Reasoning Quality: {(bestModel.Value.ReasoningWithMetrics * 100.0 / bestModel.Value.SuccessfulParses):F1}% cite metrics");
        }
        results.AppendLine($"  - Avg Response Time: {(bestModel.Value.TotalResponseTime / bestModel.Value.TestsRun):F1}s");

        await context.Response.WriteAsync(results.ToString());
    }

    /// <summary>
    /// Represents an uncertain hierarchy decision that needs LLM critique
    /// </summary>
    private class UncertainDecision
    {
        public string HeaderText { get; set; } = string.Empty;
        public int CurrentLevel { get; set; }
        public string CurrentParent { get; set; } = string.Empty;
        public string? DataNumber { get; set; }
        public int WordCount { get; set; }
        public int ChildHeaderCount { get; set; }
        public string? PreviousHeader { get; set; }
        public int PreviousSiblingLevel { get; set; }
        public string? NextHeader { get; set; }
        public int NextSiblingLevel { get; set; }
        public string UncertaintyReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parsed LLM critique response
    /// </summary>
    private class CritiqueResponse
    {
        public string Recommendation { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public int Confidence { get; set; }
    }

    /// <summary>
    /// Model comparison test results
    /// </summary>
    private class ModelTestResult
    {
        public string ModelName { get; set; } = string.Empty;
        public int TestsRun { get; set; }
        public int SuccessfulParses { get; set; }
        public int FailedParses { get; set; }
        public int ReasoningWithMetrics { get; set; }
        public int Errors { get; set; }
        public double TotalResponseTime { get; set; }
    }

    /// <summary>
    /// Build a critique prompt for an uncertain hierarchy decision.
    /// This is the CRITICAL part - the prompt must be generic, contextual, and constrained.
    /// </summary>
    private static string BuildCritiquePrompt(UncertainDecision decision)
    {
        // Generate options based on context
        var options = GenerateOptions(decision);

        return $@"You are reviewing a proposed document hierarchy decision. This is NOT about generating new structure - only critiquing an existing proposal.

PROPOSED DECISION:
- Header: ""{decision.HeaderText}""
- Proposed Level: {decision.CurrentLevel}
- Proposed Parent: ""{decision.CurrentParent}""
- Content: {decision.WordCount} words, {decision.ChildHeaderCount} sub-headers
- Uncertainty: {decision.UncertaintyReason}

CONTEXT (Adjacent Sections):
- Previous Section: ""{decision.PreviousHeader ?? "N/A"}"" (Level {decision.PreviousSiblingLevel})
- Next Section: ""{decision.NextHeader ?? "N/A"}"" (Level {decision.NextSiblingLevel})

DATA ATTRIBUTES:
- Current data-number: {(string.IsNullOrEmpty(decision.DataNumber) ? "\"(none)\"" : $"\"{decision.DataNumber}\"")}

METRICS ANALYSIS:
- Content Length: {WordCountCategory(decision.WordCount)} ({decision.WordCount} words)
- Structure: {StructureCategory(decision.ChildHeaderCount)} ({decision.ChildHeaderCount} children)
- Context: Positioned between Level {decision.PreviousSiblingLevel} and Level {decision.NextSiblingLevel} sections

QUESTION: What should we do with ""{decision.HeaderText}""?

OPTIONS:
{options}

INSTRUCTIONS:
1. Choose ONE option (A, B, C, or D)
2. Explain your reasoning in 2-3 sentences
3. Your reasoning MUST cite specific metrics:
   - Reference the word count category if relevant (e.g., ""With 650 words (Typical length)..."")
   - Reference the structure (children count) if relevant (e.g., ""Having 3 children (Hierarchical)..."")
   - Reference the positioning context if relevant (e.g., ""Positioned between Level 2 and Level 1..."")
   - Explain HOW these metrics support your recommendation
4. Rate your confidence (0-100):
   - 50-70: Multiple valid options, unclear best choice
   - 70-85: Good choice but some uncertainty remains
   - 85-95: Strong choice with clear justification
   - 95-100: Only valid option, all others clearly wrong
5. Consider: numbering patterns, content length, document flow, typical annual report structure

You MUST respond with ONLY valid JSON in this exact format:
{{
  ""recommendation"": ""A"",
  ""reasoning"": ""Your explanation here citing SPECIFIC metrics (2-3 sentences)"",
  ""confidence"": 85
}}

Do not include any text before or after the JSON. Only the JSON object.";
    }

    /// <summary>
    /// Generate multiple-choice options based on the decision context
    /// </summary>
    private static string GenerateOptions(UncertainDecision decision)
    {
        var options = new System.Text.StringBuilder();

        // Option A: Keep as-is
        options.AppendLine($"A) Keep current position (Level {decision.CurrentLevel} under \"{decision.CurrentParent}\")");

        // Option B: Promote to higher level
        if (decision.CurrentLevel > 1)
        {
            options.AppendLine($"B) Promote to Level {decision.CurrentLevel - 1} (make more prominent, higher in hierarchy)");
        }
        else
        {
            options.AppendLine("B) Already at top level - cannot promote");
        }

        // Option C: Demote to lower level
        if (decision.CurrentLevel < 4)
        {
            var targetParent = decision.PreviousHeader ?? "previous section";
            options.AppendLine($"C) Demote to Level {decision.CurrentLevel + 1} (nest under \"{targetParent}\")");
        }
        else
        {
            options.AppendLine("C) Already deeply nested - demoting not recommended");
        }

        // Option D: Merge with adjacent section
        if (!string.IsNullOrEmpty(decision.PreviousHeader))
        {
            options.AppendLine($"D) Merge with \"{decision.PreviousHeader}\" (combine into single section)");
        }
        else if (!string.IsNullOrEmpty(decision.NextHeader))
        {
            options.AppendLine($"D) Merge with \"{decision.NextHeader}\" (combine into single section)");
        }
        else
        {
            options.AppendLine("D) No suitable adjacent section for merging");
        }

        return options.ToString();
    }

    /// <summary>
    /// Categorize word count for clearer metric context
    /// </summary>
    private static string WordCountCategory(int wordCount)
    {
        return wordCount switch
        {
            < 100 => "Very short",
            < 300 => "Short",
            < 800 => "Typical",
            < 2000 => "Long",
            _ => "Very long"
        };
    }

    /// <summary>
    /// Categorize structure complexity based on child header count
    /// </summary>
    private static string StructureCategory(int childCount)
    {
        return childCount switch
        {
            0 => "Flat (no children)",
            <= 2 => "Shallow (1-2 children)",
            _ => "Hierarchical (3+ children)"
        };
    }

    /// <summary>
    /// Generate and save rule-based hierarchy with confidence scores for optiver/ar24-4.
    /// This prepares the hierarchy.xml file needed for prompt evolution testing.
    /// </summary>
    private static async Task HandleGenerateHierarchyTestAsync(HttpContext context, ILogger logger)
    {
        var hierarchyGenerator = context.RequestServices.GetRequiredService<IHierarchyGeneratorService>();
        var hierarchyService = context.RequestServices.GetRequiredService<IHierarchyService>();

        var results = new System.Text.StringBuilder();
        results.AppendLine("=== GENERATE RULE-BASED HIERARCHY TEST ===\n");
        results.AppendLine("Project: optiver/ar24-4");
        results.AppendLine("===========================================\n");

        try
        {
            // 1. Load normalized XML
            var normalizedPath = "/app/data/output/optiver/projects/ar24-4/normalized.xml";
            if (!File.Exists(normalizedPath))
            {
                results.AppendLine($"✗ Error: Normalized XML not found at {normalizedPath}");
                await context.Response.WriteAsync(results.ToString());
                return;
            }

            results.AppendLine($"✓ Loading normalized XML...");
            var normalizedXml = await File.ReadAllTextAsync(normalizedPath);

            // 2. Generate rule-based hierarchy (extracts headers internally)
            results.AppendLine($"✓ Generating rule-based hierarchy with confidence scores...");

            // Call GenerateHierarchyAsync with empty examples list (rule-based mode doesn't use them)
            var proposal = await hierarchyGenerator.GenerateHierarchyAsync(
                normalizedXml,
                new List<string>(), // empty examples
                "unused-model-for-rule-based",
                CancellationToken.None);

            var hierarchy = proposal.Root;

            // 3. Count items and analyze confidence
            var allItems = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(hierarchy, allItems);

            var itemsWithoutRoot = allItems.Where(i => i.Level > 0).ToList();

            results.AppendLine($"  Generated {itemsWithoutRoot.Count} items");
            results.AppendLine();

            if (itemsWithoutRoot.Count > 0)
            {
                var highConf = itemsWithoutRoot.Count(i => i.ConfidenceScore >= 0.9);
                var medConf = itemsWithoutRoot.Count(i => i.ConfidenceScore >= 0.6 && i.ConfidenceScore < 0.9);
                var lowConf = itemsWithoutRoot.Count(i => i.ConfidenceScore < 0.6);

                results.AppendLine("--- CONFIDENCE DISTRIBUTION ---");
                results.AppendLine($"High (≥0.9):     {highConf,3} items ({(highConf * 100.0 / itemsWithoutRoot.Count):F1}%)");
                results.AppendLine($"Medium (0.6-0.9): {medConf,3} items ({(medConf * 100.0 / itemsWithoutRoot.Count):F1}%)");
                results.AppendLine($"Low (<0.6):      {lowConf,3} items ({(lowConf * 100.0 / itemsWithoutRoot.Count):F1}%)");
                results.AppendLine();

                // 4. Show uncertain items
                var uncertainItems = itemsWithoutRoot
                    .Where(i => i.ConfidenceScore < 0.9)
                    .OrderBy(i => i.ConfidenceScore)
                    .Take(10)
                    .ToList();

                if (uncertainItems.Any())
                {
                    results.AppendLine($"--- TOP {Math.Min(10, uncertainItems.Count)} UNCERTAIN DECISIONS ---");
                    foreach (var item in uncertainItems)
                    {
                        results.AppendLine($"  • {item.LinkName}");
                        results.AppendLine($"    Level: {item.Level}, Confidence: {item.ConfidenceScore:F2}");
                        if (!string.IsNullOrEmpty(item.Reasoning))
                        {
                            results.AppendLine($"    Reason: {item.Reasoning}");
                        }
                        results.AppendLine();
                    }
                }
            }

            // 5. Save hierarchy to file
            var outputPath = "/app/data/output/optiver/projects/ar24-4/hierarchy.xml";
            var hierarchyStructure = new HierarchyStructure { Root = hierarchy };
            await hierarchyService.SaveHierarchyAsync(outputPath, hierarchyStructure);

            results.AppendLine($"✓ Saved hierarchy to: {outputPath}");
            results.AppendLine();
            results.AppendLine("=== SUCCESS ===");
            results.AppendLine("Now run: curl http://localhost:8085/sandbox?mode=prompt-evolution");
        }
        catch (Exception ex)
        {
            results.AppendLine($"\n✗ Error: {ex.Message}");
            results.AppendLine($"Stack: {ex.StackTrace}");
        }

        await context.Response.WriteAsync(results.ToString());
    }

    /// <summary>
    /// Test LLM critique accuracy against ground truth hierarchy.
    /// This is the Prompt Evolution Engine - automated testing against real uncertain decisions.
    /// </summary>
    private static async Task HandlePromptEvolutionTestAsync(HttpContext context, ILogger logger)
    {
        var hierarchyService = context.RequestServices.GetRequiredService<IHierarchyService>();
        var comparisonService = context.RequestServices.GetRequiredService<HierarchyComparisonService>();
        var ollamaService = context.RequestServices.GetRequiredService<IOllamaService>();

        var results = new System.Text.StringBuilder();
        results.AppendLine("=== PROMPT EVOLUTION ENGINE TEST ===\n");
        results.AppendLine("Testing LLM critique accuracy against ground truth");
        results.AppendLine("Project: optiver/ar24-4\n");
        results.AppendLine("=================================================\n\n");

        try
        {
            // 1. Load generated hierarchy (try output first, fall back to metadata/manual)
            var generatedPath = "/app/data/output/optiver/projects/ar24-4/hierarchy.xml";
            if (!File.Exists(generatedPath))
            {
                // Fall back to manual-hierarchy.xml (which we'll treat as generated for testing)
                generatedPath = "/app/data/input/optiver/projects/ar24-4/metadata/manual-hierarchy.xml";
                results.AppendLine($"⚠ Using manual-hierarchy.xml as generated (no rule-based hierarchy found)");
            }

            if (!File.Exists(generatedPath))
            {
                results.AppendLine($"✗ Error: Generated hierarchy not found at {generatedPath}");
                results.AppendLine("Please generate hierarchy first using rule-based mode or ensure manual-hierarchy.xml exists.");
                await context.Response.WriteAsync(results.ToString());
                return;
            }

            var generatedStructure = await hierarchyService.LoadHierarchyAsync(generatedPath);
            var generatedHierarchy = generatedStructure.Root;

            results.AppendLine($"✓ Loaded generated hierarchy: {CountItems(generatedHierarchy)} items");

            // 2. Load ground truth hierarchy
            var groundTruthPath = "/app/data/input/optiver/projects/ar24-4/metadata/ground-truth-hierarchy.xml";
            if (!File.Exists(groundTruthPath))
            {
                results.AppendLine($"✗ Error: Ground truth not found at {groundTruthPath}");
                await context.Response.WriteAsync(results.ToString());
                return;
            }

            var groundTruthStructure = await hierarchyService.LoadHierarchyAsync(groundTruthPath);
            var groundTruth = groundTruthStructure.Root;

            results.AppendLine($"✓ Loaded ground truth: {CountItems(groundTruth)} items");
            results.AppendLine();

            // 4. Compare hierarchies
            var comparison = comparisonService.Compare(generatedHierarchy, groundTruth);

            results.AppendLine("--- RULE-BASED GENERATOR ACCURACY ---");
            results.AppendLine($"Total Items: {comparison.TotalItems}");
            results.AppendLine($"Correct: {comparison.CorrectCount} ({comparison.Accuracy:P1})");
            results.AppendLine($"Incorrect: {comparison.IncorrectCount}");
            results.AppendLine($"Missing: {comparison.MissingItems.Count}");
            results.AppendLine($"Extra: {comparison.ExtraItems.Count}");
            results.AppendLine();

            // 5. Extract uncertain decisions (confidence < 0.9)
            var uncertainMatches = comparison.Matches
                .Where(m => m.ConfidenceScore < 0.9)
                .OrderBy(m => m.ConfidenceScore)
                .ToList();

            results.AppendLine($"--- UNCERTAIN DECISIONS (Confidence < 0.9) ---");
            results.AppendLine($"Found {uncertainMatches.Count} uncertain decisions for LLM review\n");

            if (uncertainMatches.Count == 0)
            {
                results.AppendLine("No uncertain decisions found. All rule-based decisions are high confidence.");
                await context.Response.WriteAsync(results.ToString());
                return;
            }

            // 6. Test LLM critique on uncertain decisions
            int testCount = Math.Min(5, uncertainMatches.Count); // Test first 5
            int llmCorrect = 0;
            int llmTested = 0;

            results.AppendLine($"Testing LLM critique on first {testCount} uncertain decisions...\n");

            for (int i = 0; i < testCount; i++)
            {
                var match = uncertainMatches[i];

                results.AppendLine($"TEST {i + 1}: {match.LinkName}");
                results.AppendLine($"  Rule-Based: Level {match.GeneratedLevel} (Confidence: {match.ConfidenceScore:F2})");
                results.AppendLine($"  Ground Truth: Level {match.GroundTruthLevel}");
                results.AppendLine($"  Rule-Based Correct: {(match.IsCorrect ? "✓" : "✗")}");

                // Build uncertain decision for LLM critique
                var decision = new UncertainDecision
                {
                    HeaderText = match.LinkName,
                    CurrentLevel = match.GeneratedLevel,
                    CurrentParent = match.GeneratedParent ?? "Unknown",
                    DataNumber = null, // Would need to extract from headers
                    WordCount = 500, // Would need to calculate from content
                    ChildHeaderCount = 0, // Would need to count from hierarchy
                    UncertaintyReason = $"Low confidence ({match.ConfidenceScore:F2})"
                };

                try
                {
                    var prompt = BuildCritiquePrompt(decision);
                    var response = await ollamaService.GenerateAsync(
                        model: "deepseek-coder:33b",
                        prompt: prompt,
                        temperature: 0.1,
                        cancellationToken: CancellationToken.None
                    );

                    var critique = ParseCritiqueResponse(response);

                    if (critique != null)
                    {
                        // Interpret LLM recommendation
                        var llmRecommendedLevel = InterpretRecommendation(
                            critique.Recommendation,
                            match.GeneratedLevel
                        );

                        bool llmCorrectDecision = (llmRecommendedLevel == match.GroundTruthLevel);

                        results.AppendLine($"  LLM Recommendation: {critique.Recommendation} → Level {llmRecommendedLevel}");
                        results.AppendLine($"  LLM Correct: {(llmCorrectDecision ? "✓" : "✗")}");
                        results.AppendLine($"  Reasoning: {critique.Reasoning}");

                        llmTested++;
                        if (llmCorrectDecision)
                        {
                            llmCorrect++;
                        }
                    }
                    else
                    {
                        results.AppendLine("  ✗ Failed to parse LLM response");
                    }
                }
                catch (Exception ex)
                {
                    results.AppendLine($"  ✗ Error: {ex.Message}");
                }

                results.AppendLine();
            }

            // 7. Summary
            results.AppendLine("=== SUMMARY ===");
            results.AppendLine($"Rule-Based Accuracy: {comparison.Accuracy:P1} ({comparison.CorrectCount}/{comparison.TotalItems})");

            if (llmTested > 0)
            {
                double llmAccuracy = (double)llmCorrect / llmTested;
                results.AppendLine($"LLM Critique Accuracy: {llmAccuracy:P1} ({llmCorrect}/{llmTested} uncertain cases)");
                results.AppendLine();
                results.AppendLine("Next Steps:");
                results.AppendLine("- If LLM accuracy > 80%, proceed to integration");
                results.AppendLine("- If LLM accuracy < 80%, refine prompt and iterate");
            }
        }
        catch (Exception ex)
        {
            results.AppendLine($"\n✗ Fatal Error: {ex.Message}");
            results.AppendLine($"Stack: {ex.StackTrace}");
        }

        await context.Response.WriteAsync(results.ToString());
    }

    private static int CountItems(HierarchyItem root)
    {
        int count = root.Level > 0 ? 1 : 0;
        foreach (var child in root.SubItems)
        {
            count += CountItems(child);
        }
        return count;
    }

    private static int InterpretRecommendation(string recommendation, int currentLevel)
    {
        return recommendation.ToUpper() switch
        {
            "A" => currentLevel, // Keep as-is
            "B" => Math.Max(1, currentLevel - 1), // Promote
            "C" => currentLevel + 1, // Demote
            "D" => currentLevel, // Merge (keep same level)
            _ => currentLevel
        };
    }

    /// <summary>
    /// Parse and validate LLM critique response JSON
    /// </summary>
    private static CritiqueResponse? ParseCritiqueResponse(string response)
    {
        try
        {
            // Clean up response (remove markdown code blocks if present)
            var cleaned = response.Trim();
            if (cleaned.StartsWith("```json"))
            {
                cleaned = cleaned.Substring(7);
            }
            if (cleaned.StartsWith("```"))
            {
                cleaned = cleaned.Substring(3);
            }
            if (cleaned.EndsWith("```"))
            {
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            }
            cleaned = cleaned.Trim();

            // Try to extract JSON if there's extra text
            var jsonStart = cleaned.IndexOf('{');
            var jsonEnd = cleaned.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var critique = System.Text.Json.JsonSerializer.Deserialize<CritiqueResponse>(
                cleaned,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            // Validate
            if (critique != null &&
                !string.IsNullOrEmpty(critique.Recommendation) &&
                critique.Recommendation.Length == 1 &&
                "ABCD".Contains(critique.Recommendation.ToUpper()) &&
                !string.IsNullOrEmpty(critique.Reasoning) &&
                critique.Confidence >= 0 && critique.Confidence <= 100)
            {
                critique.Recommendation = critique.Recommendation.ToUpper();
                return critique;
            }
        }
        catch (Exception)
        {
            // Parsing failed
        }

        return null;
    }

    /// <summary>
    /// Check if reasoning contains metric citations (content categories, structure patterns, etc.)
    /// </summary>
    private static bool ContainsMetricCitation(string reasoning)
    {
        // Check if reasoning contains metric categories
        var metricKeywords = new[]
        {
            "very short", "short", "typical", "long", "very long",
            "flat", "shallow", "hierarchical",
            "words", "children", "level"
        };

        reasoning = reasoning.ToLowerInvariant();
        return metricKeywords.Any(keyword => reasoning.Contains(keyword));
    }

    /// <summary>
    /// Complete LLM hierarchy refinement workflow simulation (Week 3 preview).
    /// Tests the ENTIRE end-to-end process in sandbox mode:
    /// 1. Generate rule-based hierarchy with confidence scores
    /// 2. Compare with ground truth
    /// 3. Extract uncertain decisions (confidence < 0.9)
    /// 4. Run LLM critique on uncertain cases
    /// 5. Show recommendations in HTML review UI format
    /// 6. Simulate user accept/reject (hardcoded for testing)
    /// 7. Extract patterns from decisions
    /// 8. Propose new rules
    /// </summary>
    private static async Task HandleFullWorkflowSimulationAsync(HttpContext context, ILogger logger)
    {
        var hierarchyGenerator = context.RequestServices.GetRequiredService<IHierarchyGeneratorService>();
        var comparisonService = context.RequestServices.GetRequiredService<HierarchyComparisonService>();
        var ollamaService = context.RequestServices.GetRequiredService<IOllamaService>();
        var hierarchyService = context.RequestServices.GetRequiredService<IHierarchyService>();

        var results = new System.Text.StringBuilder();
        results.AppendLine("<!DOCTYPE html>");
        results.AppendLine("<html><head><style>");
        results.AppendLine(@"
            body { font-family: system-ui; max-width: 1200px; margin: 20px auto; padding: 0 20px; background: #1e1e1e; color: #cccccc; }
            h1 { color: #4fc3f7; border-bottom: 3px solid #4fc3f7; padding-bottom: 10px; }
            h2 { color: #9cdcfe; margin-top: 30px; }
            h3 { color: #dcdcaa; margin-top: 20px; }
            h4 { color: #d7ba7d; margin-top: 15px; }
            .step { background: #252526; padding: 15px; margin: 10px 0; border-radius: 8px; border-left: 4px solid #007acc; }
            .success { color: #4ec9b0; font-weight: bold; }
            .warning { color: #d7ba7d; font-weight: bold; }
            .error { color: #f48771; font-weight: bold; }
            .metric { display: inline-block; background: #3c3c3c; padding: 5px 10px; margin: 5px; border-radius: 4px; border: 1px solid #007acc; }
            .recommendation { background: #2d2d30; border: 2px solid #3e3e42; padding: 15px; margin: 15px 0; border-radius: 8px; }
            .llm-response { background: #264f78; padding: 10px; margin: 10px 0; border-left: 4px solid #569cd6; }
            .user-action { background: #1a472a; padding: 10px; margin: 10px 0; border-left: 4px solid #4ec9b0; }
            .user-reject { background: #4a1f1f; padding: 10px; margin: 10px 0; border-left: 4px solid #f48771; }
            .pattern { background: #2e2139; padding: 10px; margin: 10px 0; border-left: 4px solid #c586c0; }
            .rule-proposal { background: #3b3b2e; padding: 15px; margin: 10px 0; border: 2px solid #d7ba7d; border-radius: 8px; }
            pre { background: #1e1e1e; color: #dcdcaa; padding: 10px; border-radius: 4px; overflow-x: auto; border: 1px solid #3e3e42; }
            .confidence-high { color: #4ec9b0; font-weight: bold; }
            .confidence-med { color: #d7ba7d; font-weight: bold; }
            .confidence-low { color: #f48771; font-weight: bold; }
            p { line-height: 1.6; }
            ul { line-height: 1.8; }
            em { color: #9cdcfe; }
        ");
        results.AppendLine("</style></head><body>");

        results.AppendLine("<h1>🤖 LLM Hierarchy Refinement - Full Workflow Simulation</h1>");
        results.AppendLine("<p><em>Testing complete end-to-end process in sandbox mode (Week 3 preview)</em></p>");
        results.AppendLine("<p><strong>Project:</strong> optiver/ar24-4</p>");

        try
        {
            // STEP 1: Generate Rule-Based Hierarchy
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 1: Generate Rule-Based Hierarchy</h2>");

            var normalizedPath = "/app/data/output/optiver/projects/ar24-4/normalized.xml";
            var normalizedXml = await File.ReadAllTextAsync(normalizedPath);

            // Generate rule-based hierarchy (extracts headers internally)
            var proposal = await hierarchyGenerator.GenerateHierarchyAsync(
                normalizedXml,
                new List<string>(), // empty examples for rule-based mode
                "unused-model-for-rule-based",
                CancellationToken.None);

            var generatedHierarchy = proposal.Root;

            var allItems = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(generatedHierarchy, allItems);
            var itemsWithoutRoot = allItems.Where(i => i.Level > 0).ToList();

            var highConf = itemsWithoutRoot.Count(i => i.ConfidenceScore >= 0.9);
            var medConf = itemsWithoutRoot.Count(i => i.ConfidenceScore >= 0.6 && i.ConfidenceScore < 0.9);
            var lowConf = itemsWithoutRoot.Count(i => i.ConfidenceScore < 0.6);

            results.AppendLine($"<p class='success'>✓ Generated {itemsWithoutRoot.Count} items</p>");
            results.AppendLine("<div>");
            results.AppendLine($"<span class='metric confidence-high'>High (≥0.9): {highConf} ({(highConf*100.0/itemsWithoutRoot.Count):F1}%)</span>");
            results.AppendLine($"<span class='metric confidence-med'>Medium (0.6-0.9): {medConf} ({(medConf*100.0/itemsWithoutRoot.Count):F1}%)</span>");
            results.AppendLine($"<span class='metric confidence-low'>Low (<0.6): {lowConf} ({(lowConf*100.0/itemsWithoutRoot.Count):F1}%)</span>");
            results.AppendLine("</div>");
            results.AppendLine("</div>");

            // STEP 2: Load Ground Truth & Compare
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 2: Compare with Ground Truth</h2>");

            var groundTruthPath = "/app/data/input/optiver/projects/ar24-4/metadata/ground-truth-hierarchy.xml";
            var groundTruthStructure = await hierarchyService.LoadHierarchyAsync(groundTruthPath);
            var groundTruth = groundTruthStructure.Root;

            var comparison = comparisonService.Compare(generatedHierarchy, groundTruth);

            results.AppendLine($"<p class='success'>✓ Rule-Based Accuracy: {comparison.Accuracy:P1} ({comparison.CorrectCount}/{comparison.TotalItems})</p>");
            results.AppendLine($"<p><span class='metric'>Correct: {comparison.CorrectCount}</span>");
            results.AppendLine($"<span class='metric'>Incorrect: {comparison.IncorrectCount}</span>");
            results.AppendLine($"<span class='metric'>Missing: {comparison.MissingItems.Count}</span>");
            results.AppendLine($"<span class='metric'>Extra: {comparison.ExtraItems.Count}</span></p>");
            results.AppendLine("</div>");

            // STEP 3: Extract Uncertain Decisions
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 3: Extract Uncertain Decisions for LLM Review</h2>");

            var uncertainItems = itemsWithoutRoot
                .Where(i => i.ConfidenceScore < 0.9)
                .OrderBy(i => i.ConfidenceScore)
                .Take(5) // Test with first 5
                .ToList();

            results.AppendLine($"<p class='success'>✓ Found {uncertainItems.Count} uncertain decisions (testing first 5)</p>");
            results.AppendLine("<p><em>In production, these would be shown in the Review UI for user decision</em></p>");
            results.AppendLine("</div>");

            // STEP 4: LLM Critique with Review UI Simulation
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 4: LLM Critique & Review UI Simulation</h2>");

            var critiqueResults = new List<CritiqueResult>();

            // Simulate user responses (for testing pattern learning)
            var simulatedUserResponses = new Dictionary<string, string>
            {
                // User accepts first 2, rejects last 3
                { uncertainItems[0].LinkName, "accept" },
                { uncertainItems[1].LinkName, "accept" },
                { uncertainItems[2].LinkName, "reject" },
                { uncertainItems[3].LinkName, "reject" },
                { uncertainItems[4].LinkName, "reject" }
            };

            int critiqueNum = 1;
            foreach (var item in uncertainItems)
            {
                results.AppendLine($"<div class='recommendation'>");
                results.AppendLine($"<h3>Decision {critiqueNum}: {item.LinkName}</h3>");
                results.AppendLine($"<p><strong>Current:</strong> Level {item.Level}</p>");
                results.AppendLine($"<p><strong>Confidence:</strong> <span class='confidence-low'>{item.ConfidenceScore:F2}</span></p>");
                results.AppendLine($"<p><strong>Reason:</strong> {item.Reasoning}</p>");
                results.AppendLine($"<p><strong>Context:</strong> {item.WordCount} words, {item.ChildCount} children</p>");

                // Build uncertain decision for LLM
                var decision = new UncertainDecision
                {
                    HeaderText = item.LinkName,
                    CurrentLevel = item.Level,
                    CurrentParent = "Unknown", // Would extract from hierarchy
                    DataNumber = null,
                    WordCount = item.WordCount,
                    ChildHeaderCount = item.ChildCount,
                    PreviousHeader = item.PreviousHeader,
                    PreviousSiblingLevel = 1,
                    NextHeader = item.NextHeader,
                    NextSiblingLevel = 1,
                    UncertaintyReason = item.Reasoning ?? "Low confidence"
                };

                try
                {
                    results.AppendLine("<div class='llm-response'>");
                    results.AppendLine("<h4>🤖 LLM Critique</h4>");

                    var prompt = BuildCritiquePrompt(decision);
                    var response = await ollamaService.GenerateAsync(
                        model: "mistral:latest",
                        prompt: prompt,
                        temperature: 0.1f,
                        cancellationToken: CancellationToken.None
                    );

                    var critique = ParseCritiqueResponse(response);

                    if (critique != null)
                    {
                        results.AppendLine($"<p><strong>Recommendation:</strong> {critique.Recommendation}</p>");
                        results.AppendLine($"<p><strong>Reasoning:</strong> {critique.Reasoning}</p>");
                        results.AppendLine($"<p><strong>Confidence:</strong> {critique.Confidence}%</p>");

                        critiqueResults.Add(new CritiqueResult
                        {
                            Item = item,
                            LlmRecommendation = critique.Recommendation,
                            LlmReasoning = critique.Reasoning,
                            LlmConfidence = critique.Confidence
                        });
                    }
                    else
                    {
                        results.AppendLine("<p class='error'>✗ Failed to parse LLM response</p>");
                        results.AppendLine($"<pre>{response}</pre>");
                    }

                    results.AppendLine("</div>");

                    // Simulate user action
                    var userResponse = simulatedUserResponses[item.LinkName];
                    var actionClass = userResponse == "accept" ? "user-action" : "user-reject";
                    results.AppendLine($"<div class='{actionClass}'>");
                    results.AppendLine($"<h4>👤 User Decision: {userResponse.ToUpper()}</h4>");
                    results.AppendLine($"<p><em>(Simulated for testing - in production this would be a button click)</em></p>");
                    results.AppendLine("</div>");

                    if (critiqueResults.Any())
                    {
                        critiqueResults.Last().UserAccepted = (userResponse == "accept");
                    }
                }
                catch (Exception ex)
                {
                    results.AppendLine($"<p class='error'>✗ Error: {ex.Message}</p>");
                }

                results.AppendLine("</div>");
                critiqueNum++;
            }

            results.AppendLine("</div>");

            // STEP 5: Pattern Learning
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 5: Pattern Learning & Analysis</h2>");

            var acceptedPatterns = critiqueResults
                .Where(r => r.UserAccepted)
                .Select(r => ExtractPattern(r))
                .ToList();

            var rejectedPatterns = critiqueResults
                .Where(r => !r.UserAccepted)
                .Select(r => ExtractPattern(r))
                .ToList();

            results.AppendLine($"<p><span class='metric'>Accepted: {acceptedPatterns.Count}</span>");
            results.AppendLine($"<span class='metric'>Rejected: {rejectedPatterns.Count}</span></p>");

            foreach (var pattern in acceptedPatterns)
            {
                results.AppendLine("<div class='pattern'>");
                results.AppendLine($"<h4>✓ Accepted Pattern</h4>");
                results.AppendLine($"<p><strong>Decision:</strong> {pattern.HeaderText} → Level {pattern.Level}</p>");
                results.AppendLine($"<p><strong>LLM Recommendation:</strong> {pattern.LlmRecommendation}</p>");
                results.AppendLine($"<p><strong>Characteristics:</strong></p>");
                results.AppendLine("<ul>");
                if (!string.IsNullOrEmpty(pattern.DataNumber))
                    results.AppendLine($"<li>Data-number: {pattern.DataNumber}</li>");
                else
                    results.AppendLine("<li>No data-number</li>");
                results.AppendLine($"<li>Word count: {pattern.WordCount}</li>");
                results.AppendLine($"<li>Child count: {pattern.ChildCount}</li>");
                results.AppendLine("</ul>");
                results.AppendLine("</div>");
            }

            foreach (var pattern in rejectedPatterns)
            {
                results.AppendLine("<div class='pattern'>");
                results.AppendLine($"<h4>✗ Rejected Pattern</h4>");
                results.AppendLine($"<p><strong>Decision:</strong> {pattern.HeaderText} → Level {pattern.Level}</p>");
                results.AppendLine($"<p><strong>LLM suggested:</strong> {pattern.LlmRecommendation}</p>");
                results.AppendLine($"<p><em>User disagreed - pattern avoided</em></p>");
                results.AppendLine("</div>");
            }

            results.AppendLine("</div>");

            // STEP 6: Rule Proposal
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Step 6: Automatic Rule Proposal</h2>");

            // Analyze accepted patterns for common characteristics
            var proposedRules = ProposeRulesFromPatterns(acceptedPatterns, rejectedPatterns);

            if (proposedRules.Any())
            {
                foreach (var rule in proposedRules)
                {
                    results.AppendLine("<div class='rule-proposal'>");
                    results.AppendLine($"<h4>💡 Proposed Rule</h4>");
                    results.AppendLine($"<p><strong>Pattern:</strong> {rule.Description}</p>");
                    results.AppendLine($"<p><strong>Evidence:</strong> {rule.EvidenceCount} cases</p>");
                    results.AppendLine("<p><strong>Proposed Code:</strong></p>");
                    results.AppendLine($"<pre>{rule.CodeSuggestion}</pre>");
                    results.AppendLine("</div>");
                }
            }
            else
            {
                results.AppendLine("<p><em>Not enough patterns yet to propose rules (need 2+ similar cases)</em></p>");
            }

            results.AppendLine("</div>");

            // Summary
            results.AppendLine("<div class='step'>");
            results.AppendLine("<h2>Summary & Next Steps</h2>");
            results.AppendLine($"<p><strong>Workflow Status:</strong> <span class='success'>✓ Complete</span></p>");
            results.AppendLine("<ul>");
            results.AppendLine($"<li>Generated hierarchy with {itemsWithoutRoot.Count} items</li>");
            results.AppendLine($"<li>Identified {uncertainItems.Count} uncertain decisions</li>");
            results.AppendLine($"<li>LLM critiqued {critiqueResults.Count} cases</li>");
            results.AppendLine($"<li>User accepted {acceptedPatterns.Count}, rejected {rejectedPatterns.Count}</li>");
            results.AppendLine($"<li>Proposed {proposedRules.Count} new rules</li>");
            results.AppendLine("</ul>");
            results.AppendLine("<p><strong>Ready for:</strong></p>");
            results.AppendLine("<ul>");
            results.AppendLine("<li>Service integration (extract logic to LlmCritiqueService)</li>");
            results.AppendLine("<li>UI implementation (convert HTML mockup to Blazor components)</li>");
            results.AppendLine("<li>Pattern learning storage (database or JSON files)</li>");
            results.AppendLine("</ul>");
            results.AppendLine("<p><strong>Test Command:</strong></p>");
            results.AppendLine("<pre>curl http://localhost:8085/sandbox?mode=full-workflow</pre>");
            results.AppendLine("</div>");
        }
        catch (Exception ex)
        {
            results.AppendLine($"<div class='error'>");
            results.AppendLine($"<h2>Error</h2>");
            results.AppendLine($"<p>{ex.Message}</p>");
            results.AppendLine($"<pre>{ex.StackTrace}</pre>");
            results.AppendLine("</div>");
        }

        results.AppendLine("</body></html>");

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(results.ToString());
    }

    // Helper classes for full workflow simulation
    private class CritiqueResult
    {
        public required PdfConversion.Models.HierarchyItem Item { get; set; }
        public string LlmRecommendation { get; set; } = string.Empty;
        public string LlmReasoning { get; set; } = string.Empty;
        public int LlmConfidence { get; set; }
        public bool UserAccepted { get; set; }
    }

    private class DecisionPattern
    {
        public string HeaderText { get; set; } = string.Empty;
        public int Level { get; set; }
        public string? DataNumber { get; set; }
        public int WordCount { get; set; }
        public int ChildCount { get; set; }
        public string LlmRecommendation { get; set; } = string.Empty;
        public bool UserAccepted { get; set; }
    }

    private class ProposedRule
    {
        public string Description { get; set; } = string.Empty;
        public int EvidenceCount { get; set; }
        public string CodeSuggestion { get; set; } = string.Empty;
    }

    private static DecisionPattern ExtractPattern(CritiqueResult result)
    {
        return new DecisionPattern
        {
            HeaderText = result.Item.LinkName,
            Level = result.Item.Level,
            DataNumber = null, // Would extract from item
            WordCount = result.Item.WordCount,
            ChildCount = result.Item.ChildCount,
            LlmRecommendation = result.LlmRecommendation,
            UserAccepted = result.UserAccepted
        };
    }

    private static List<ProposedRule> ProposeRulesFromPatterns(
        List<DecisionPattern> accepted,
        List<DecisionPattern> rejected)
    {
        var rules = new List<ProposedRule>();

        // Find patterns in accepted decisions
        // Rule 1: Short content without data-number
        var shortNoNumber = accepted
            .Where(p => p.WordCount < 100 && string.IsNullOrEmpty(p.DataNumber))
            .ToList();

        if (shortNoNumber.Count >= 2)
        {
            rules.Add(new ProposedRule
            {
                Description = "Short content (<100 words) without data-number should be Level 2+",
                EvidenceCount = shortNoNumber.Count,
                CodeSuggestion = @"// Pattern: Short content without numbering
if (header.WordCount < 100 && string.IsNullOrEmpty(header.DataNumber))
{
    level = Math.Max(2, suggestedLevel);
    confidence = 0.75;
    reasoning = ""Short content without numbering - typically subsection"";
}"
            });
        }

        // Rule 2: Long content suggests major section
        var longContent = accepted
            .Where(p => p.WordCount > 1000)
            .ToList();

        if (longContent.Count >= 2)
        {
            rules.Add(new ProposedRule
            {
                Description = "Long content (>1000 words) suggests Level 1 major section",
                EvidenceCount = longContent.Count,
                CodeSuggestion = @"// Pattern: Long substantial content
if (header.WordCount > 1000 && header.ChildCount > 2)
{
    level = 1;
    confidence = 0.85;
    reasoning = ""Substantial content with children - major section"";
}"
            });
        }

        return rules;
    }

    /// <summary>
    /// Generates detailed LLM accuracy report comparing LLM recommendations against ground truth.
    /// Tests LLM critique on all uncertain items to measure how often LLM is correct.
    /// </summary>
    private static async Task HandleLlmAccuracyReportAsync(HttpContext context, ILogger logger)
    {
        var hierarchyService = context.RequestServices.GetRequiredService<IHierarchyService>();
        var hierarchyGeneratorService = context.RequestServices.GetRequiredService<IHierarchyGeneratorService>();
        var comparisonService = context.RequestServices.GetRequiredService<HierarchyComparisonService>();
        var ollamaService = context.RequestServices.GetRequiredService<IOllamaService>();

        var results = new System.Text.StringBuilder();
        results.AppendLine("<!DOCTYPE html>");
        results.AppendLine("<html><head><style>");
        results.AppendLine(@"
        body { font-family: system-ui; max-width: 1400px; margin: 20px auto; padding: 0 20px; background: #1e1e1e; color: #d4d4d4; }
        h1 { color: #4fc3f7; border-bottom: 3px solid #4fc3f7; padding-bottom: 10px; }
        h2 { color: #9cdcfe; margin-top: 30px; }
        .summary { background: #2d2d30; padding: 20px; margin: 20px 0; border-radius: 8px; border: 2px solid #4fc3f7; }
        .metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 15px; margin: 20px 0; }
        .metric-card { background: #252526; padding: 15px; border-radius: 8px; border: 1px solid #3e3e42; }
        .metric-label { color: #858585; font-size: 0.9em; margin-bottom: 5px; }
        .metric-value { color: #4fc3f7; font-size: 2em; font-weight: bold; }
        .comparison { background: #252526; padding: 15px; margin: 15px 0; border-radius: 8px; border-left: 4px solid #858585; }
        .comparison.correct { border-left-color: #4ec9b0; }
        .comparison.incorrect { border-left-color: #f48771; }
        .comparison.no-match { border-left-color: #ce9178; }
        .header-info { display: flex; justify-content: space-between; align-items: center; margin-bottom: 10px; }
        .level-badge { display: inline-block; padding: 4px 12px; border-radius: 4px; font-size: 0.9em; font-weight: bold; }
        .level-rule { background: #264f78; color: #9cdcfe; }
        .level-llm { background: #1a472a; color: #4ec9b0; }
        .level-truth { background: #5a1e1e; color: #f48771; }
        .confidence { color: #ce9178; font-weight: bold; }
        .result-correct { color: #4ec9b0; font-weight: bold; }
        .result-incorrect { color: #f48771; font-weight: bold; }
        .result-no-match { color: #ce9178; font-weight: bold; }
        .reasoning { background: #1e1e1e; padding: 10px; margin: 10px 0; border-radius: 4px; border: 1px solid #3e3e42; color: #d7ba7d; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; background: #252526; }
        th { background: #2d2d30; color: #4fc3f7; padding: 12px; text-align: left; border-bottom: 2px solid #4fc3f7; }
        td { padding: 12px; border-bottom: 1px solid #3e3e42; }
        tr:hover { background: #2a2d2e; }
        .loading { color: #ce9178; font-style: italic; }
    ");
        results.AppendLine("</style></head><body>");

        results.AppendLine("<h1>🎯 LLM Accuracy Report vs Ground Truth</h1>");
        results.AppendLine("<p><em>Detailed comparison: Rule-Based → LLM → Ground Truth</em></p>");

        try
        {
            // Generate fresh hierarchy with confidence scores
            results.AppendLine("<p class='loading'>Generating rule-based hierarchy with confidence scores...</p>");

            var normalizedPath = "/app/data/output/optiver/projects/ar24-4/normalized.xml";
            var normalizedXml = await File.ReadAllTextAsync(normalizedPath);

            var proposal = await hierarchyGeneratorService.GenerateHierarchyAsync(
                normalizedXml,
                new List<string>(), // empty examples for rule-based mode
                "unused-model-for-rule-based",
                CancellationToken.None);
            var generated = proposal.Root;

            results.AppendLine("<p class='loading'>Loading ground truth...</p>");

            var groundTruthPath = "/app/data/input/optiver/projects/ar24-4/metadata/ground-truth-hierarchy.xml";
            var groundTruthStructure = await hierarchyService.LoadHierarchyAsync(groundTruthPath);
            var groundTruth = groundTruthStructure.Root;

            // Compare
            var comparison = comparisonService.Compare(generated, groundTruth);

            // Extract uncertain items
            var allGenerated = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(generated, allGenerated);

            var uncertainItems = allGenerated
                .Where(i => i.Level > 0 && i.ConfidenceScore < 0.9)
                .OrderBy(i => i.ConfidenceScore)
                .ToList();

            // Build ground truth lookup (handle duplicates with GroupBy)
            var allTruth = new List<PdfConversion.Models.HierarchyItem>();
            CollectAllItems(groundTruth, allTruth);
            var truthLookup = allTruth
                .Where(i => i.Level > 0)
                .GroupBy(i => i.LinkName.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First()); // Use first occurrence if duplicates exist

            // Summary section
            results.AppendLine("<div class='summary'>");
            results.AppendLine("<h2>Summary</h2>");
            results.AppendLine("<div class='metric-grid'>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>Total Items Generated</div>");
            results.AppendLine($"<div class='metric-value'>{allGenerated.Count(i => i.Level > 0)}</div>");
            results.AppendLine("</div>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>Uncertain Items (< 0.9)</div>");
            results.AppendLine($"<div class='metric-value'>{uncertainItems.Count}</div>");
            results.AppendLine("</div>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>Rule-Based Accuracy</div>");
            results.AppendLine($"<div class='metric-value'>{comparison.Accuracy:P1}</div>");
            results.AppendLine($"<div class='metric-label'>{comparison.CorrectCount}/{comparison.TotalItems} correct</div>");
            results.AppendLine("</div>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>Ground Truth Items</div>");
            results.AppendLine($"<div class='metric-value'>{allTruth.Count(i => i.Level > 0)}</div>");
            results.AppendLine("</div>");

            results.AppendLine("</div>");
            results.AppendLine("</div>");

            // Test LLM on uncertain items
            results.AppendLine("<h2>Detailed Analysis: LLM Critique Results</h2>");
            results.AppendLine($"<p>Testing LLM critique on <strong>{uncertainItems.Count}</strong> uncertain decisions...</p>");

            int llmCorrect = 0;
            int llmIncorrect = 0;
            int noMatch = 0;
            int testCount = 0;

            foreach (var item in uncertainItems)
            {
                testCount++;
                var key = item.LinkName.ToLowerInvariant();

                results.AppendLine("<div class='comparison");

                // Check if exists in ground truth
                if (truthLookup.TryGetValue(key, out var truthItem))
                {
                    // Build decision for LLM
                    var decision = new UncertainDecision
                    {
                        HeaderText = item.LinkName,
                        CurrentLevel = item.Level,
                        CurrentParent = "Unknown",
                        DataNumber = null,
                        WordCount = item.WordCount,
                        ChildHeaderCount = item.ChildCount,
                        PreviousHeader = null,
                        PreviousSiblingLevel = 1,
                        NextHeader = null,
                        NextSiblingLevel = 1,
                        UncertaintyReason = item.Reasoning ?? "Low confidence"
                    };

                    // Get LLM critique
                    var prompt = BuildCritiquePrompt(decision);
                    var response = await ollamaService.GenerateAsync(
                        model: "mistral:latest",
                        prompt: prompt,
                        temperature: 0.1
                    );

                    var critique = ParseCritiqueResponse(response);

                    if (critique != null)
                    {
                        // Interpret LLM recommendation
                        var llmLevel = InterpretRecommendation(critique.Recommendation, item.Level);
                        bool isCorrect = (llmLevel == truthItem.Level);

                        if (isCorrect)
                        {
                            llmCorrect++;
                            results.AppendLine(" correct'>");
                        }
                        else
                        {
                            llmIncorrect++;
                            results.AppendLine(" incorrect'>");
                        }

                        results.AppendLine("<div class='header-info'>");
                        results.AppendLine($"<div><strong>#{testCount}: {item.LinkName}</strong></div>");
                        results.AppendLine($"<div class='confidence'>Confidence: {item.ConfidenceScore:F2}</div>");
                        results.AppendLine("</div>");

                        results.AppendLine("<div>");
                        results.AppendLine($"<span class='level-badge level-rule'>Rule-Based: Level {item.Level}</span> ");
                        results.AppendLine($"<span class='level-badge level-llm'>LLM Says: Level {llmLevel} ({critique.Recommendation})</span> ");
                        results.AppendLine($"<span class='level-badge level-truth'>Ground Truth: Level {truthItem.Level}</span> ");

                        if (isCorrect)
                        {
                            results.AppendLine($"<span class='result-correct'>✓ CORRECT</span>");
                        }
                        else
                        {
                            results.AppendLine($"<span class='result-incorrect'>✗ INCORRECT</span>");
                        }
                        results.AppendLine("</div>");

                        results.AppendLine("<div class='reasoning'>");
                        results.AppendLine($"<strong>LLM Reasoning:</strong> {critique.Reasoning}");
                        results.AppendLine($"<br><strong>Confidence:</strong> {critique.Confidence}%");
                        results.AppendLine("</div>");
                    }
                    else
                    {
                        results.AppendLine(" no-match'>");
                        results.AppendLine("<p class='result-no-match'>✗ Failed to parse LLM response</p>");
                    }
                }
                else
                {
                    noMatch++;
                    results.AppendLine(" no-match'>");
                    results.AppendLine("<div class='header-info'>");
                    results.AppendLine($"<div><strong>#{testCount}: {item.LinkName}</strong></div>");
                    results.AppendLine($"<div class='confidence'>Confidence: {item.ConfidenceScore:F2}</div>");
                    results.AppendLine("</div>");
                    results.AppendLine("<p class='result-no-match'>⚠ Not found in ground truth (cannot validate)</p>");
                }

                results.AppendLine("</div>");
            }

            // Final summary
            results.AppendLine("<div class='summary'>");
            results.AppendLine("<h2>Final Results</h2>");

            results.AppendLine("<div class='metric-grid'>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>LLM Tested Cases</div>");
            results.AppendLine($"<div class='metric-value'>{testCount}</div>");
            results.AppendLine("</div>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>LLM Correct</div>");
            results.AppendLine($"<div class='metric-value' style='color: #4ec9b0;'>{llmCorrect}</div>");
            results.AppendLine("</div>");

            results.AppendLine("<div class='metric-card'>");
            results.AppendLine("<div class='metric-label'>LLM Incorrect</div>");
            results.AppendLine($"<div class='metric-value' style='color: #f48771;'>{llmIncorrect}</div>");
            results.AppendLine("</div>");

            if (llmCorrect + llmIncorrect > 0)
            {
                var llmAccuracy = (double)llmCorrect / (llmCorrect + llmIncorrect);
                results.AppendLine("<div class='metric-card'>");
                results.AppendLine("<div class='metric-label'>LLM Accuracy</div>");
                results.AppendLine($"<div class='metric-value'>{llmAccuracy:P1}</div>");
                results.AppendLine($"<div class='metric-label'>{llmCorrect}/{llmCorrect + llmIncorrect} correct</div>");
                results.AppendLine("</div>");
            }

            results.AppendLine("</div>");

            // Comparison table
            results.AppendLine("<h3>Comparison: Rule-Based vs LLM-Enhanced</h3>");
            results.AppendLine("<table>");
            results.AppendLine("<tr>");
            results.AppendLine("<th>Metric</th>");
            results.AppendLine("<th>Rule-Based Only</th>");
            results.AppendLine("<th>LLM-Enhanced (Uncertain Only)</th>");
            results.AppendLine("</tr>");

            results.AppendLine("<tr>");
            results.AppendLine("<td>Baseline Accuracy</td>");
            results.AppendLine($"<td>{comparison.Accuracy:P1} ({comparison.CorrectCount}/{comparison.TotalItems})</td>");
            results.AppendLine("<td>-</td>");
            results.AppendLine("</tr>");

            if (llmCorrect + llmIncorrect > 0)
            {
                var llmAccuracy = (double)llmCorrect / (llmCorrect + llmIncorrect);
                results.AppendLine("<tr>");
                results.AppendLine("<td>Uncertain Items Accuracy</td>");
                results.AppendLine("<td>Would be included in baseline</td>");
                results.AppendLine($"<td>{llmAccuracy:P1} ({llmCorrect}/{llmCorrect + llmIncorrect})</td>");
                results.AppendLine("</tr>");
            }

            results.AppendLine("<tr>");
            results.AppendLine("<td>Items Not in Ground Truth</td>");
            results.AppendLine($"<td>{comparison.ExtraItems.Count} extra</td>");
            results.AppendLine($"<td>{noMatch} uncertain (cannot validate)</td>");
            results.AppendLine("</tr>");

            results.AppendLine("</table>");

            results.AppendLine("<h3>Interpretation</h3>");
            results.AppendLine("<ul>");
            results.AppendLine($"<li>The rule-based generator achieved <strong>{comparison.Accuracy:P1}</strong> accuracy on items that exist in both hierarchies.</li>");

            if (llmCorrect + llmIncorrect > 0)
            {
                var llmAccuracy = (double)llmCorrect / (llmCorrect + llmIncorrect);
                results.AppendLine($"<li>The LLM achieved <strong>{llmAccuracy:P1}</strong> accuracy on uncertain items that could be validated against ground truth.</li>");

                if (llmAccuracy > comparison.Accuracy)
                {
                    results.AppendLine($"<li><strong style='color: #4ec9b0;'>✓ LLM improved accuracy</strong> on uncertain cases by {(llmAccuracy - comparison.Accuracy):P1}.</li>");
                }
                else if (llmAccuracy < comparison.Accuracy)
                {
                    results.AppendLine($"<li><strong style='color: #f48771;'>✗ LLM performed worse</strong> on uncertain cases by {(comparison.Accuracy - llmAccuracy):P1}.</li>");
                }
                else
                {
                    results.AppendLine($"<li>LLM accuracy matches baseline on these cases.</li>");
                }
            }

            results.AppendLine($"<li>However, <strong>{comparison.MissingItems.Count}</strong> items from ground truth are missing in the generated hierarchy, indicating structural differences.</li>");
            results.AppendLine($"<li><strong>{noMatch}</strong> uncertain items could not be validated because they don't exist in ground truth.</li>");
            results.AppendLine("</ul>");

            results.AppendLine("</div>");
        }
        catch (Exception ex)
        {
            results.AppendLine($"<div style='color: #f48771;'>");
            results.AppendLine($"<h2>Error</h2>");
            results.AppendLine($"<p>{ex.Message}</p>");
            results.AppendLine($"<pre>{ex.StackTrace}</pre>");
            results.AppendLine("</div>");
        }

        results.AppendLine("</body></html>");

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(results.ToString());
    }

    /// <summary>
    /// Side-by-side comparison of ground truth vs generated hierarchy for optiver/ar24-4.
    /// Shows visual tree structure with color coding for matches/mismatches.
    /// </summary>
    /// <summary>
    /// Three-way comparison: Rule-Based -> AI-Refined -> Ground Truth
    /// Shows whether applying LLM recommendations improves accuracy
    /// </summary>
    private static async Task HandleHierarchyComparisonAsync(HttpContext context, ILogger logger)
    {
        var hierarchyService = context.RequestServices.GetRequiredService<IHierarchyService>();
        var hierarchyGeneratorService = context.RequestServices.GetRequiredService<IHierarchyGeneratorService>();
        var ollamaService = context.RequestServices.GetRequiredService<IOllamaService>();

        var html = new System.Text.StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='utf-8'>");
        html.AppendLine("<title>Three-Way Hierarchy Comparison - optiver/ar24-4</title>");
        html.AppendLine("<style>");
        // VS Code Dark Modern theme colors
        html.AppendLine("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #1e1e1e; color: #cccccc; padding: 20px; margin: 0; }");
        html.AppendLine("h1 { color: #4fc3f7; border-bottom: 2px solid #569cd6; padding-bottom: 10px; }");
        html.AppendLine("h2 { color: #9cdcfe; margin-top: 30px; }");
        html.AppendLine(".container { max-width: 2400px; margin: 0 auto; }");
        html.AppendLine(".stats-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin-bottom: 20px; }");
        html.AppendLine(".stats { background: #252526; padding: 20px; border-radius: 8px; border-left: 4px solid #569cd6; }");
        html.AppendLine(".stats h3 { color: #4fc3f7; margin-top: 0; }");
        html.AppendLine(".stats table { width: 100%; border-collapse: collapse; }");
        html.AppendLine(".stats td { padding: 8px; border-bottom: 1px solid #3c3c3c; font-size: 13px; }");
        html.AppendLine(".stats td:first-child { font-weight: bold; color: #9cdcfe; width: 150px; }");
        html.AppendLine(".comparison { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; margin-top: 20px; }");
        html.AppendLine(".column { background: #252526; padding: 20px; border-radius: 8px; }");
        html.AppendLine(".column h3 { color: #4fc3f7; margin-top: 0; border-bottom: 1px solid #3c3c3c; padding-bottom: 10px; }");
        html.AppendLine(".tree-item { margin: 5px 0; font-family: 'Consolas', monospace; font-size: 13px; }");
        html.AppendLine(".level-1 { margin-left: 0; font-weight: bold; color: #dcdcaa; }");
        html.AppendLine(".level-2 { margin-left: 20px; color: #9cdcfe; }");
        html.AppendLine(".level-3 { margin-left: 40px; color: #ce9178; }");
        html.AppendLine(".level-4 { margin-left: 60px; color: #b5cea8; }");
        html.AppendLine(".match { background: #1e3a1e; border-left: 3px solid #4ec9b0; padding: 5px; }");
        html.AppendLine(".level-mismatch { background: #3d3d1e; border-left: 3px solid #dcdcaa; padding: 5px; }");
        html.AppendLine(".missing { background: #3d1e1e; border-left: 3px solid #f48771; padding: 5px; }");
        html.AppendLine(".extra { background: #3d2a1e; border-left: 3px solid #ce9178; padding: 5px; }");
        html.AppendLine(".llm-changed { background: #1e2a3d; border-left: 3px solid #569cd6; padding: 5px; }");
        html.AppendLine(".change-indicator { color: #569cd6; font-size: 11px; margin-left: 5px; }");
        html.AppendLine(".legend { background: #252526; padding: 15px; border-radius: 8px; margin-bottom: 20px; display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 10px; }");
        html.AppendLine(".legend-item { display: flex; align-items: center; gap: 10px; }");
        html.AppendLine(".legend-box { width: 20px; height: 20px; border-radius: 3px; }");
        html.AppendLine(".legend-text { color: #cccccc; font-size: 14px; }");
        html.AppendLine(".intro { background: #252526; padding: 20px; border-radius: 8px; margin-bottom: 20px; border-left: 4px solid #4fc3f7; }");
        html.AppendLine(".intro h3 { color: #4fc3f7; margin-top: 0; }");
        html.AppendLine(".intro p { margin: 10px 0; line-height: 1.6; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class='container'>");
        html.AppendLine("<h1>🔍 Three-Way Hierarchy Comparison: Does LLM Refinement Help?</h1>");
        html.AppendLine("<p style='color: #9cdcfe; margin-bottom: 20px;'>Project: <strong>optiver/ar24-4</strong> | Model: <strong>mistral:latest</strong></p>");

        try
        {
            // Intro box
            html.AppendLine("<div class='intro'>");
            html.AppendLine("<h3>📊 Experiment Design</h3>");
            html.AppendLine("<p>This page compares three hierarchy versions to answer: <strong>\"Does applying LLM recommendations improve accuracy vs ground truth?\"</strong></p>");
            html.AppendLine("<p><strong>Column 1 (Rule-Based):</strong> Original output from deterministic rule-based generator</p>");
            html.AppendLine("<p><strong>Column 2 (AI-Refined):</strong> Rule-based hierarchy + LLM recommendations applied (promote/demote/merge uncertain items)</p>");
            html.AppendLine("<p><strong>Column 3 (Ground Truth):</strong> Manual hierarchy from production</p>");
            html.AppendLine("</div>");

            // 1. Load ground truth hierarchy
            var groundTruthPath = "/app/data/output/optiver/projects/ar24-4/hierarchy.xml";
            if (!File.Exists(groundTruthPath))
            {
                html.AppendLine($"<p style='color: #f48771;'>Error: Ground truth not found at {groundTruthPath}</p>");
                html.AppendLine("</div></body></html>");
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(html.ToString());
                return;
            }

            var groundTruthStructure = await hierarchyService.LoadHierarchyAsync(groundTruthPath);
            var groundTruthRoot = groundTruthStructure.Root;

            // 2. Generate rule-based hierarchy
            var normalizedPath = "/app/data/output/optiver/projects/ar24-4/normalized.xml";
            if (!File.Exists(normalizedPath))
            {
                html.AppendLine($"<p style='color: #f48771;'>Error: Normalized XML not found at {normalizedPath}</p>");
                html.AppendLine("</div></body></html>");
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(html.ToString());
                return;
            }

            var normalizedXml = await File.ReadAllTextAsync(normalizedPath);
            var proposal = await hierarchyGeneratorService.GenerateHierarchyAsync(
                normalizedXml,
                new List<string>(),
                "unused-model-for-rule-based",
                CancellationToken.None);
            var ruleBasedRoot = DeepCopyHierarchy(proposal.Root);

            // 3. Create AI-refined version by applying LLM recommendations
            var aiRefinedRoot = await ApplyLlmRefinementsAsync(
                DeepCopyHierarchy(proposal.Root),
                ollamaService,
                logger);

            // 4. Flatten all three for counting
            var ruleBasedFlat = FlattenHierarchy(ruleBasedRoot).Where(i => i.Level > 0).ToList();
            var aiRefinedFlat = FlattenHierarchy(aiRefinedRoot).Where(i => i.Level > 0).ToList();
            var groundTruthFlat = FlattenHierarchy(groundTruthRoot).Where(i => i.Level > 0).ToList();

            // 5. Compare each version to ground truth
            var ruleBasedAccuracy = CalculateAccuracy(ruleBasedFlat, groundTruthFlat);
            var aiRefinedAccuracy = CalculateAccuracy(aiRefinedFlat, groundTruthFlat);

            // 6. Collect LLM changes for highlighting
            var llmChangedNames = CollectLlmChangedItems(ruleBasedFlat, aiRefinedFlat);

            // 7. Build lookup sets for color coding
            var groundTruthLookup = BuildLookup(groundTruthFlat);

            // 8. Summary Statistics
            html.AppendLine("<div class='stats-grid'>");

            // Rule-Based Stats
            html.AppendLine("<div class='stats'>");
            html.AppendLine("<h3>Column 1: Rule-Based</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td>Total Items:</td><td>{ruleBasedFlat.Count}</td></tr>");
            html.AppendLine($"<tr><td>Matches:</td><td style='color: #4ec9b0;'>{ruleBasedAccuracy.CorrectCount}</td></tr>");
            html.AppendLine($"<tr><td>Level Mismatches:</td><td style='color: #dcdcaa;'>{ruleBasedAccuracy.LevelMismatchCount}</td></tr>");
            html.AppendLine($"<tr><td>Missing:</td><td style='color: #f48771;'>{ruleBasedAccuracy.MissingCount}</td></tr>");
            html.AppendLine($"<tr><td>Extra:</td><td style='color: #ce9178;'>{ruleBasedAccuracy.ExtraCount}</td></tr>");
            html.AppendLine($"<tr><td>Accuracy:</td><td style='color: #4fc3f7; font-weight: bold;'>{ruleBasedAccuracy.Accuracy:P1}</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // AI-Refined Stats
            html.AppendLine("<div class='stats'>");
            html.AppendLine("<h3>Column 2: AI-Refined</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td>Total Items:</td><td>{aiRefinedFlat.Count}</td></tr>");
            html.AppendLine($"<tr><td>Matches:</td><td style='color: #4ec9b0;'>{aiRefinedAccuracy.CorrectCount}</td></tr>");
            html.AppendLine($"<tr><td>Level Mismatches:</td><td style='color: #dcdcaa;'>{aiRefinedAccuracy.LevelMismatchCount}</td></tr>");
            html.AppendLine($"<tr><td>Missing:</td><td style='color: #f48771;'>{aiRefinedAccuracy.MissingCount}</td></tr>");
            html.AppendLine($"<tr><td>Extra:</td><td style='color: #ce9178;'>{aiRefinedAccuracy.ExtraCount}</td></tr>");
            html.AppendLine($"<tr><td>LLM Changes:</td><td style='color: #569cd6;'>{llmChangedNames.Count}</td></tr>");
            html.AppendLine($"<tr><td>Accuracy:</td><td style='color: #4fc3f7; font-weight: bold;'>{aiRefinedAccuracy.Accuracy:P1}</td></tr>");

            var improvement = aiRefinedAccuracy.Accuracy - ruleBasedAccuracy.Accuracy;
            var improvementColor = improvement > 0 ? "#4ec9b0" : improvement < 0 ? "#f48771" : "#cccccc";
            var improvementSymbol = improvement > 0 ? "↑" : improvement < 0 ? "↓" : "=";
            html.AppendLine($"<tr><td>Change:</td><td style='color: {improvementColor}; font-weight: bold;'>{improvementSymbol} {improvement:+0.0%;-0.0%;0.0%}</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            // Ground Truth Stats
            html.AppendLine("<div class='stats'>");
            html.AppendLine("<h3>Column 3: Ground Truth</h3>");
            html.AppendLine("<table>");
            html.AppendLine($"<tr><td>Total Items:</td><td>{groundTruthFlat.Count}</td></tr>");
            html.AppendLine($"<tr><td>Source:</td><td>Production</td></tr>");
            html.AppendLine($"<tr><td>Status:</td><td style='color: #4ec9b0;'>✓ Reference</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("</div>");

            // 9. Legend
            html.AppendLine("<div class='legend'>");
            html.AppendLine("<div class='legend-item'><div class='legend-box' style='background: #1e3a1e; border-left: 3px solid #4ec9b0;'></div><span class='legend-text'>Match (correct name & level)</span></div>");
            html.AppendLine("<div class='legend-item'><div class='legend-box' style='background: #3d3d1e; border-left: 3px solid #dcdcaa;'></div><span class='legend-text'>Level mismatch</span></div>");
            html.AppendLine("<div class='legend-item'><div class='legend-box' style='background: #3d1e1e; border-left: 3px solid #f48771;'></div><span class='legend-text'>Missing (ground truth only)</span></div>");
            html.AppendLine("<div class='legend-item'><div class='legend-box' style='background: #3d2a1e; border-left: 3px solid #ce9178;'></div><span class='legend-text'>Extra (not in ground truth)</span></div>");
            html.AppendLine("<div class='legend-item'><div class='legend-box' style='background: #1e2a3d; border-left: 3px solid #569cd6;'></div><span class='legend-text'>Changed by LLM</span></div>");
            html.AppendLine("</div>");

            // 10. Three-column comparison
            html.AppendLine("<div class='comparison'>");

            // Column 1: Rule-Based
            html.AppendLine("<div class='column'>");
            html.AppendLine($"<h3>Rule-Based ({ruleBasedFlat.Count} items)</h3>");
            RenderTreeWithGroundTruth(html, ruleBasedRoot, groundTruthLookup, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            html.AppendLine("</div>");

            // Column 2: AI-Refined
            html.AppendLine("<div class='column'>");
            html.AppendLine($"<h3>AI-Refined ({aiRefinedFlat.Count} items)</h3>");
            RenderTreeWithGroundTruth(html, aiRefinedRoot, groundTruthLookup, llmChangedNames);
            html.AppendLine("</div>");

            // Column 3: Ground Truth
            html.AppendLine("<div class='column'>");
            html.AppendLine($"<h3>Ground Truth ({groundTruthFlat.Count} items)</h3>");
            RenderTreeWithGroundTruth(html, groundTruthRoot, groundTruthLookup, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            html.AppendLine("</div>");

            html.AppendLine("</div>");

            logger.LogInformation("Three-way comparison completed: Rule-Based={RuleAccuracy:P1}, AI-Refined={AiAccuracy:P1}, Improvement={Improvement:+0.0%;-0.0%;0.0%}",
                ruleBasedAccuracy.Accuracy, aiRefinedAccuracy.Accuracy, improvement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating three-way hierarchy comparison");
            html.AppendLine($"<div style='color: #f48771;'>");
            html.AppendLine($"<h2>Error</h2>");
            html.AppendLine($"<p>{ex.Message}</p>");
            html.AppendLine($"<pre>{ex.StackTrace}</pre>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</div></body></html>");

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html.ToString());
    }

    /// <summary>
    /// Renders hierarchy tree with color coding based on comparison results
    /// </summary>
    private static void RenderTree(
        System.Text.StringBuilder html,
        HierarchyItem item,
        HashSet<string> missingNames,
        HashSet<string> matchedNames,
        HashSet<string> levelMismatchNames,
        HashSet<string> extraNames,
        bool isGroundTruth)
    {
        if (item.Level == 0)
        {
            // Skip root, render children
            foreach (var child in item.SubItems)
            {
                RenderTree(html, child, missingNames, matchedNames, levelMismatchNames, extraNames, isGroundTruth);
            }
            return;
        }

        var name = item.LinkName.ToLowerInvariant();
        string cssClass;

        if (isGroundTruth)
        {
            // Ground truth perspective
            if (matchedNames.Contains(name) && !levelMismatchNames.Contains(name))
            {
                cssClass = "match";
            }
            else if (levelMismatchNames.Contains(name))
            {
                cssClass = "level-mismatch";
            }
            else if (missingNames.Contains(name))
            {
                cssClass = "missing";
            }
            else
            {
                cssClass = "";
            }
        }
        else
        {
            // Generated perspective
            if (matchedNames.Contains(name) && !levelMismatchNames.Contains(name))
            {
                cssClass = "match";
            }
            else if (levelMismatchNames.Contains(name))
            {
                cssClass = "level-mismatch";
            }
            else if (extraNames.Contains(name))
            {
                cssClass = "extra";
            }
            else
            {
                cssClass = "";
            }
        }

        html.AppendLine($"<div class='tree-item level-{item.Level} {cssClass}'>");
        html.Append(item.LinkName);
        html.AppendLine("</div>");

        // Render children
        foreach (var child in item.SubItems)
        {
            RenderTree(html, child, missingNames, matchedNames, levelMismatchNames, extraNames, isGroundTruth);
        }
    }

    /// <summary>
    /// Flattens hierarchy into a list for counting
    /// </summary>
    private static List<HierarchyItem> FlattenHierarchy(HierarchyItem root)
    {
        var result = new List<HierarchyItem>();

        void Traverse(HierarchyItem item)
        {
            result.Add(item);
            foreach (var child in item.SubItems)
            {
                Traverse(child);
            }
        }

        Traverse(root);
        return result;
    }

    /// <summary>
    /// Deep copy hierarchy to create independent versions
    /// </summary>
    private static HierarchyItem DeepCopyHierarchy(HierarchyItem source)
    {
        var copy = new HierarchyItem
        {
            Id = source.Id,
            Level = source.Level,
            LinkName = source.LinkName,
            DataRef = source.DataRef,
            Path = source.Path,
            Confidence = source.Confidence,
            Reasoning = source.Reasoning,
            IsUncertain = source.IsUncertain,
            SubItems = new List<HierarchyItem>()
        };

        foreach (var child in source.SubItems)
        {
            copy.SubItems.Add(DeepCopyHierarchy(child));
        }

        return copy;
    }

    /// <summary>
    /// Apply LLM refinements to uncertain items in the hierarchy
    /// </summary>
    private static async Task<HierarchyItem> ApplyLlmRefinementsAsync(
        HierarchyItem root,
        IOllamaService ollamaService,
        ILogger logger)
    {
        logger.LogInformation("[ThreeWayComparison] Starting LLM refinement process");

        // Check Ollama health
        var isHealthy = await ollamaService.CheckHealthAsync();
        if (!isHealthy)
        {
            logger.LogWarning("[ThreeWayComparison] Ollama not available, returning unmodified hierarchy");
            return root;
        }

        // Flatten hierarchy to find uncertain items (confidence < 90)
        var allItems = FlattenHierarchy(root);
        var uncertainItems = allItems.Where(i => i.Level > 0 && (i.Confidence ?? 100) < 90).ToList();

        logger.LogInformation("[ThreeWayComparison] Found {Count} uncertain items (confidence < 90)", uncertainItems.Count);

        int changesApplied = 0;

        foreach (var item in uncertainItems)
        {
            try
            {
                // Build critique prompt
                var prompt = BuildCritiquePromptForItem(item, allItems);

                // Call LLM (mistral:latest for best balance)
                var response = await ollamaService.GenerateAsync(
                    model: "mistral:latest",
                    prompt: prompt,
                    temperature: 0.1 // Low temperature for consistency
                );

                // Parse response
                var critique = ParseCritiqueResponse(response);
                if (critique == null)
                {
                    logger.LogWarning("[ThreeWayComparison] Failed to parse critique for '{Item}'", item.LinkName);
                    continue;
                }

                // Apply recommendation
                bool changed = ApplyCritiqueRecommendation(item, critique, root, logger);
                if (changed)
                {
                    changesApplied++;
                    logger.LogInformation("[ThreeWayComparison] Applied {Recommendation} to '{Item}': Level {OldLevel} -> {NewLevel}",
                        critique.Recommendation, item.LinkName, item.Level,
                        critique.Recommendation == "B" ? item.Level - 1 :
                        critique.Recommendation == "C" ? item.Level + 1 : item.Level);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[ThreeWayComparison] Error processing item '{Item}'", item.LinkName);
            }
        }

        logger.LogInformation("[ThreeWayComparison] LLM refinement complete: {Changes} changes applied", changesApplied);

        return root;
    }

    /// <summary>
    /// Build critique prompt for a specific uncertain item
    /// </summary>
    private static string BuildCritiquePromptForItem(HierarchyItem item, List<HierarchyItem> allItems)
    {
        // Find adjacent items for context
        var itemIndex = allItems.IndexOf(item);
        var previousItem = itemIndex > 0 ? allItems[itemIndex - 1] : null;
        var nextItem = itemIndex < allItems.Count - 1 ? allItems[itemIndex + 1] : null;

        var decision = new UncertainDecision
        {
            HeaderText = item.LinkName,
            CurrentLevel = item.Level,
            CurrentParent = FindParentName(item, allItems),
            DataNumber = null, // We don't have data-number here
            WordCount = 500, // Placeholder
            ChildHeaderCount = item.SubItems.Count,
            PreviousHeader = previousItem?.LinkName,
            PreviousSiblingLevel = previousItem?.Level ?? 0,
            NextHeader = nextItem?.LinkName,
            NextSiblingLevel = nextItem?.Level ?? 0,
            UncertaintyReason = item.Reasoning ?? "Confidence below 90%"
        };

        return BuildCritiquePrompt(decision);
    }

    /// <summary>
    /// Find parent name for an item
    /// </summary>
    private static string FindParentName(HierarchyItem item, List<HierarchyItem> allItems)
    {
        var itemIndex = allItems.IndexOf(item);
        for (int i = itemIndex - 1; i >= 0; i--)
        {
            if (allItems[i].Level < item.Level)
            {
                return allItems[i].LinkName;
            }
        }
        return "Report Root";
    }

    /// <summary>
    /// Apply critique recommendation to modify hierarchy
    /// </summary>
    private static bool ApplyCritiqueRecommendation(
        HierarchyItem item,
        CritiqueResponse critique,
        HierarchyItem root,
        ILogger logger)
    {
        switch (critique.Recommendation)
        {
            case "A":
                // Keep as-is - no change
                return false;

            case "B":
                // Promote (decrease level by 1)
                if (item.Level > 1)
                {
                    item.Level--;
                    return true;
                }
                return false;

            case "C":
                // Demote (increase level by 1)
                if (item.Level < 4)
                {
                    item.Level++;
                    return true;
                }
                return false;

            case "D":
                // Merge with adjacent - mark for removal
                // For simplicity, we'll just demote it (make it a subsection)
                if (item.Level < 4)
                {
                    item.Level++;
                    return true;
                }
                return false;

            default:
                logger.LogWarning("[ThreeWayComparison] Unknown recommendation: {Rec}", critique.Recommendation);
                return false;
        }
    }

    /// <summary>
    /// Calculate accuracy metrics by comparing to ground truth
    /// </summary>
    private static AccuracyMetrics CalculateAccuracy(
        List<HierarchyItem> generated,
        List<HierarchyItem> groundTruth)
    {
        // Build lookup by lowercase name (handle duplicates by taking first occurrence)
        var groundTruthLookup = new Dictionary<string, HierarchyItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in groundTruth)
        {
            var key = item.LinkName.ToLowerInvariant();
            if (!groundTruthLookup.ContainsKey(key))
            {
                groundTruthLookup[key] = item;
            }
        }

        int correctCount = 0;
        int levelMismatchCount = 0;
        int extraCount = 0;

        foreach (var genItem in generated)
        {
            if (groundTruthLookup.TryGetValue(genItem.LinkName.ToLowerInvariant(), out var truthItem))
            {
                if (genItem.Level == truthItem.Level)
                {
                    correctCount++;
                }
                else
                {
                    levelMismatchCount++;
                }
            }
            else
            {
                extraCount++;
            }
        }

        // Build lookup for generated items (handle duplicates)
        var generatedLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in generated)
        {
            generatedLookup.Add(item.LinkName.ToLowerInvariant());
        }

        int missingCount = 0;
        var processedTruthItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var truthItem in groundTruth)
        {
            var key = truthItem.LinkName.ToLowerInvariant();
            if (!processedTruthItems.Contains(key)) // Skip duplicates in ground truth
            {
                processedTruthItems.Add(key);
                if (!generatedLookup.Contains(key))
                {
                    missingCount++;
                }
            }
        }

        // Use unique items count for accuracy (exclude duplicates)
        var totalItems = processedTruthItems.Count;
        var accuracy = totalItems > 0 ? (double)correctCount / totalItems : 0;

        return new AccuracyMetrics
        {
            CorrectCount = correctCount,
            LevelMismatchCount = levelMismatchCount,
            MissingCount = missingCount,
            ExtraCount = extraCount,
            Accuracy = accuracy
        };
    }

    /// <summary>
    /// Collect items that were changed by LLM
    /// </summary>
    private static HashSet<string> CollectLlmChangedItems(
        List<HierarchyItem> ruleBasedFlat,
        List<HierarchyItem> aiRefinedFlat)
    {
        var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build lookup handling duplicates
        var ruleBasedLookup = new Dictionary<string, HierarchyItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ruleBasedFlat)
        {
            var key = item.LinkName.ToLowerInvariant();
            if (!ruleBasedLookup.ContainsKey(key))
            {
                ruleBasedLookup[key] = item;
            }
        }

        foreach (var aiItem in aiRefinedFlat)
        {
            if (ruleBasedLookup.TryGetValue(aiItem.LinkName.ToLowerInvariant(), out var ruleItem))
            {
                if (aiItem.Level != ruleItem.Level)
                {
                    changed.Add(aiItem.LinkName);
                }
            }
        }

        return changed;
    }

    /// <summary>
    /// Build lookup dictionary for ground truth items
    /// </summary>
    private static Dictionary<string, (int Level, bool Exists)> BuildLookup(List<HierarchyItem> items)
    {
        // Handle duplicates by taking first occurrence
        var lookup = new Dictionary<string, (int Level, bool Exists)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var key = item.LinkName.ToLowerInvariant();
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = (Level: item.Level, Exists: true);
            }
        }
        return lookup;
    }

    /// <summary>
    /// Render tree with ground truth comparison
    /// </summary>
    private static void RenderTreeWithGroundTruth(
        System.Text.StringBuilder html,
        HierarchyItem item,
        Dictionary<string, (int Level, bool Exists)> groundTruthLookup,
        HashSet<string> llmChangedNames)
    {
        if (item.Level == 0)
        {
            // Skip root, render children
            foreach (var child in item.SubItems)
            {
                RenderTreeWithGroundTruth(html, child, groundTruthLookup, llmChangedNames);
            }
            return;
        }

        var name = item.LinkName.ToLowerInvariant();
        string cssClass = "";
        string changeIndicator = "";

        // Check if LLM changed this item
        bool isLlmChanged = llmChangedNames.Contains(item.LinkName);

        // Determine color based on ground truth comparison
        if (groundTruthLookup.TryGetValue(name, out var truthInfo))
        {
            if (item.Level == truthInfo.Level)
            {
                cssClass = isLlmChanged ? "llm-changed match" : "match";
            }
            else
            {
                cssClass = isLlmChanged ? "llm-changed level-mismatch" : "level-mismatch";
            }

            if (isLlmChanged)
            {
                changeIndicator = $" <span class='change-indicator'>[LLM: L{item.Level}]</span>";
            }
        }
        else
        {
            // Not in ground truth - extra item
            cssClass = isLlmChanged ? "llm-changed extra" : "extra";
            if (isLlmChanged)
            {
                changeIndicator = $" <span class='change-indicator'>[LLM: L{item.Level}]</span>";
            }
        }

        html.AppendLine($"<div class='tree-item level-{item.Level} {cssClass}'>");
        html.Append(item.LinkName);
        html.Append(changeIndicator);
        html.AppendLine("</div>");

        // Render children
        foreach (var child in item.SubItems)
        {
            RenderTreeWithGroundTruth(html, child, groundTruthLookup, llmChangedNames);
        }
    }

    /// <summary>
    /// Helper class for accuracy metrics
    /// </summary>
    private class AccuracyMetrics
    {
        public int CorrectCount { get; set; }
        public int LevelMismatchCount { get; set; }
        public int MissingCount { get; set; }
        public int ExtraCount { get; set; }
        public double Accuracy { get; set; }
    }

    /// <summary>
    /// Inspects training data hierarchies from data/training-material/hierarchies/
    ///
    /// Usage:
    ///   curl http://localhost:8085/sandbox?mode=inspect-training-data
    ///   curl http://localhost:8085/sandbox?mode=inspect
    ///
    /// Scans all XML files in training directory and extracts:
    /// - File name and root item
    /// - Total item count
    /// - Level distribution (count per level)
    /// - Sample items from each level
    /// - Data quality indicators (TOC attributes, patterns)
    /// </summary>
    private static async Task HandleInspectTrainingDataAsync(
        HttpContext context,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Inspecting training data hierarchies");

            var trainingDir = Path.Combine("data", "training-material", "hierarchies");

            if (!Directory.Exists(trainingDir))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"Training directory not found: {trainingDir}");
                return;
            }

            // Find all XML files
            var xmlFiles = Directory.GetFiles(trainingDir, "*.xml", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

            logger.LogInformation("[Sandbox] Found {Count} XML files in training directory", xmlFiles.Count);

            // Parse all hierarchies
            var hierarchyInfos = new List<HierarchyFileInfo>();
            var totalItems = 0;
            var failedFiles = new List<(string Path, string Error)>();

            foreach (var filePath in xmlFiles)
            {
                try
                {
                    var hierarchy = await hierarchyService.LoadHierarchyAsync(filePath);
                    var allItems = hierarchyService.GetAllItems(hierarchy);

                    var info = new HierarchyFileInfo
                    {
                        FilePath = filePath,
                        RelativePath = Path.GetRelativePath(trainingDir, filePath),
                        RootName = hierarchy.Root?.LinkName ?? "(no root)",
                        TotalItems = allItems.Count,
                        Items = allItems
                    };

                    // Calculate level distribution
                    info.LevelDistribution = allItems
                        .GroupBy(item => item.Level)
                        .OrderBy(g => g.Key)
                        .ToDictionary(g => g.Key, g => g.Count());

                    // Count quality indicators
                    info.ItemsWithTocNumber = allItems.Count(item => !string.IsNullOrEmpty(item.TocNumber));
                    info.ItemsWithTocStyle = allItems.Count(item => !string.IsNullOrEmpty(item.TocStyle));
                    info.ItemsWithTocStart = allItems.Count(item => item.TocStart);
                    info.ItemsWithTocEnd = allItems.Count(item => item.TocEnd);
                    info.ItemsWithHeaderType = allItems.Count(item => !string.IsNullOrEmpty(item.HeaderType));

                    // Get sample items per level (first 3 from each level)
                    info.SamplesByLevel = allItems
                        .GroupBy(item => item.Level)
                        .OrderBy(g => g.Key)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Take(3).Select(item => item.LinkName).ToList()
                        );

                    hierarchyInfos.Add(info);
                    totalItems += allItems.Count;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Sandbox] Failed to parse {FilePath}", filePath);
                    failedFiles.Add((Path.GetRelativePath(trainingDir, filePath), ex.Message));
                }
            }

            // Generate HTML report
            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='en'>");
            html.AppendLine("<head>");
            html.AppendLine("  <meta charset='utf-8'>");
            html.AppendLine("  <meta name='viewport' content='width=device-width, initial-scale=1'>");
            html.AppendLine("  <title>Training Data Inspection</title>");
            html.AppendLine("  <style>");
            html.AppendLine(@"
    body {
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        background-color: #1e1e1e;
        color: #d4d4d4;
        padding: 20px;
        margin: 0;
    }
    .container {
        max-width: 1400px;
        margin: 0 auto;
    }
    h1 {
        color: #569cd6;
        border-bottom: 2px solid #569cd6;
        padding-bottom: 10px;
    }
    h2 {
        color: #4ec9b0;
        margin-top: 30px;
    }
    .summary {
        background-color: #252526;
        border-left: 4px solid #569cd6;
        padding: 20px;
        margin: 20px 0;
        border-radius: 4px;
    }
    .summary-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
        gap: 15px;
        margin-top: 15px;
    }
    .stat {
        background-color: #2d2d30;
        padding: 12px;
        border-radius: 4px;
        border-left: 3px solid #4ec9b0;
    }
    .stat-label {
        color: #858585;
        font-size: 0.9em;
        margin-bottom: 5px;
    }
    .stat-value {
        color: #d4d4d4;
        font-size: 1.5em;
        font-weight: bold;
    }
    table {
        width: 100%;
        border-collapse: collapse;
        margin: 20px 0;
        background-color: #252526;
    }
    th {
        background-color: #2d2d30;
        color: #569cd6;
        padding: 12px;
        text-align: left;
        font-weight: 600;
        border-bottom: 2px solid #569cd6;
    }
    td {
        padding: 10px 12px;
        border-bottom: 1px solid #3e3e42;
    }
    tr:hover {
        background-color: #2d2d30;
    }
    .file-path {
        color: #ce9178;
        font-family: 'Consolas', 'Monaco', monospace;
        font-size: 0.9em;
    }
    .expandable {
        cursor: pointer;
        color: #4fc1ff;
        text-decoration: underline;
    }
    .expandable:hover {
        color: #569cd6;
    }
    .details {
        display: none;
        margin-top: 10px;
        padding: 12px;
        background-color: #1e1e1e;
        border-left: 3px solid #4ec9b0;
        font-family: 'Consolas', 'Monaco', monospace;
        font-size: 0.85em;
    }
    .details.show {
        display: block;
    }
    .level-badge {
        display: inline-block;
        padding: 2px 8px;
        margin: 2px;
        background-color: #3e3e42;
        border-radius: 3px;
        font-size: 0.85em;
        color: #d7ba7d;
    }
    .sample-item {
        color: #9cdcfe;
        margin: 3px 0;
        padding-left: 10px;
    }
    .quality-indicator {
        display: inline-block;
        margin: 2px 5px;
        padding: 3px 8px;
        background-color: #2d2d30;
        border-radius: 3px;
        font-size: 0.85em;
    }
    .quality-indicator.present {
        color: #4ec9b0;
        border-left: 3px solid #4ec9b0;
    }
    .quality-indicator.absent {
        color: #858585;
        border-left: 3px solid #3e3e42;
    }
    .error-section {
        background-color: #3c1f1e;
        border-left: 4px solid #f48771;
        padding: 15px;
        margin: 20px 0;
        border-radius: 4px;
    }
    .error-file {
        color: #ce9178;
        font-family: 'Consolas', 'Monaco', monospace;
        margin: 5px 0;
    }
    .error-message {
        color: #f48771;
        font-size: 0.9em;
        margin-left: 20px;
    }
            ");
            html.AppendLine("  </style>");
            html.AppendLine("  <script>");
            html.AppendLine(@"
    function toggleDetails(id) {
        const element = document.getElementById(id);
        element.classList.toggle('show');
    }
            ");
            html.AppendLine("  </script>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("  <div class='container'>");
            html.AppendLine("    <h1>Training Data Inspection</h1>");

            // Summary section
            html.AppendLine("    <div class='summary'>");
            html.AppendLine("      <h2 style='margin-top:0'>Summary Statistics</h2>");
            html.AppendLine("      <div class='summary-grid'>");
            html.AppendLine($"        <div class='stat'><div class='stat-label'>Total Files</div><div class='stat-value'>{hierarchyInfos.Count}</div></div>");
            html.AppendLine($"        <div class='stat'><div class='stat-label'>Total Items</div><div class='stat-value'>{totalItems:N0}</div></div>");
            html.AppendLine($"        <div class='stat'><div class='stat-label'>Avg Items/File</div><div class='stat-value'>{(hierarchyInfos.Any() ? totalItems / hierarchyInfos.Count : 0):N1}</div></div>");
            html.AppendLine($"        <div class='stat'><div class='stat-label'>Failed Files</div><div class='stat-value'>{failedFiles.Count}</div></div>");
            html.AppendLine("      </div>");
            html.AppendLine("    </div>");

            // Failed files section
            if (failedFiles.Any())
            {
                html.AppendLine("    <div class='error-section'>");
                html.AppendLine("      <h2 style='margin-top:0; color:#f48771'>Failed Files</h2>");
                foreach (var (path, error) in failedFiles)
                {
                    html.AppendLine($"      <div class='error-file'>{path}</div>");
                    html.AppendLine($"      <div class='error-message'>{System.Web.HttpUtility.HtmlEncode(error)}</div>");
                }
                html.AppendLine("    </div>");
            }

            // Main table
            html.AppendLine("    <h2>Hierarchy Files</h2>");
            html.AppendLine("    <table>");
            html.AppendLine("      <thead>");
            html.AppendLine("        <tr>");
            html.AppendLine("          <th>File</th>");
            html.AppendLine("          <th>Root</th>");
            html.AppendLine("          <th>Items</th>");
            html.AppendLine("          <th>Levels</th>");
            html.AppendLine("          <th>Quality</th>");
            html.AppendLine("          <th>Details</th>");
            html.AppendLine("        </tr>");
            html.AppendLine("      </thead>");
            html.AppendLine("      <tbody>");

            var counter = 0;
            foreach (var info in hierarchyInfos)
            {
                var detailsId = $"details-{counter++}";

                html.AppendLine("        <tr>");
                html.AppendLine($"          <td><div class='file-path'>{System.Web.HttpUtility.HtmlEncode(info.RelativePath)}</div></td>");
                html.AppendLine($"          <td>{System.Web.HttpUtility.HtmlEncode(info.RootName)}</td>");
                html.AppendLine($"          <td>{info.TotalItems}</td>");

                // Level distribution
                html.AppendLine("          <td>");
                foreach (var (level, count) in info.LevelDistribution)
                {
                    html.AppendLine($"            <span class='level-badge'>L{level}: {count}</span>");
                }
                html.AppendLine("          </td>");

                // Quality indicators
                html.AppendLine("          <td>");
                html.AppendLine($"            <span class='quality-indicator {(info.ItemsWithTocNumber > 0 ? "present" : "absent")}'>TOC#: {info.ItemsWithTocNumber}</span>");
                html.AppendLine($"            <span class='quality-indicator {(info.ItemsWithTocStyle > 0 ? "present" : "absent")}'>Style: {info.ItemsWithTocStyle}</span>");
                html.AppendLine($"            <span class='quality-indicator {(info.ItemsWithHeaderType > 0 ? "present" : "absent")}'>Header: {info.ItemsWithHeaderType}</span>");
                html.AppendLine("          </td>");

                // Details toggle
                html.AppendLine($"          <td><span class='expandable' onclick='toggleDetails(\"{detailsId}\")'>Show samples</span></td>");
                html.AppendLine("        </tr>");

                // Details row
                html.AppendLine("        <tr>");
                html.AppendLine("          <td colspan='6'>");
                html.AppendLine($"            <div id='{detailsId}' class='details'>");
                html.AppendLine("              <strong>Sample Items by Level:</strong><br>");

                foreach (var (level, samples) in info.SamplesByLevel)
                {
                    html.AppendLine($"              <br><span class='level-badge'>Level {level}</span>");
                    foreach (var sample in samples)
                    {
                        html.AppendLine($"              <div class='sample-item'>• {System.Web.HttpUtility.HtmlEncode(sample)}</div>");
                    }
                }

                html.AppendLine("              <br><strong>TOC Markers:</strong><br>");
                html.AppendLine($"              TOC Start: {info.ItemsWithTocStart}, TOC End: {info.ItemsWithTocEnd}");

                html.AppendLine("            </div>");
                html.AppendLine("          </td>");
                html.AppendLine("        </tr>");
            }

            html.AppendLine("      </tbody>");
            html.AppendLine("    </table>");

            html.AppendLine("  </div>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html.ToString());

            logger.LogInformation("[Sandbox] Training data inspection complete: {SuccessCount} files parsed, {FailedCount} failed",
                hierarchyInfos.Count, failedFiles.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Failed to inspect training data");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Helper class for training data inspection
    /// </summary>
    private class HierarchyFileInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string RootName { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public List<HierarchyItem> Items { get; set; } = new();
        public Dictionary<int, int> LevelDistribution { get; set; } = new();
        public Dictionary<int, List<string>> SamplesByLevel { get; set; } = new();
        public int ItemsWithTocNumber { get; set; }
        public int ItemsWithTocStyle { get; set; }
        public int ItemsWithTocStart { get; set; }
        public int ItemsWithTocEnd { get; set; }
        public int ItemsWithHeaderType { get; set; }
    }

    /// <summary>
    /// Analyzes all training hierarchies and extracts universal patterns.
    /// Generates comprehensive report and saves learned rules to JSON.
    ///
    /// Usage:
    ///   curl http://localhost:8085/sandbox?mode=analyze-training-hierarchies
    ///   curl http://localhost:8085/sandbox?mode=analyze
    /// </summary>
    private static async Task HandleAnalyzeTrainingHierarchiesAsync(HttpContext context, IHierarchyService hierarchyService, ILogger logger)
    {
        try
        {
            logger.LogInformation("[Sandbox] Starting training hierarchy pattern analysis");

            // Get logger factory to create typed logger for PatternLearningService
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var typedLogger = loggerFactory.CreateLogger<PatternLearningService>();

            // Create pattern learning service
            var learningService = new PatternLearningService(typedLogger, hierarchyService);

            // Analyze training hierarchies
            var trainingDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "training-material", "hierarchies");

            if (!Directory.Exists(trainingDir))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"Training directory not found: {trainingDir}");
                return;
            }

            var database = await learningService.AnalyzeTrainingHierarchies(trainingDir);

            // Save to JSON
            var patternsDir = Path.Combine(Directory.GetCurrentDirectory(), "data", "patterns");
            Directory.CreateDirectory(patternsDir);

            var jsonPath = Path.Combine(patternsDir, "learned-rules.json");
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            var jsonContent = System.Text.Json.JsonSerializer.Serialize(database, jsonOptions);
            await File.WriteAllTextAsync(jsonPath, jsonContent);

            logger.LogInformation("[Sandbox] Saved pattern database to {Path}", jsonPath);

            // Generate HTML report
            var html = GeneratePatternAnalysisReport(database);

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);

            logger.LogInformation("[Sandbox] Pattern analysis complete: {TotalHierarchies} hierarchies, {TotalItems} items analyzed",
                database.TotalHierarchiesAnalyzed, database.TotalItemsAnalyzed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Sandbox] Failed to analyze training hierarchies");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static string GeneratePatternAnalysisReport(PatternDatabase database)
    {
        var html = new System.Text.StringBuilder();

        // VS Code Dark Modern color palette
        var bgPrimary = "#1e1e1e";
        var bgSecondary = "#252526";
        var bgTertiary = "#2d2d30";
        var textPrimary = "#cccccc";
        var textSecondary = "#858585";
        var accentBlue = "#007acc";
        var accentGreen = "#4ec9b0";
        var accentYellow = "#dcdcaa";
        var borderColor = "#3e3e42";

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='en'>");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset='UTF-8'>");
        html.AppendLine("  <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        html.AppendLine("  <title>Training Hierarchy Pattern Analysis</title>");
        html.AppendLine("  <style>");
        html.AppendLine($@"
    * {{
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }}

    body {{
      font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
      background-color: {bgPrimary};
      color: {textPrimary};
      padding: 20px;
      line-height: 1.6;
    }}

    .container {{
      max-width: 1400px;
      margin: 0 auto;
    }}

    h1 {{
      color: {accentBlue};
      margin-bottom: 10px;
      font-size: 28px;
    }}

    h2 {{
      color: {accentGreen};
      margin-top: 30px;
      margin-bottom: 15px;
      font-size: 22px;
      border-bottom: 2px solid {borderColor};
      padding-bottom: 8px;
    }}

    h3 {{
      color: {accentYellow};
      margin-top: 20px;
      margin-bottom: 10px;
      font-size: 18px;
    }}

    .summary-box {{
      background-color: {bgSecondary};
      border: 1px solid {borderColor};
      border-radius: 6px;
      padding: 20px;
      margin-bottom: 30px;
    }}

    .summary-stat {{
      display: inline-block;
      margin-right: 30px;
      margin-bottom: 10px;
    }}

    .summary-stat .label {{
      color: {textSecondary};
      font-size: 14px;
      display: block;
    }}

    .summary-stat .value {{
      color: {accentBlue};
      font-size: 24px;
      font-weight: bold;
    }}

    table {{
      width: 100%;
      border-collapse: collapse;
      margin-top: 15px;
      margin-bottom: 20px;
      background-color: {bgSecondary};
    }}

    th, td {{
      padding: 12px;
      text-align: left;
      border: 1px solid {borderColor};
    }}

    th {{
      background-color: {bgTertiary};
      color: {accentGreen};
      font-weight: 600;
    }}

    tr:hover {{
      background-color: {bgTertiary};
    }}

    .level-badge {{
      display: inline-block;
      background-color: {accentBlue};
      color: white;
      padding: 4px 10px;
      border-radius: 4px;
      font-size: 12px;
      margin-right: 8px;
      margin-bottom: 4px;
    }}

    .level-badge.level-1 {{ background-color: #d73a49; }}
    .level-badge.level-2 {{ background-color: #f97583; }}
    .level-badge.level-3 {{ background-color: {accentBlue}; }}
    .level-badge.level-4 {{ background-color: {accentGreen}; }}
    .level-badge.level-5 {{ background-color: {accentYellow}; color: {bgPrimary}; }}

    .confidence {{
      display: inline-block;
      padding: 2px 8px;
      border-radius: 3px;
      font-size: 11px;
      font-weight: bold;
    }}

    .confidence.high {{ background-color: #4ec9b0; color: {bgPrimary}; }}
    .confidence.medium {{ background-color: #dcdcaa; color: {bgPrimary}; }}
    .confidence.low {{ background-color: #f97583; color: white; }}

    .metric {{
      display: inline-block;
      color: {accentYellow};
      font-weight: bold;
      margin-right: 15px;
    }}

    .proposed-rules {{
      background-color: {bgTertiary};
      border-left: 4px solid {accentGreen};
      padding: 15px;
      margin-top: 15px;
      font-family: 'Courier New', monospace;
      font-size: 13px;
    }}

    .proposed-rules li {{
      margin-bottom: 8px;
      color: {textPrimary};
    }}

    code {{
      background-color: {bgTertiary};
      padding: 2px 6px;
      border-radius: 3px;
      color: {accentYellow};
      font-family: 'Courier New', monospace;
    }}

    .section {{
      margin-bottom: 40px;
    }}

    .scrollable {{
      max-height: 500px;
      overflow-y: auto;
    }}

    .scrollable::-webkit-scrollbar {{
      width: 12px;
    }}

    .scrollable::-webkit-scrollbar-track {{
      background: {bgSecondary};
    }}

    .scrollable::-webkit-scrollbar-thumb {{
      background: {borderColor};
      border-radius: 6px;
    }}

    .scrollable::-webkit-scrollbar-thumb:hover {{
      background: {textSecondary};
    }}

    .timestamp {{
      color: {textSecondary};
      font-size: 14px;
      margin-bottom: 20px;
    }}
  ");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <div class='container'>");

        // Header
        html.AppendLine("    <h1>Training Hierarchy Pattern Analysis</h1>");
        html.AppendLine($"    <div class='timestamp'>Generated: {database.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC</div>");

        // Summary
        html.AppendLine("    <div class='summary-box'>");
        html.AppendLine("      <div class='summary-stat'>");
        html.AppendLine("        <span class='label'>Hierarchies Analyzed</span>");
        html.AppendLine($"        <span class='value'>{database.TotalHierarchiesAnalyzed}</span>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class='summary-stat'>");
        html.AppendLine("        <span class='label'>Total Items</span>");
        html.AppendLine($"        <span class='value'>{database.TotalItemsAnalyzed}</span>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class='summary-stat'>");
        html.AppendLine("        <span class='label'>Levels Found</span>");
        html.AppendLine($"        <span class='value'>{database.LevelProfiles.Count}</span>");
        html.AppendLine("      </div>");
        html.AppendLine("      <div class='summary-stat'>");
        html.AppendLine("        <span class='label'>Unique Sections</span>");
        html.AppendLine($"        <span class='value'>{database.CommonSections.Count}</span>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        // Level Profiles
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>Level Profiles</h2>");
        html.AppendLine("      <p>Characteristics of each hierarchy level across all training data:</p>");
        html.AppendLine("      <div class='scrollable'>");
        html.AppendLine("        <table>");
        html.AppendLine("          <thead>");
        html.AppendLine("            <tr>");
        html.AppendLine("              <th>Level</th>");
        html.AppendLine("              <th>Occurrences</th>");
        html.AppendLine("              <th>Word Count (Avg/Med/Min-Max)</th>");
        html.AppendLine("              <th>Children (Avg/Med/Max)</th>");
        html.AppendLine("              <th>Avg Siblings</th>");
        html.AppendLine("              <th>Top Headers</th>");
        html.AppendLine("            </tr>");
        html.AppendLine("          </thead>");
        html.AppendLine("          <tbody>");

        foreach (var (level, profile) in database.LevelProfiles.OrderBy(x => x.Key))
        {
            var levelClass = $"level-{Math.Min(level, 5)}";
            var topHeaders = string.Join(", ", profile.CommonHeaders.Take(3).Select(h => $"{h.HeaderText} ({h.Occurrences})"));

            html.AppendLine("            <tr>");
            html.AppendLine($"              <td><span class='level-badge {levelClass}'>Level {level}</span></td>");
            html.AppendLine($"              <td>{profile.TotalOccurrences}</td>");
            html.AppendLine($"              <td><span class='metric'>{profile.AvgWordCount:F1}</span> / {profile.MedianWordCount:F1} / {profile.MinWordCount}-{profile.MaxWordCount}</td>");
            html.AppendLine($"              <td><span class='metric'>{profile.AvgChildCount:F1}</span> / {profile.MedianChildCount:F1} / {profile.MaxChildCount}</td>");
            html.AppendLine($"              <td>{profile.AvgSiblingCount:F1}</td>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(topHeaders)}</td>");
            html.AppendLine("            </tr>");
        }

        html.AppendLine("          </tbody>");
        html.AppendLine("        </table>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        // Section Vocabulary
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>Section Vocabulary</h2>");
        html.AppendLine("      <p>Most common section headers and their typical level placements:</p>");
        html.AppendLine("      <div class='scrollable'>");
        html.AppendLine("        <table>");
        html.AppendLine("          <thead>");
        html.AppendLine("            <tr>");
        html.AppendLine("              <th>Header Text</th>");
        html.AppendLine("              <th>Most Common Level</th>");
        html.AppendLine("              <th>Total Occurrences</th>");
        html.AppendLine("              <th>Confidence</th>");
        html.AppendLine("              <th>Level Distribution</th>");
        html.AppendLine("            </tr>");
        html.AppendLine("          </thead>");
        html.AppendLine("          <tbody>");

        foreach (var section in database.CommonSections.Take(30))
        {
            var confidenceClass = section.Confidence >= 0.8 ? "high" : (section.Confidence >= 0.5 ? "medium" : "low");
            var levelDist = string.Join(" ", section.LevelDistribution.OrderBy(x => x.Key).Select(kvp => $"L{kvp.Key}:{kvp.Value}"));

            html.AppendLine("            <tr>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(section.HeaderText)}</td>");
            html.AppendLine($"              <td><span class='level-badge'>L{section.MostCommonLevel}</span></td>");
            html.AppendLine($"              <td>{section.TotalOccurrences}</td>");
            html.AppendLine($"              <td><span class='confidence {confidenceClass}'>{section.Confidence:P0}</span></td>");
            html.AppendLine($"              <td>{levelDist}</td>");
            html.AppendLine("            </tr>");
        }

        html.AppendLine("          </tbody>");
        html.AppendLine("        </table>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        // Sequence Patterns
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>Sequence Patterns</h2>");
        html.AppendLine("      <p>Common orderings of sections (what typically follows what):</p>");
        html.AppendLine("      <div class='scrollable'>");
        html.AppendLine("        <table>");
        html.AppendLine("          <thead>");
        html.AppendLine("            <tr>");
        html.AppendLine("              <th>Section</th>");
        html.AppendLine("              <th>Typically Follows</th>");
        html.AppendLine("              <th>Occurrences</th>");
        html.AppendLine("              <th>Confidence</th>");
        html.AppendLine("            </tr>");
        html.AppendLine("          </thead>");
        html.AppendLine("          <tbody>");

        foreach (var pattern in database.TypicalSequences.Take(20))
        {
            var confidenceClass = pattern.Confidence >= 0.6 ? "high" : (pattern.Confidence >= 0.3 ? "medium" : "low");

            html.AppendLine("            <tr>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(pattern.SectionName)}</td>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(pattern.TypicallyFollows)}</td>");
            html.AppendLine($"              <td>{pattern.Occurrences}</td>");
            html.AppendLine($"              <td><span class='confidence {confidenceClass}'>{pattern.Confidence:P0}</span></td>");
            html.AppendLine("            </tr>");
        }

        html.AppendLine("          </tbody>");
        html.AppendLine("        </table>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        // Numbering Patterns
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>TOC Numbering Patterns</h2>");
        html.AppendLine("      <p>Numbering styles and their typical level assignments:</p>");
        html.AppendLine("      <table>");
        html.AppendLine("        <thead>");
        html.AppendLine("          <tr>");
        html.AppendLine("            <th>Pattern</th>");
        html.AppendLine("            <th>Most Common Level</th>");
        html.AppendLine("            <th>Occurrences</th>");
        html.AppendLine("            <th>Confidence</th>");
        html.AppendLine("          </tr>");
        html.AppendLine("        </thead>");
        html.AppendLine("        <tbody>");

        foreach (var (patternType, pattern) in database.NumberingPatterns.OrderBy(x => x.Value.MostCommonLevel))
        {
            var confidenceClass = pattern.Confidence >= 0.8 ? "high" : (pattern.Confidence >= 0.5 ? "medium" : "low");

            html.AppendLine("          <tr>");
            html.AppendLine($"            <td><code>{System.Web.HttpUtility.HtmlEncode(pattern.Pattern)}</code></td>");
            html.AppendLine($"            <td><span class='level-badge'>L{pattern.MostCommonLevel}</span></td>");
            html.AppendLine($"            <td>{pattern.Occurrences}</td>");
            html.AppendLine($"            <td><span class='confidence {confidenceClass}'>{pattern.Confidence:P0}</span></td>");
            html.AppendLine("          </tr>");
        }

        html.AppendLine("        </tbody>");
        html.AppendLine("      </table>");
        html.AppendLine("    </div>");

        // Parent-Child Patterns
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>Parent-Child Relationships</h2>");
        html.AppendLine("      <p>Common parent-child section pairings:</p>");
        html.AppendLine("      <div class='scrollable'>");
        html.AppendLine("        <table>");
        html.AppendLine("          <thead>");
        html.AppendLine("            <tr>");
        html.AppendLine("              <th>Parent Header</th>");
        html.AppendLine("              <th>Child Header</th>");
        html.AppendLine("              <th>Occurrences</th>");
        html.AppendLine("              <th>Confidence</th>");
        html.AppendLine("            </tr>");
        html.AppendLine("          </thead>");
        html.AppendLine("          <tbody>");

        foreach (var pattern in database.ParentChildPatterns.Take(20))
        {
            var confidenceClass = pattern.Confidence >= 0.6 ? "high" : (pattern.Confidence >= 0.3 ? "medium" : "low");

            html.AppendLine("            <tr>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(pattern.ParentHeader)}</td>");
            html.AppendLine($"              <td>{System.Web.HttpUtility.HtmlEncode(pattern.ChildHeader)}</td>");
            html.AppendLine($"              <td>{pattern.Occurrences}</td>");
            html.AppendLine($"              <td><span class='confidence {confidenceClass}'>{pattern.Confidence:P0}</span></td>");
            html.AppendLine("            </tr>");
        }

        html.AppendLine("          </tbody>");
        html.AppendLine("        </table>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        // Proposed Rules
        html.AppendLine("    <div class='section'>");
        html.AppendLine("      <h2>Proposed Rules for Rule-Based Generator</h2>");
        html.AppendLine("      <p>Actionable improvements based on pattern analysis:</p>");
        html.AppendLine("      <div class='proposed-rules'>");
        html.AppendLine("        <h3>1. Level Assignment Rules</h3>");
        html.AppendLine("        <ul>");

        foreach (var (level, profile) in database.LevelProfiles.OrderBy(x => x.Key).Take(5))
        {
            html.AppendLine($"          <li>Level {level}: Word count typically {profile.AvgWordCount:F0} words (range: {profile.MinWordCount}-{profile.MaxWordCount}), {profile.AvgChildCount:F1} children</li>");
        }

        html.AppendLine("        </ul>");

        html.AppendLine("        <h3>2. Section Recognition Rules</h3>");
        html.AppendLine("        <ul>");

        foreach (var section in database.CommonSections.Where(s => s.Confidence >= 0.7).Take(10))
        {
            html.AppendLine($"          <li>'{section.HeaderText}' → Level {section.MostCommonLevel} (confidence: {section.Confidence:P0})</li>");
        }

        html.AppendLine("        </ul>");

        html.AppendLine("        <h3>3. Numbering Rules</h3>");
        html.AppendLine("        <ul>");

        foreach (var (_, pattern) in database.NumberingPatterns.Where(p => p.Value.Confidence >= 0.7))
        {
            html.AppendLine($"          <li>Pattern <code>{pattern.Pattern}</code> → Level {pattern.MostCommonLevel} (confidence: {pattern.Confidence:P0})</li>");
        }

        html.AppendLine("        </ul>");

        html.AppendLine("        <h3>4. Sequence Validation Rules</h3>");
        html.AppendLine("        <ul>");

        foreach (var pattern in database.TypicalSequences.Where(p => p.Confidence >= 0.5).Take(5))
        {
            html.AppendLine($"          <li>'{pattern.SectionName}' commonly follows '{pattern.TypicallyFollows}' (confidence: {pattern.Confidence:P0})</li>");
        }

        html.AppendLine("        </ul>");
        html.AppendLine("      </div>");
        html.AppendLine("    </div>");

        html.AppendLine("  </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Collects all items from the hierarchy tree into a flat list (helper method).
    /// </summary>
    private static void CollectAllItems(PdfConversion.Models.HierarchyItem item, List<PdfConversion.Models.HierarchyItem> accumulator)
    {
        accumulator.Add(item);
        foreach (var subItem in item.SubItems)
        {
            CollectAllItems(subItem, accumulator);
        }
    }
}

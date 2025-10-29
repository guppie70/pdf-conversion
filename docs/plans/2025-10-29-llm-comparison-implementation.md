# LLM Comparison Sandbox Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add comparison utility to `/sandbox` endpoint that sends prompts to Claude Sonnet 4 and displays side-by-side results with local LLM responses

**Architecture:** Extend existing SandboxEndpoint.cs with query parameter routing. Main handler delegates to appropriate utility method based on `mode` parameter. New HandleLlmComparisonAsync method calls Anthropic API, saves responses to disk, and returns HTML comparison view.

**Tech Stack:** C# .NET 9, System.Net.Http, System.Text.Json, Docker

---

## Task 1: Refactor Existing Sandbox Endpoint

**Files:**
- Modify: `PdfConversion/Endpoints/SandboxEndpoint.cs`

**Step 1: Read current implementation**

Read the file to understand current structure and method signature.

**Step 2: Rename HandleAsync to HandlePromptGenerationAsync**

Rename the existing main handler method to preserve existing functionality:

```csharp
// Old name: public static async Task HandleAsync(...)
// New name:
public static async Task HandlePromptGenerationAsync(
    HttpContext context,
    IXsltTransformationService xsltService,
    IHierarchyGeneratorService hierarchyService,
    ILogger<Program> logger)
{
    // Keep existing implementation unchanged
    // ...
}
```

**Step 3: Create new HandleAsync router method**

Add new main handler that routes to appropriate utility:

```csharp
public static async Task HandleAsync(
    HttpContext context,
    IXsltTransformationService xsltService,
    IHierarchyGeneratorService hierarchyService,
    ILogger<Program> logger)
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
```

**Step 4: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: "Application started" message with recent timestamp

**Step 5: Commit**

```bash
git add PdfConversion/Endpoints/SandboxEndpoint.cs
git commit -m "Refactor sandbox endpoint to support multiple utilities

Rename HandleAsync to HandlePromptGenerationAsync and add router
to enable multiple development utilities in single endpoint"
```

---

## Task 2: Add Anthropic API Client Method

**Files:**
- Modify: `PdfConversion/Endpoints/SandboxEndpoint.cs`

**Step 1: Add CallAnthropicApiAsync private method**

Add private helper method to handle Anthropic API communication:

```csharp
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

    var jsonContent = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

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
        var responseJson = JsonSerializer.Deserialize<JsonElement>(responseBody);

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
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: "Application started" with recent timestamp

**Step 3: Commit**

```bash
git add PdfConversion/Endpoints/SandboxEndpoint.cs
git commit -m "Add Anthropic API client method

Implement CallAnthropicApiAsync helper with comprehensive error
handling for network errors, timeouts, and HTTP failures"
```

---

## Task 3: Add HTML Generation Method

**Files:**
- Modify: `PdfConversion/Endpoints/SandboxEndpoint.cs`

**Step 1: Add BuildComparisonHtml private method**

Add method to generate HTML output with VS Code Dark Modern styling:

```csharp
private static string BuildComparisonHtml(
    string approachName,
    string localResponse,
    object claudeResponse)
{
    var isError = claudeResponse is not string;
    var claudeContent = isError
        ? JsonSerializer.Serialize(claudeResponse, new JsonSerializerOptions { WriteIndented = true })
        : (string)claudeResponse;

    // Format JSON for display
    string FormatJson(string json)
    {
        try
        {
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(json);
            return JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
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
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: "Application started" with recent timestamp

**Step 3: Commit**

```bash
git add PdfConversion/Endpoints/SandboxEndpoint.cs
git commit -m "Add HTML comparison view generator

Implement BuildComparisonHtml with VS Code Dark Modern styling
and side-by-side JSON display with error highlighting"
```

---

## Task 4: Implement Main Comparison Handler

**Files:**
- Modify: `PdfConversion/Endpoints/SandboxEndpoint.cs`

**Step 1: Add HandleLlmComparisonAsync method**

Add main comparison logic:

```csharp
private static async Task HandleLlmComparisonAsync(
    HttpContext context,
    ILogger logger)
{
    // Check API key
    var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
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
    htmlParts.Add(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>LLM Comparison Results</title>
    <style>
        body {
            background: #1F1F1F;
            color: #CCCCCC;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', system-ui, sans-serif;
            margin: 0;
            padding: 20px;
        }
        h1 {
            color: #FFFFFF;
            font-size: 24px;
            margin-bottom: 32px;
        }
        .approach-section {
            margin-bottom: 60px;
        }
        .approach-header {
            color: #FFFFFF;
            font-size: 18px;
            margin-bottom: 16px;
            padding-bottom: 8px;
            border-bottom: 2px solid #0078D4;
        }
        .comparison-container {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
        }
        .panel {
            background: #181818;
            border: 1px solid #2B2B2B;
            border-radius: 4px;
            overflow: hidden;
        }
        .panel-header {
            background: #1F1F1F;
            color: #FFFFFF;
            padding: 12px 16px;
            border-bottom: 1px solid #2B2B2B;
            font-weight: 600;
        }
        .panel-subheader {
            color: #9D9D9D;
            font-size: 12px;
            font-weight: normal;
            margin-top: 4px;
        }
        .panel-content {
            padding: 16px;
            max-height: 600px;
            overflow-y: auto;
        }
        pre {
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
        }
        .error-panel pre {
            background: #5A1D1D;
            border: 2px solid #F85149;
        }
        .error-header {
            color: #F85149;
            font-weight: bold;
            margin-bottom: 8px;
        }
    </style>
</head>
<body>
    <h1>LLM Comparison Results</h1>
");

    foreach (var approach in approaches)
    {
        logger.LogInformation("Processing approach: {Approach}", approach);

        var basePath = $"/app/data/llm-development/{approach}";

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

        // Call Anthropic API
        logger.LogInformation("Calling Anthropic API for {Approach}", approach);
        var claudeResponse = await CallAnthropicApiAsync(promptText, apiKey, approach, logger);

        // Save response
        var isError = claudeResponse is not string;
        var outputPath = Path.Combine(basePath,
            isError ? "claude-response-error.json" : "claude-response.json");

        var responseText = claudeResponse is string
            ? (string)claudeResponse
            : JsonSerializer.Serialize(claudeResponse, new JsonSerializerOptions { WriteIndented = true });

        // Try to pretty-print if valid JSON
        if (!isError)
        {
            try
            {
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(responseText);
                responseText = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                // Keep as-is if not valid JSON
            }
        }

        await File.WriteAllTextAsync(outputPath, responseText);
        logger.LogInformation("Saved response to {Path}", outputPath);

        // Format JSON for display
        string FormatJson(string json)
        {
            try
            {
                var jsonObj = JsonSerializer.Deserialize<JsonElement>(json);
                return JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
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

        var errorClass = isError ? "error-panel" : "";
        var errorHeader = isError
            ? "<div class='error-header'>❌ API ERROR</div>"
            : "";

        htmlParts.Add($@"
    <div class='approach-section'>
        <div class='approach-header'>{approach}</div>
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

            <div class='panel {errorClass}'>
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
```

**Step 2: Verify compilation**

```bash
docker logs taxxor-pdfconversion-1 2>&1 | grep -E "(Building|error CS|Application started)" | tail -10
```

Expected: "Application started" with recent timestamp

**Step 3: Commit**

```bash
git add PdfConversion/Endpoints/SandboxEndpoint.cs
git commit -m "Implement LLM comparison handler

Add HandleLlmComparisonAsync that processes all 4 approaches,
calls Anthropic API, saves responses, and generates HTML view"
```

---

## Task 5: Update docker-compose.yml

**Files:**
- Modify: `docker-compose.yml`

**Step 1: Add environment variable to pdfconversion service**

Add ANTHROPIC_API_KEY to the environment section:

```yaml
services:
  pdfconversion:
    image: txdotnetdevelop:net09
    container_name: taxxor-pdfconversion
    ports:
      - "8085:8085"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8085
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
    # ... rest of configuration
```

**Step 2: Commit**

```bash
git add docker-compose.yml
git commit -m "Add ANTHROPIC_API_KEY environment variable support

Enable passing API key from host environment to container
for LLM comparison functionality"
```

---

## Task 6: Manual Testing

**Files:**
- None (testing only)

**Step 1: Set API key and restart**

```bash
export ANTHROPIC_API_KEY="sk-ant-..."
docker-compose restart pdfconversion
```

Wait for container to start (check logs):
```bash
docker logs taxxor-pdfconversion-1 --tail 20
```

Expected: "Application started. Press Ctrl+C to shut down."

**Step 2: Test single approach**

Navigate to: `http://localhost:8085/sandbox?approach=1`

Expected:
- Page loads with comparison view
- Left panel shows local LLM response
- Right panel shows Claude Sonnet response (or error if API key invalid)
- Response saved to `data/llm-development/1-full-generation-approach/claude-response.json`

**Step 3: Verify file saved**

```bash
ls -la data/llm-development/1-full-generation-approach/
```

Expected: `claude-response.json` file exists with recent timestamp

**Step 4: Test all approaches**

Navigate to: `http://localhost:8085/sandbox`

Expected:
- Page shows 4 sections, one per approach
- Each section has side-by-side comparison
- All 4 claude-response.json files created

**Step 5: Test old functionality still works**

Navigate to: `http://localhost:8085/sandbox?mode=prompt-gen`

Expected: Original prompt generation utility still works

**Step 6: Test error handling**

Unset API key and restart:
```bash
unset ANTHROPIC_API_KEY
docker-compose restart pdfconversion
```

Navigate to: `http://localhost:8085/sandbox`

Expected: 400 error with helpful message about missing API key

---

## Task 7: Update Documentation

**Files:**
- Modify: `CLAUDE.md`

**Step 1: Add sandbox testing section**

Add documentation for the new testing workflow:

```markdown
### Sandbox Pattern: LLM Comparison Testing

**Purpose:** Compare Claude Sonnet 4 against local LLM for hierarchy generation prompts.

**URL patterns:**
- `/sandbox` → Run all 4 approaches through Anthropic (default)
- `/sandbox?approach=1` → Run only approach 1
- `/sandbox?mode=prompt-gen` → Old prompt generation utility

**Setup:**
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
docker-compose restart pdfconversion
```

**Output:**
- HTML comparison view with side-by-side results
- Responses saved to `data/llm-development/{approach}/claude-response.json`
- Errors saved to `data/llm-development/{approach}/claude-response-error.json`

**Iteration workflow:**
1. Set API key once
2. Navigate to `/sandbox` or `/sandbox?approach=N`
3. Review comparison in browser
4. Check saved JSON files for detailed analysis
```

**Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "Document LLM comparison sandbox testing

Add usage instructions for Claude Sonnet comparison utility
including setup, URL patterns, and iteration workflow"
```

---

## Verification Checklist

After implementation, verify:

- [ ] `/sandbox` processes all 4 approaches and displays comparison
- [ ] `/sandbox?approach=1` processes single approach
- [ ] `/sandbox?mode=prompt-gen` still works (old functionality)
- [ ] Claude responses saved to `claude-response.json` files
- [ ] API errors saved to `claude-response-error.json` files
- [ ] Missing API key returns helpful error message
- [ ] HTML styling matches VS Code Dark Modern theme
- [ ] Side-by-side comparison is readable and scrollable
- [ ] Logs show processing status for each approach
- [ ] Docker hot-reload works during development

## Success Criteria

Implementation succeeds when:

1. **Functional:**
   - All 4 prompts successfully sent to Claude Sonnet 4
   - Responses persisted to disk
   - HTML comparison displays correctly
   - Error handling works for API failures

2. **Usable:**
   - Simple URL parameters control behavior
   - Clear error messages guide troubleshooting
   - Visual comparison makes it easy to spot differences

3. **Maintainable:**
   - Code follows existing SandboxEndpoint pattern
   - Comprehensive logging for debugging
   - No new dependencies required
   - Old functionality preserved

## Notes

- This is a one-time research utility, not production code
- Keep implementation simple - no need for fancy UI or metrics
- Focus on getting results quickly for decision making
- Can be removed or simplified after comparison study complete

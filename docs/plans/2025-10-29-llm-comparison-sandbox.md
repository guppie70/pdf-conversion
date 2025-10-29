# LLM Comparison Sandbox Design

**Date:** 2025-10-29
**Status:** Approved
**Purpose:** One-time comparison study to test if Claude Sonnet 4 performs better than local LLM for hierarchy generation

## Overview

This design adds a simple comparison utility to the existing `/sandbox` endpoint that:
1. Reads existing prompts from `data/llm-development/` (4 approaches tested with local LLM)
2. Sends each prompt to Anthropic's Claude Sonnet 4 API
3. Saves Claude's responses to disk for later review
4. Displays side-by-side comparison in a simple HTML page

**Key constraint:** This is a one-time research tool, not production infrastructure. Keep it simple.

## Architecture

### Endpoint Structure

**File:** `PdfConversion/Endpoints/SandboxEndpoint.cs`

The sandbox endpoint will become a collection of development utilities using query parameter routing:

```csharp
public static async Task HandleAsync(HttpContext context, ...)
{
    var approach = context.Request.Query["approach"].FirstOrDefault();
    var mode = context.Request.Query["mode"].FirstOrDefault();

    if (mode == "prompt-gen")
    {
        await HandlePromptGenerationAsync(context, ...);  // Renamed from old HandleAsync
    }
    else
    {
        await HandleLlmComparisonAsync(context, approach, ...);  // New functionality
    }
}
```

### URL Patterns

| URL | Behavior |
|-----|----------|
| `/sandbox` | Run all 4 approaches through Anthropic (default) |
| `/sandbox?approach=1` | Run only approach 1 |
| `/sandbox?approach=2` | Run only approach 2 |
| `/sandbox?approach=3` | Run only approach 3 |
| `/sandbox?approach=4` | Run only approach 4 |
| `/sandbox?mode=prompt-gen` | Existing prompt generation utility |

## Anthropic API Integration

### API Configuration

**Environment Variable:**
```yaml
# docker-compose.yml
services:
  pdfconversion:
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
```

Set on host before starting:
```bash
export ANTHROPIC_API_KEY="sk-ant-..."
docker-compose up
```

### API Request

**Endpoint:** `https://api.anthropic.com/v1/messages`
**Model:** `claude-sonnet-4-20250514` (latest Sonnet)

**Headers:**
- `x-api-key: <API_KEY>`
- `anthropic-version: 2023-06-01`
- `content-type: application/json`

**Request body:**
```json
{
    "model": "claude-sonnet-4-20250514",
    "max_tokens": 4096,
    "messages": [
        {
            "role": "user",
            "content": "<content from prompt.txt>"
        }
    ]
}
```

**Response parsing:**
```csharp
// Extract text from response
var claudeText = responseJson["content"][0]["text"].GetString();

// Try to parse as JSON hierarchy
var hierarchyJson = JsonSerializer.Deserialize<JsonElement>(claudeText);
```

## File Structure

### Before Running
```
data/llm-development/
├── 1-full-generation-approach/
│   ├── prompt.txt              (existing - input to LLM)
│   └── response.json           (existing - local LLM output)
├── 2-task-inversion-line-numbers/
│   ├── prompt.txt
│   └── response.json
├── 3-labeled-training-examples/
│   ├── prompt.txt
│   └── response.json
└── 4-context-aware-metadata/
    ├── prompt.txt
    └── response.json
```

### After Running
```
data/llm-development/
├── 1-full-generation-approach/
│   ├── prompt.txt
│   ├── response.json           (local LLM)
│   └── claude-response.json    (NEW - Claude Sonnet 4)
├── 2-task-inversion-line-numbers/
│   ├── prompt.txt
│   ├── response.json
│   └── claude-response.json
└── ... (same pattern)
```

### Error Cases
If API call fails, create error file instead:
```
claude-response-error.json      (contains HTTP status, error message, timestamp)
```

## File I/O Operations

### Reading Prompts and Responses

```csharp
var approaches = new[] {
    "1-full-generation-approach",
    "2-task-inversion-line-numbers",
    "3-labeled-training-examples",
    "4-context-aware-metadata"
};

foreach (var approach in approaches)
{
    var basePath = $"/app/data/llm-development/{approach}";

    // Check directory exists
    if (!Directory.Exists(basePath))
    {
        logger.LogWarning("Skipping {Approach} - directory not found", approach);
        continue;
    }

    // Read existing data
    var promptText = await File.ReadAllTextAsync($"{basePath}/prompt.txt");
    var localResponse = await File.ReadAllTextAsync($"{basePath}/response.json");

    // ... send to Anthropic ...
}
```

### Saving Claude Responses

```csharp
var claudeResponsePath = $"{basePath}/claude-response.json";

try
{
    // Try to parse and pretty-print JSON
    var jsonObj = JsonSerializer.Deserialize<JsonElement>(anthropicResponse);
    var formatted = JsonSerializer.Serialize(jsonObj, new JsonSerializerOptions
    {
        WriteIndented = true
    });
    await File.WriteAllTextAsync(claudeResponsePath, formatted);
}
catch
{
    // If not valid JSON, save raw response
    await File.WriteAllTextAsync(claudeResponsePath, anthropicResponse);
}
```

## HTML Output

### Layout Design

Simple two-column comparison layout using inline CSS (no Razor components):

```
┌─────────────────────────────────────────────────────────────┐
│ LLM Comparison Results                                      │
│ Approach: 1-full-generation-approach                        │
├──────────────────────────────┬──────────────────────────────┤
│ Local LLM                    │ Claude Sonnet 4              │
│ (deepseek-coder:33b)         │ (claude-sonnet-4-20250514)   │
├──────────────────────────────┼──────────────────────────────┤
│ <pre>                        │ <pre>                        │
│ {                            │ {                            │
│   "reasoning": "...",        │   "reasoning": "...",        │
│   "root": {                  │   "root": {                  │
│     "id": "report-root",     │     "id": "report-root",     │
│     "level": 0,              │     "level": 0,              │
│     "subItems": [            │     "subItems": [            │
│       ...                    │       ...                    │
│     ]                        │     ]                        │
│   }                          │   }                          │
│ }                            │ }                            │
│ </pre>                       │ </pre>                       │
└──────────────────────────────┴──────────────────────────────┘

[Repeat for each approach when ?approach=all]
```

### Styling

Use VS Code Dark Modern color palette:
- Background: `#1F1F1F`
- Panel border: `#2B2B2B`
- Text: `#CCCCCC`
- Headers: `#FFFFFF`
- JSON syntax: Consolas monospace font

### JSON Display

```html
<pre style="
    background: #1F1F1F;
    color: #CCCCCC;
    font-family: Consolas, Monaco, 'Courier New', monospace;
    font-size: 12px;
    padding: 16px;
    border: 1px solid #2B2B2B;
    max-height: 600px;
    overflow-y: auto;
    white-space: pre-wrap;
">{JSON_CONTENT}</pre>
```

## Error Handling

### API Call Failures

Capture comprehensive error information:

```csharp
try
{
    var response = await httpClient.PostAsync(anthropicUrl, content);

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

        // Save error to file
        await File.WriteAllTextAsync(
            $"{basePath}/claude-response-error.json",
            JsonSerializer.Serialize(errorInfo, new JsonSerializerOptions { WriteIndented = true })
        );

        logger.LogError(
            "Anthropic API failed for {Approach}: {Status} - {Error}",
            approach,
            response.StatusCode,
            errorBody
        );

        return errorInfo; // Will be displayed in UI
    }
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Network error calling Anthropic API for {Approach}", approach);
    return new { Error = ex.Message, Type = "NetworkError" };
}
catch (TaskCanceledException ex)
{
    logger.LogError(ex, "Timeout calling Anthropic API for {Approach}", approach);
    return new { Error = "Request timeout", Type = "Timeout" };
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error for {Approach}", approach);
    return new { Error = ex.Message, Type = ex.GetType().Name };
}
```

### Error Display in UI

When API call fails, display error in red panel instead of JSON:

```
┌──────────────────────────────┬──────────────────────────────┐
│ Local LLM                    │ Claude Sonnet 4              │
│ ✓ Success                    │ ❌ ERROR                     │
├──────────────────────────────┼──────────────────────────────┤
│ { ... JSON ... }             │ Status: 401 Unauthorized     │
│                              │                              │
│                              │ Error Message:               │
│                              │ Invalid API key              │
│                              │                              │
│                              │ Timestamp:                   │
│                              │ 2025-10-29T10:30:45Z        │
│                              │                              │
│                              │ Troubleshooting:             │
│                              │ - Check ANTHROPIC_API_KEY    │
│                              │   environment variable       │
│                              │ - Verify API key is valid    │
│                              │ - Check error file at:       │
│                              │   data/llm-development/      │
│                              │   {approach}/                │
│                              │   claude-response-error.json │
└──────────────────────────────┴──────────────────────────────┘
```

### Common Error Codes

| Status | Meaning | Likely Cause |
|--------|---------|--------------|
| 401 | Unauthorized | Invalid or missing API key |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Internal Server Error | Anthropic service issue |
| Timeout | Request timeout | Slow network or large prompt |

### Missing API Key Handling

Check API key before any processing:

```csharp
var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    return Results.BadRequest(new
    {
        Error = "ANTHROPIC_API_KEY environment variable not set",
        Help = "Set it in docker-compose.yml or via: export ANTHROPIC_API_KEY='sk-ant-...'"
    });
}
```

## Implementation Notes

### Method Organization

1. **HandleAsync** (main router)
   - Check query parameters
   - Route to appropriate handler

2. **HandlePromptGenerationAsync** (existing utility)
   - Renamed from old HandleAsync
   - Keep existing functionality unchanged

3. **HandleLlmComparisonAsync** (new)
   - Main comparison logic
   - Calls private helper methods

4. **CallAnthropicApiAsync** (private helper)
   - Makes HTTP request to Anthropic
   - Returns response text or error object

5. **BuildComparisonHtml** (private helper)
   - Generates HTML output
   - Handles both success and error cases

### Dependencies

Required NuGet packages (already in project):
- `System.Net.Http` - HTTP client
- `System.Text.Json` - JSON parsing
- `Microsoft.Extensions.Logging` - Logging

No new dependencies needed.

### Testing Workflow

1. Set API key: `export ANTHROPIC_API_KEY="sk-ant-..."`
2. Restart container: `docker-compose restart pdfconversion`
3. Navigate to: `http://localhost:8085/sandbox`
4. Wait for processing (may take 30-60 seconds for all 4 approaches)
5. Review side-by-side comparison
6. Check saved files: `ls data/llm-development/*/claude-response.json`
7. Test single approach: `http://localhost:8085/sandbox?approach=1`

## Success Criteria

This design succeeds if:
1. ✅ All 4 prompts can be sent to Claude Sonnet 4
2. ✅ Claude responses are saved to disk for later review
3. ✅ Simple HTML comparison page shows both results side-by-side
4. ✅ API errors are clearly displayed with troubleshooting guidance
5. ✅ Can run all approaches or individual approach via URL parameter
6. ✅ Results persist for multiple review sessions

## Future Considerations (Out of Scope)

Not included in this one-time utility:
- ❌ Automatic quality metrics or scoring
- ❌ Interactive voting/ranking system
- ❌ Re-running local LLM for comparison
- ❌ Structured diff viewer for hierarchy differences
- ❌ Export to CSV or report format
- ❌ Integration with production hierarchy generation

**Rationale:** This is research infrastructure to answer one question: "Can Claude Sonnet solve this better than local LLM?" Keep it simple and focused.

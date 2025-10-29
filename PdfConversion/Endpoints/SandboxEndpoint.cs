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

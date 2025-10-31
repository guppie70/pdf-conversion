using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PdfConversion.HealthChecks;
using PdfConversion.Services;
using Serilog;
using Serilog.Events;
using System.Text.Json;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "/app/logs/pdfconversion-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    Log.Information("Starting PDF Conversion application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        // Increase max message size to 10MB to support large XML file content in Monaco editor callbacks
        // Default is 32KB which is too small for Source XML editor changes
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
    })
    .AddCircuitOptions(options =>
    {
        // Increase circuit retention for long-running Docling conversions
        // Default is 3 minutes, increase to 15 minutes
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(15);
    });

// Add SignalR for real-time progress updates
builder.Services.AddSignalR();

// Add memory cache for service caching
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB limit
});

// Register configuration settings
builder.Services.Configure<PdfConversion.Models.ConversionSettings>(
    builder.Configuration.GetSection("ConversionSettings"));

// Add distributed cache (Redis) - optional, falls back to memory
var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "PdfConversion:";
    });
}

// Register custom services
builder.Services.AddScoped<IProjectManagementService, ProjectManagementService>();
builder.Services.AddScoped<IFileGroupBuilderService, FileGroupBuilderService>();
builder.Services.AddScoped<IXsltTransformationService, XsltTransformationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IConversionService, ConversionService>();
builder.Services.AddScoped<IHeaderMatchingService, HeaderMatchingService>();
builder.Services.AddScoped<IHeaderNormalizationService, HeaderNormalizationService>();
builder.Services.AddScoped<IContentExtractionService, ContentExtractionService>();
builder.Services.AddScoped<IDocumentReconstructionService, DocumentReconstructionService>();
builder.Services.AddScoped<IDiffService, DiffService>();
builder.Services.AddScoped<IRoundTripValidationService, RoundTripValidationService>();
builder.Services.AddScoped<IBase64ImageExtractor, Base64ImageExtractor>();
builder.Services.AddScoped<IHierarchyService, HierarchyService>();
builder.Services.AddScoped<IHeaderExtractionService, HeaderExtractionService>();
builder.Services.AddScoped<IManualHierarchyBuilder, ManualHierarchyBuilder>();
builder.Services.AddScoped<TransformToolbarState>();
builder.Services.AddSingleton<IXhtmlValidationService, XhtmlValidationService>();
builder.Services.AddSingleton<ITransformationLogService, TransformationLogService>();
builder.Services.AddSingleton<ThemeService>();
builder.Services.AddSingleton<IXsltFileWatcherService, XsltFileWatcherService>();
builder.Services.AddSingleton<IXmlFileWatcherService, XmlFileWatcherService>();
builder.Services.AddSingleton<IUserSelectionService, UserSelectionService>();
builder.Services.AddSingleton<IProjectLabelService, ProjectLabelService>();
builder.Services.AddSingleton<IProjectDirectoryWatcherService, ProjectDirectoryWatcherService>();
builder.Services.AddScoped<IProjectDeletionService, ProjectDeletionService>();

// Register MetadataSyncService (single instance for both hosted service and injection)
builder.Services.AddSingleton<IMetadataSyncService, MetadataSyncService>();
builder.Services.AddHostedService(provider => (MetadataSyncService)provider.GetRequiredService<IMetadataSyncService>());

// Register Docling job polling service (single instance for both hosted service and injection)
builder.Services.AddSingleton<DoclingJobPollingService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<DoclingJobPollingService>());

// Register ProjectMetadataService
var metadataPath = Path.Combine(builder.Environment.ContentRootPath, "data", "project-metadata.json");
builder.Services.AddSingleton(new ProjectMetadataService(metadataPath));

// Register performance optimization services
builder.Services.AddSingleton<IDistributedCacheService, DistributedCacheService>();
builder.Services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
builder.Services.AddSingleton<IMemoryPoolManager, MemoryPoolManager>();
builder.Services.AddScoped<IStreamingXsltTransformationService, StreamingXsltTransformationService>();
builder.Services.AddSingleton<IBatchTransformationService, BatchTransformationService>();

// Register Ollama service for AI hierarchy generation
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<RuleBasedHierarchyGenerator>();
builder.Services.AddScoped<IHierarchyGeneratorService, HierarchyGeneratorService>();
builder.Services.AddSingleton<IHierarchyRequestLogger, HierarchyRequestLogger>();
builder.Services.AddScoped<HierarchyComparisonService>();
builder.Services.AddScoped<PatternLearningService>();
builder.Services.AddScoped<IPatternReindexingService, PatternReindexingService>();

// Configure HttpClient for XSLT3Service
var xslt3ServiceUrl = builder.Configuration.GetValue<string>("XSLT3_SERVICE_URL") ?? "http://xslt3service:4806";

builder.Services.AddHttpClient<IXslt3ServiceClient, Xslt3ServiceClient>(client =>
{
    client.BaseAddress = new Uri(xslt3ServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "PdfConversion/1.0");
});

// Register Xslt3ServiceClient as concrete class for direct injection
builder.Services.AddHttpClient<Xslt3ServiceClient>(client =>
{
    client.BaseAddress = new Uri(xslt3ServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "PdfConversion/1.0");
});

// Configure HttpClient for Docling service
var doclingServiceUrl = builder.Configuration.GetValue<string>("DOCLING_SERVICE_URL") ?? "http://docling-service:4808";

builder.Services.AddHttpClient("DoclingService", client =>
{
    client.BaseAddress = new Uri(doclingServiceUrl);
    client.Timeout = TimeSpan.FromMinutes(15); // Long timeout for large PDFs
    client.DefaultRequestHeaders.Add("User-Agent", "PdfConversion/1.0");
});

// Configure HttpClient for health checks (separate client with shorter timeout)
builder.Services.AddHttpClient("Xslt3ServiceHealthCheck", client =>
{
    client.BaseAddress = new Uri(xslt3ServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("User-Agent", "PdfConversion-HealthCheck/1.0");
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck<Xslt3ServiceHealthCheck>(
        "xslt3service",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready", "dependencies" });

// builder.Services.AddScoped<IFileSystemService, FileSystemService>();
// builder.Services.AddSingleton<ITransformationLogService, TransformationLogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

// Map Blazor endpoints
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Map SignalR hubs
app.MapHub<PdfConversion.Hubs.DoclingProgressHub>("/hubs/docling-progress");

// Map transform-test endpoint for rapid XSLT testing
app.MapGet("/transform-test", async (HttpContext context, IXsltTransformationService xsltService, IUserSelectionService selectionService, ILogger<Program> logger) =>
{
    try
    {
        // Get XSLT file from query string, or use last selected from JSON storage
        var selection = await selectionService.GetSelectionAsync();
        var xsltFile = context.Request.Query["xslt"].FirstOrDefault() ?? selection.LastSelectedXslt ?? "transformation.xslt";

        // Fixed test XML path
        var testXmlPath = "/app/data/input/_test.xml";

        if (!File.Exists(testXmlPath))
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"Test file not found: {testXmlPath}");
            return;
        }

        // Read test XML content
        var xmlContent = await File.ReadAllTextAsync(testXmlPath);

        // Construct full XSLT path
        var xsltPath = Path.Combine("/app/xslt", xsltFile);

        if (!File.Exists(xsltPath))
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"XSLT file not found: {xsltPath}");
            return;
        }

        // Read XSLT content
        var xsltContent = await File.ReadAllTextAsync(xsltPath);

        logger.LogInformation("Transform test: using XSLT {XsltFile}", xsltFile);

        // Get projectid from query string (optional)
        var projectId = context.Request.Query["projectid"].FirstOrDefault();

        // Perform transformation (using XSLT3Service by default)
        var options = new PdfConversion.Models.TransformationOptions
        {
            UseXslt3Service = true
        };

        // Add projectid parameter if provided
        if (!string.IsNullOrEmpty(projectId))
        {
            options.Parameters["projectid"] = projectId;
            logger.LogInformation("Transform test: using projectid={ProjectId}", projectId);
        }

        var result = await xsltService.TransformAsync(xmlContent, xsltContent, options, xsltPath);

        if (!result.IsSuccess)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"Transformation failed: {result.ErrorMessage}");
            return;
        }

        // Return transformed XML
        context.Response.ContentType = "text/xml; charset=utf-8";
        await context.Response.WriteAsync(result.OutputContent ?? "");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in transform-test endpoint");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync($"Error: {ex.Message}");
    }
});

// Map API endpoint to retrieve last AI generation request parameters
app.MapGet("/api/hierarchy/last-request-params", (IHierarchyRequestLogger requestLogger) =>
{
    var lastRequest = requestLogger.GetLastRequest();

    if (lastRequest == null)
    {
        return Results.Ok(new { hasData = false });
    }

    return Results.Ok(new
    {
        hasData = true,
        project = lastRequest.Project,
        sourceXml = lastRequest.SourceXml,
        xslt = lastRequest.Xslt,
        examples = lastRequest.Examples,
        timestamp = lastRequest.Timestamp
    });
});

// Map DELETE endpoint for project deletion
app.MapDelete("/api/projects/{customer}/{projectId}", async (
    string customer,
    string projectId,
    IProjectDeletionService deletionService,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("DELETE request received for project: {Customer}/{ProjectId}", customer, projectId);

        var result = await deletionService.DeleteProjectAsync(customer, projectId);

        if (!result.Success)
        {
            logger.LogError("Project deletion failed: {Message}", result.Message);
            return Results.Json(result, statusCode: 500);
        }

        if (result.HasWarnings)
        {
            logger.LogWarning("Project deletion completed with warnings: {Warnings}", result.Details.GetWarningsSummary());
            return Results.Json(result, statusCode: 200);
        }

        logger.LogInformation("Project deletion completed successfully");
        return Results.Json(result, statusCode: 200);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error in DELETE endpoint for {Customer}/{ProjectId}", customer, projectId);
        return Results.Json(new
        {
            success = false,
            message = $"Internal server error: {ex.Message}"
        }, statusCode: 500);
    }
});

// Map hierarchy-test-api endpoint for rapid prompt testing
// This endpoint allows testing hierarchy generation without UI interaction
// Example: curl "http://localhost:8085/hierarchy-test-api?project=taxxor/projects/ar25-1&sourceXml=docling-output.xml&xslt=docling/transformation.xslt&examples=optiver/projects/ar24-6&model=deepseek-coder:33b"
app.MapGet("/hierarchy-test-api", async (
    HttpContext context,
    IHierarchyGeneratorService hierarchyService,
    IProjectManagementService projectService,
    ILogger<Program> logger) =>
{
    try
    {
        // Parse required parameters
        var project = context.Request.Query["project"].FirstOrDefault();
        var sourceXml = context.Request.Query["sourceXml"].FirstOrDefault();
        var xslt = context.Request.Query["xslt"].FirstOrDefault();
        var examplesParam = context.Request.Query["examples"].FirstOrDefault();
        var model = context.Request.Query["model"].FirstOrDefault() ?? "deepseek-coder:33b";

        // Validate required parameters
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(sourceXml) ||
            string.IsNullOrEmpty(xslt) || string.IsNullOrEmpty(examplesParam))
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "Missing required parameters. Expected: project, sourceXml, xslt, examples",
                example = "curl \"http://localhost:8085/hierarchy-test-api?project=taxxor/ar25-1&sourceXml=docling-output.xml&xslt=docling/transformation.xslt&examples=taxxor/ar23,taxxor/ar24\""
            });
            return;
        }

        logger.LogInformation("Hierarchy test API: project={Project}, sourceXml={SourceXml}, xslt={Xslt}, examples={Examples}, model={Model}",
            project, sourceXml, xslt, examplesParam, model);

        // Load project
        var projectPath = Path.Combine("/app/data/input", project);
        if (!Directory.Exists(projectPath))
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = $"Project not found: {projectPath}"
            });
            return;
        }

        // Find source XML file
        var sourceXmlPath = Path.Combine(projectPath, sourceXml);
        if (!File.Exists(sourceXmlPath))
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = $"Source XML not found: {sourceXmlPath}"
            });
            return;
        }

        // Find XSLT file
        var xsltPath = Path.Combine("/app/xslt", xslt);
        if (!File.Exists(xsltPath))
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = $"XSLT file not found: {xsltPath}"
            });
            return;
        }

        // Read source XML
        var normalizedXml = await File.ReadAllTextAsync(sourceXmlPath);

        // Parse example hierarchy IDs and load files
        var exampleIds = examplesParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var exampleHierarchies = new List<string>();
        var missingExamples = new List<string>();

        foreach (var exampleId in exampleIds)
        {
            var examplePath = Path.Combine("/app/data/output", exampleId.Trim());
            var hierarchyFile = Path.Combine(examplePath, "hierarchy.xml");

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
            logger.LogWarning("Some example hierarchies not found: {MissingExamples}", string.Join(", ", missingExamples));
        }

        if (!exampleHierarchies.Any())
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "No valid example hierarchy files found",
                missingFiles = missingExamples
            });
            return;
        }

        logger.LogInformation("Loaded {Count} example hierarchies", exampleHierarchies.Count);

        // Call hierarchy generation service
        var startTime = DateTime.UtcNow;
        var proposal = await hierarchyService.GenerateHierarchyAsync(
            normalizedXml,
            exampleHierarchies,
            model,
            CancellationToken.None);

        var duration = (DateTime.UtcNow - startTime).TotalSeconds;

        // Calculate whitelist and omitted counts
        var doc = System.Xml.Linq.XDocument.Parse(normalizedXml);
        var headers = doc.Descendants()
            .Where(e => e.Name.LocalName.StartsWith("h") &&
                       e.Name.LocalName.Length == 2 &&
                       char.IsDigit(e.Name.LocalName[1]))
            .ToList();
        var whitelistCount = headers.Count;
        var hierarchyItemCount = proposal.TotalItems;
        var omittedCount = whitelistCount - hierarchyItemCount;

        // Calculate approximate prompt/response sizes
        var promptSize = normalizedXml.Length + (exampleHierarchies.Sum(e => e.Length));
        var responseSize = proposal.Root.ToString().Length; // Rough estimate

        // Return success metrics
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = proposal.ValidationResult?.IsValid ?? true,
            promptSize,
            responseSize,
            hierarchyItemCount,
            whitelistCount,
            omittedCount,
            hallucinatedCount = proposal.ValidationResult?.HallucinatedItems.Count ?? 0,
            hallucinatedItems = proposal.ValidationResult?.HallucinatedItems ?? new List<string>(),
            overallConfidence = proposal.OverallConfidence,
            uncertaintyCount = proposal.Uncertainties.Count,
            reasoning = proposal.Reasoning,
            durationSeconds = duration,
            model,
            timestamp = DateTime.UtcNow,
            validationSummary = proposal.ValidationResult?.Summary
        }, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        logger.LogInformation("Hierarchy generation completed successfully in {Duration}s", duration);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in hierarchy-test-api endpoint");
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = ex.Message,
            stackTrace = ex.StackTrace,
            timestamp = DateTime.UtcNow
        });
    }
});

// Map sandbox endpoint for testing prompt generation without calling LLM
// Logic extracted to PdfConversion.Endpoints.SandboxEndpoint for better maintainability
// Example: curl http://localhost:8085/sandbox              # anonymized (default: false)
// Example: curl http://localhost:8085/sandbox?anonymize=true   # anonymized examples
app.MapGet("/sandbox", async (
    HttpContext context,
    IXsltTransformationService xsltService,
    IHierarchyGeneratorService hierarchyGeneratorService,
    IHierarchyService hierarchyService,
    IOllamaService ollamaService,
    ILogger<Program> logger) =>
{
    await PdfConversion.Endpoints.SandboxEndpoint.HandleAsync(
        context,
        xsltService,
        hierarchyGeneratorService,
        hierarchyService,
        ollamaService,
        logger);
});

// Add health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(result);
    }
});

// Add liveness probe (simple check that app is running)
app.MapHealthChecks("/health/liveness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false, // No checks, just return healthy if app is running
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }));
    }
});

// Add readiness probe (checks dependencies)
app.MapHealthChecks("/health/readiness", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            timestamp = DateTime.UtcNow
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await context.Response.WriteAsync(result);
    }
});

    Log.Information("Application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

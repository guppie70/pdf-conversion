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
builder.Services.AddServerSideBlazor();

// Add memory cache for service caching
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024 * 1024 * 100; // 100MB limit
});

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
builder.Services.AddScoped<IXsltTransformationService, XsltTransformationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<DevelopmentToolbarState>();
builder.Services.AddSingleton<ITransformationLogService, TransformationLogService>();
builder.Services.AddSingleton<ThemeService>();

// Register performance optimization services
builder.Services.AddSingleton<IDistributedCacheService, DistributedCacheService>();
builder.Services.AddSingleton<IPerformanceMonitoringService, PerformanceMonitoringService>();
builder.Services.AddSingleton<IMemoryPoolManager, MemoryPoolManager>();
builder.Services.AddScoped<IStreamingXsltTransformationService, StreamingXsltTransformationService>();
builder.Services.AddSingleton<IBatchTransformationService, BatchTransformationService>();

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

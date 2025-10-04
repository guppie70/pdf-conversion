using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PdfConversion.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add memory cache for service caching
builder.Services.AddMemoryCache();

// Add logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Register custom services
builder.Services.AddScoped<IProjectManagementService, ProjectManagementService>();
// builder.Services.AddScoped<IXsltTransformationService, XsltTransformationService>();
// builder.Services.AddScoped<IFileSystemService, FileSystemService>();
// builder.Services.AddHttpClient<IXslt3ServiceClient, Xslt3ServiceClient>();
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

// Add health check endpoint for Docker
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

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
builder.Services.AddScoped<IXsltTransformationService, XsltTransformationService>();

// Configure HttpClient for XSLT3Service
builder.Services.AddHttpClient<IXslt3ServiceClient, Xslt3ServiceClient>(client =>
{
    var xslt3ServiceUrl = builder.Configuration.GetValue<string>("XSLT3_SERVICE_URL") ?? "http://xslt3service:4806";
    client.BaseAddress = new Uri(xslt3ServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "PdfConversion/1.0");
});

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

// Add health check endpoint for Docker
app.MapGet("/health", () => Results.Ok("Healthy"));

app.Run();

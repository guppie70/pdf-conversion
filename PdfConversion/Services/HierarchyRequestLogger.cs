namespace PdfConversion.Services;

/// <summary>
/// Stores the last AI hierarchy generation request parameters for debugging purposes.
/// This service provides a "Test API" button after generation to quickly test with the same parameters.
/// </summary>
public interface IHierarchyRequestLogger
{
    /// <summary>
    /// Logs the parameters from an AI hierarchy generation request
    /// </summary>
    void LogRequest(string project, string sourceXml, string xslt, List<string> examplePaths);

    /// <summary>
    /// Gets the last logged request parameters (returns null if no request logged yet)
    /// </summary>
    HierarchyRequestParams? GetLastRequest();
}

/// <summary>
/// Request parameters for AI hierarchy generation
/// </summary>
public class HierarchyRequestParams
{
    public string Project { get; set; } = string.Empty;
    public string SourceXml { get; set; } = string.Empty;
    public string Xslt { get; set; } = string.Empty;
    public string Examples { get; set; } = string.Empty; // Comma-separated example paths
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Thread-safe singleton service that stores the last AI hierarchy generation request.
/// Used for development/debugging to provide a "Test API" button with exact parameters.
/// </summary>
public class HierarchyRequestLogger : IHierarchyRequestLogger
{
    private readonly object _lock = new();
    private HierarchyRequestParams? _lastRequest;
    private readonly ILogger<HierarchyRequestLogger> _logger;

    public HierarchyRequestLogger(ILogger<HierarchyRequestLogger> logger)
    {
        _logger = logger;
    }

    public void LogRequest(string project, string sourceXml, string xslt, List<string> examplePaths)
    {
        lock (_lock)
        {
            // Convert example paths to format expected by test API
            // From: /app/data/training-material/hierarchies/optiver/ar24-6/hierarchy.xml
            // To: optiver/projects/ar24-6
            var exampleIds = examplePaths
                .Select(path =>
                {
                    // Extract customer/projectId from training material path
                    var match = System.Text.RegularExpressions.Regex.Match(
                        path,
                        @"training-material/hierarchies/([^/]+)/([^/]+)");

                    if (match.Success)
                    {
                        var customer = match.Groups[1].Value;
                        var projectId = match.Groups[2].Value;
                        return $"{customer}/projects/{projectId}";
                    }
                    return null;
                })
                .Where(id => id != null)
                .ToList();

            // Convert project from "customer/projectId" to "customer/projects/projectId"
            var projectParts = project.Split('/');
            var projectPath = projectParts.Length == 2
                ? $"{projectParts[0]}/projects/{projectParts[1]}"
                : project;

            _lastRequest = new HierarchyRequestParams
            {
                Project = projectPath,
                SourceXml = Path.GetFileName(sourceXml), // Store just filename
                Xslt = xslt.Replace("/app/xslt/", ""), // Remove /app/xslt/ prefix
                Examples = string.Join(",", exampleIds!),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogInformation(
                "[HierarchyRequestLogger] Logged request: project={Project}, sourceXml={SourceXml}, xslt={Xslt}, examples={Examples}",
                _lastRequest.Project, _lastRequest.SourceXml, _lastRequest.Xslt, _lastRequest.Examples);
        }
    }

    public HierarchyRequestParams? GetLastRequest()
    {
        lock (_lock)
        {
            return _lastRequest;
        }
    }
}

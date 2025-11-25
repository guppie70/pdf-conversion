using System.Text.RegularExpressions;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Flexible file group builder service implementation.
/// </summary>
public class FileGroupBuilderService : IFileGroupBuilderService
{
    private readonly IProjectManagementService _projectService;
    private readonly ProjectMetadataService _metadataService;
    private readonly ILogger<FileGroupBuilderService> _logger;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;

    public FileGroupBuilderService(
        IProjectManagementService projectService,
        ProjectMetadataService metadataService,
        ILogger<FileGroupBuilderService> logger)
    {
        _projectService = projectService;
        _metadataService = metadataService;
        _logger = logger;
        _inputBasePath = "/app/data/input";
        _outputBasePath = "/app/data/output";
    }

    /// <inheritdoc />
    public IFileGroupQueryBuilder CreateQuery()
    {
        return new FileGroupQueryBuilder(
            _projectService,
            _metadataService,
            _logger,
            _inputBasePath,
            _outputBasePath);
    }

    #region Legacy Methods for Backward Compatibility

    /// <inheritdoc />
    [Obsolete("Use CreateQuery() for new implementations")]
    public async Task<List<ProjectFileGroup>> BuildXmlFileGroupsAsync(
        bool includeInputFiles = true,
        bool includeOutputFiles = false,
        bool onlyActiveProjects = true,
        string? customer = null,
        string? projectId = null,
        bool includeNormalizedFolder = true)
    {
        var query = CreateQuery()
            .OnlyActiveProjects(onlyActiveProjects)
            .UseInputBase(includeInputFiles)
            .UseOutputBase(includeOutputFiles);

        if (!string.IsNullOrEmpty(customer))
            query.ForCustomer(customer);

        if (!string.IsNullOrEmpty(projectId))
            query.ForProject(projectId);

        // Add source locations based on parameters
        if (includeInputFiles)
        {
            // Root XML files
            query.FromProjectPath("", SearchOption.TopDirectoryOnly)
                 .WithExtensions(".xml");

            // Source folder files
            query.FromProjectPath("source")
                 .WithExtensions(".xml");

            // Normalized folder files (if requested)
            if (includeNormalizedFolder)
            {
                query.FromProjectPath("normalized")
                     .WithExtensions(".xml", ".xhtml");
            }
        }

        if (includeOutputFiles)
        {
            query.FromProjectPath("", SearchOption.TopDirectoryOnly)
                 .WithExtensions(".xml");
        }

        return await query.BuildAsync();
    }

    /// <inheritdoc />
    [Obsolete("Use CreateQuery() for new implementations")]
    public async Task<List<ProjectFileGroup>> BuildDocumentFileGroupsAsync(
        string[] extensions,
        bool onlyActiveProjects = true,
        string? customer = null,
        string? projectId = null)
    {
        var query = CreateQuery()
            .OnlyActiveProjects(onlyActiveProjects)
            .UseInputBase(true)
            .UseOutputBase(false)
            .FromProjectPath("")
            .WithExtensions(extensions);

        if (!string.IsNullOrEmpty(customer))
            query.ForCustomer(customer);

        if (!string.IsNullOrEmpty(projectId))
            query.ForProject(projectId);

        return await query.BuildAsync();
    }

    #endregion
}

/// <summary>
/// Implementation of the file group query builder.
/// </summary>
internal class FileGroupQueryBuilder : IFileGroupQueryBuilder
{
    private readonly IProjectManagementService _projectService;
    private readonly ProjectMetadataService _metadataService;
    private readonly ILogger _logger;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;

    // Query configuration
    private readonly List<FileSource> _sources = new();
    private readonly List<IFileFilter> _filters = new();
    private readonly List<string> _excludePatterns = new();
    private string? _customer;
    private string? _projectId;
    private bool _onlyActiveProjects = true;
    private bool _useInputBase = true;
    private bool _useOutputBase = false;

    public FileGroupQueryBuilder(
        IProjectManagementService projectService,
        ProjectMetadataService metadataService,
        ILogger logger,
        string inputBasePath,
        string outputBasePath)
    {
        _projectService = projectService;
        _metadataService = metadataService;
        _logger = logger;
        _inputBasePath = inputBasePath;
        _outputBasePath = outputBasePath;
    }

    public IFileGroupQueryBuilder FromFolder(string basePath, string? subFolder = null, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _sources.Add(new FileSource
        {
            BasePath = basePath,
            SubFolder = subFolder,
            SearchOption = searchOption,
            IsCustomPath = true
        });
        return this;
    }

    public IFileGroupQueryBuilder FromProjectPath(string relativePath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        _sources.Add(new FileSource
        {
            SubFolder = relativePath,
            SearchOption = searchOption,
            IsCustomPath = false
        });
        return this;
    }

    public IFileGroupQueryBuilder WithExtensions(params string[] extensions)
    {
        if (extensions?.Length > 0)
        {
            var normalizedExtensions = extensions
                .Select(e => e.StartsWith(".") ? e : "." + e)
                .Select(e => e.ToLowerInvariant())
                .ToArray();

            _filters.Add(new ExtensionFilter(normalizedExtensions));
        }
        return this;
    }

    public IFileGroupQueryBuilder WithPattern(string pattern)
    {
        _filters.Add(new PatternFilter(pattern));
        return this;
    }

    public IFileGroupQueryBuilder WithFilter(Func<string, bool> predicate)
    {
        _filters.Add(new PredicateFilter(predicate));
        return this;
    }

    public IFileGroupQueryBuilder WithRegex(string pattern)
    {
        _filters.Add(new RegexFilter(pattern));
        return this;
    }

    public IFileGroupQueryBuilder ExcludePattern(string pattern)
    {
        _excludePatterns.Add(pattern);
        return this;
    }

    public IFileGroupQueryBuilder ForCustomer(string customer)
    {
        _customer = customer;
        return this;
    }

    public IFileGroupQueryBuilder ForProject(string projectId)
    {
        _projectId = projectId;
        return this;
    }

    public IFileGroupQueryBuilder OnlyActiveProjects(bool onlyActive = true)
    {
        _onlyActiveProjects = onlyActive;
        return this;
    }

    public IFileGroupQueryBuilder UseInputBase(bool useInput = true)
    {
        _useInputBase = useInput;
        return this;
    }

    public IFileGroupQueryBuilder UseOutputBase(bool useOutput = true)
    {
        _useOutputBase = useOutput;
        return this;
    }

    public async Task<List<ProjectFileGroup>> BuildAsync()
    {
        var files = await GetFilesAsync();

        // Group files by project
        var groups = files
            .GroupBy(f => new { f.Customer, f.ProjectId, f.ProjectName })
            .Select(g => new ProjectFileGroup
            {
                Customer = g.Key.Customer,
                ProjectId = g.Key.ProjectId,
                ProjectName = g.Key.ProjectName,
                Files = g.Select(f => new ProjectFile
                {
                    FileName = f.FileName,
                    FullPath = f.FullPath
                }).ToList()
            })
            .OrderBy(g => g.Customer)
            .ThenBy(g => g.ProjectId)
            .ToList();

        _logger.LogDebug("Built {Count} file groups from query", groups.Count);
        return groups;
    }

    public async Task<List<FileResult>> GetFilesAsync()
    {
        try
        {
            // Get projects based on filters
            var projects = await GetFilteredProjectsAsync();
            var results = new List<FileResult>();

            foreach (var project in projects)
            {
                var projectFiles = await GetProjectFilesAsync(project);
                results.AddRange(projectFiles);
            }

            _logger.LogDebug("Query returned {Count} files", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing file query");
            return new List<FileResult>();
        }
    }

    private async Task<List<Project>> GetFilteredProjectsAsync()
    {
        // Get all projects
        var allProjects = (await _projectService.GetProjectsAsync()).ToList();

        // Filter by customer and project ID if specified
        if (!string.IsNullOrEmpty(_customer))
        {
            allProjects = allProjects
                .Where(p => p.Organization.Equals(_customer, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrEmpty(_projectId))
        {
            allProjects = allProjects
                .Where(p => p.ProjectId.Equals(_projectId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter by active status if requested
        if (_onlyActiveProjects)
        {
            var activeProjectMetadata = await _metadataService.GetActiveProjects();
            allProjects = allProjects.Where(p =>
            {
                if (activeProjectMetadata.TryGetValue(p.Organization, out var tenantProjects))
                {
                    return tenantProjects.ContainsKey(p.ProjectId);
                }
                return false;
            }).ToList();
        }

        return allProjects;
    }

    private async Task<List<FileResult>> GetProjectFilesAsync(Project project)
    {
        var results = new List<FileResult>();
        var basePaths = new List<string>();

        // Determine base paths to search
        if (_useInputBase)
            basePaths.Add(Path.Combine(_inputBasePath, project.Organization, "projects", project.ProjectId));

        if (_useOutputBase)
            basePaths.Add(Path.Combine(_outputBasePath, project.Organization, "projects", project.ProjectId));

        // If no sources specified, use project root
        var sources = _sources.Any() ? _sources : new List<FileSource>
        {
            new FileSource { SubFolder = "", SearchOption = SearchOption.TopDirectoryOnly }
        };

        foreach (var basePath in basePaths)
        {
            if (!Directory.Exists(basePath))
                continue;

            foreach (var source in sources)
            {
                var searchPath = source.IsCustomPath
                    ? source.BasePath ?? basePath
                    : Path.Combine(basePath, source.SubFolder ?? "");

                if (!Directory.Exists(searchPath))
                    continue;

                try
                {
                    var files = Directory.GetFiles(searchPath, "*", source.SearchOption);

                    foreach (var file in files)
                    {
                        // Apply filters
                        if (!PassesFilters(file))
                            continue;

                        // Check exclusions
                        if (IsExcluded(file))
                            continue;

                        var fileInfo = new FileInfo(file);
                        var relativePath = Path.GetRelativePath(basePath, file);

                        results.Add(new FileResult
                        {
                            Customer = project.Organization,
                            ProjectId = project.ProjectId,
                            ProjectName = project.Name,
                            FileName = fileInfo.Name,
                            FullPath = file,
                            RelativePath = relativePath,
                            LastModified = fileInfo.LastWriteTime,
                            SizeInBytes = fileInfo.Length
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error scanning directory: {Path}", searchPath);
                }
            }
        }

        return results;
    }

    private bool PassesFilters(string filePath)
    {
        // If no filters specified, include all files
        if (!_filters.Any())
            return true;

        // File must pass ALL filters
        return _filters.All(filter => filter.Matches(filePath));
    }

    private bool IsExcluded(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        foreach (var pattern in _excludePatterns)
        {
            // Support simple wildcard patterns
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(fileName, regex, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    #region Helper Classes

    private class FileSource
    {
        public string? BasePath { get; set; }
        public string? SubFolder { get; set; }
        public SearchOption SearchOption { get; set; }
        public bool IsCustomPath { get; set; }
    }

    private interface IFileFilter
    {
        bool Matches(string filePath);
    }

    private class ExtensionFilter : IFileFilter
    {
        private readonly HashSet<string> _extensions;

        public ExtensionFilter(string[] extensions)
        {
            _extensions = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        }

        public bool Matches(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return _extensions.Contains(extension);
        }
    }

    private class PatternFilter : IFileFilter
    {
        private readonly Regex _regex;

        public PatternFilter(string pattern)
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            _regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        }

        public bool Matches(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return _regex.IsMatch(fileName);
        }
    }

    private class RegexFilter : IFileFilter
    {
        private readonly Regex _regex;

        public RegexFilter(string pattern)
        {
            _regex = new Regex(pattern, RegexOptions.IgnoreCase);
        }

        public bool Matches(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            return _regex.IsMatch(fileName);
        }
    }

    private class PredicateFilter : IFileFilter
    {
        private readonly Func<string, bool> _predicate;

        public PredicateFilter(Func<string, bool> predicate)
        {
            _predicate = predicate;
        }

        public bool Matches(string filePath)
        {
            return _predicate(filePath);
        }
    }

    #endregion
}
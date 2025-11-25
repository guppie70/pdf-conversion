using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for building file groups with flexible filtering and source configuration.
/// </summary>
public interface IFileGroupBuilderService
{
    /// <summary>
    /// Creates a new file group query builder.
    /// </summary>
    IFileGroupQueryBuilder CreateQuery();

    /// <summary>
    /// Legacy method for backward compatibility - builds XML file groups.
    /// </summary>
    [Obsolete("Use CreateQuery() for new implementations")]
    Task<List<ProjectFileGroup>> BuildXmlFileGroupsAsync(
        bool includeInputFiles = true,
        bool includeOutputFiles = false,
        bool onlyActiveProjects = true,
        string? customer = null,
        string? projectId = null,
        bool includeNormalizedFolder = true);

    /// <summary>
    /// Legacy method for backward compatibility - builds document file groups.
    /// </summary>
    [Obsolete("Use CreateQuery() for new implementations")]
    Task<List<ProjectFileGroup>> BuildDocumentFileGroupsAsync(
        string[] extensions,
        bool onlyActiveProjects = true,
        string? customer = null,
        string? projectId = null);
}

/// <summary>
/// Fluent query builder for file group construction.
/// </summary>
public interface IFileGroupQueryBuilder
{
    /// <summary>
    /// Add a source folder to search for files.
    /// </summary>
    /// <param name="basePath">Base path (e.g., "/app/data/input" or "/app/data/output")</param>
    /// <param name="subFolder">Optional subfolder within project directory (e.g., "source", "normalized")</param>
    /// <param name="searchOption">Search option for directory traversal</param>
    IFileGroupQueryBuilder FromFolder(string basePath, string? subFolder = null, SearchOption searchOption = SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Add a project-relative path to search (relative to project folder).
    /// </summary>
    /// <param name="relativePath">Path relative to project folder (e.g., "source", "normalized/v2")</param>
    /// <param name="searchOption">Search option for directory traversal</param>
    IFileGroupQueryBuilder FromProjectPath(string relativePath, SearchOption searchOption = SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Filter files by extension.
    /// </summary>
    /// <param name="extensions">File extensions to include (e.g., ".xml", ".pdf")</param>
    IFileGroupQueryBuilder WithExtensions(params string[] extensions);

    /// <summary>
    /// Filter files by pattern (supports wildcards).
    /// </summary>
    /// <param name="pattern">File pattern (e.g., "*.xml", "normalized*.xml", "report-*.pdf")</param>
    IFileGroupQueryBuilder WithPattern(string pattern);

    /// <summary>
    /// Filter files by custom predicate.
    /// </summary>
    /// <param name="predicate">Custom filter function</param>
    IFileGroupQueryBuilder WithFilter(Func<string, bool> predicate);

    /// <summary>
    /// Filter files by regex pattern.
    /// </summary>
    /// <param name="pattern">Regex pattern for file name matching</param>
    IFileGroupQueryBuilder WithRegex(string pattern);

    /// <summary>
    /// Exclude files matching a pattern.
    /// </summary>
    /// <param name="pattern">Pattern to exclude (e.g., "*.backup", "temp-*")</param>
    IFileGroupQueryBuilder ExcludePattern(string pattern);

    /// <summary>
    /// Filter to specific customer.
    /// </summary>
    IFileGroupQueryBuilder ForCustomer(string customer);

    /// <summary>
    /// Filter to specific project.
    /// </summary>
    IFileGroupQueryBuilder ForProject(string projectId);

    /// <summary>
    /// Include only active projects (default: true).
    /// </summary>
    IFileGroupQueryBuilder OnlyActiveProjects(bool onlyActive = true);

    /// <summary>
    /// Set whether to search input directories (default: true).
    /// </summary>
    IFileGroupQueryBuilder UseInputBase(bool useInput = true);

    /// <summary>
    /// Set whether to search output directories (default: false).
    /// </summary>
    IFileGroupQueryBuilder UseOutputBase(bool useOutput = true);

    /// <summary>
    /// Execute the query and return the file groups.
    /// </summary>
    Task<List<ProjectFileGroup>> BuildAsync();

    /// <summary>
    /// Execute the query and return flat list of files (without grouping).
    /// </summary>
    Task<List<FileResult>> GetFilesAsync();
}

/// <summary>
/// Result for individual file query.
/// </summary>
public class FileResult
{
    public string Customer { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty; // Path relative to project root
    public DateTime LastModified { get; set; }
    public long SizeInBytes { get; set; }
}

/// <summary>
/// Predefined file source configurations for common scenarios.
/// </summary>
public static class FileSourcePresets
{
    /// <summary>
    /// Source XML files from source/ folder.
    /// </summary>
    public static IFileGroupQueryBuilder SourceXmlFiles(this IFileGroupQueryBuilder builder)
        => builder.FromProjectPath("source").WithExtensions(".xml");

    /// <summary>
    /// Normalized XML/XHTML files from normalized/ folder.
    /// </summary>
    public static IFileGroupQueryBuilder NormalizedFiles(this IFileGroupQueryBuilder builder)
        => builder.FromProjectPath("normalized").WithExtensions(".xml", ".xhtml");

    /// <summary>
    /// All XML files from project root (excluding subfolders).
    /// </summary>
    public static IFileGroupQueryBuilder RootXmlFiles(this IFileGroupQueryBuilder builder)
        => builder.FromProjectPath("").WithExtensions(".xml");

    /// <summary>
    /// Document files (PDF, DOCX, DOC) from project root.
    /// </summary>
    public static IFileGroupQueryBuilder DocumentFiles(this IFileGroupQueryBuilder builder)
        => builder.FromProjectPath("").WithExtensions(".pdf", ".docx", ".doc");

    /// <summary>
    /// Output XML files from output base path.
    /// </summary>
    public static IFileGroupQueryBuilder OutputXmlFiles(this IFileGroupQueryBuilder builder)
        => builder.UseOutputBase(true).UseInputBase(false)
                 .FromProjectPath("").WithExtensions(".xml");

    /// <summary>
    /// Section files from sections/ folder in output.
    /// </summary>
    public static IFileGroupQueryBuilder SectionFiles(this IFileGroupQueryBuilder builder)
        => builder.UseOutputBase(true).UseInputBase(false)
                 .FromProjectPath("sections").WithExtensions(".xml");
}
using System.IO.Compression;
using System.Text;

namespace PdfConversion.Services;

/// <summary>
/// Service for creating downloadable project archives (ZIP files)
/// Centralizes all ZIP logic for future extensibility
/// </summary>
public class ProjectArchiveService : IProjectArchiveService
{
    private readonly ILogger<ProjectArchiveService> _logger;
    private readonly string _inputBasePath;
    private readonly string _outputBasePath;

    public ProjectArchiveService(ILogger<ProjectArchiveService> logger)
    {
        _logger = logger;
        _inputBasePath = "/app/data/input";
        _outputBasePath = "/app/data/output";
    }

    public async Task<byte[]?> CreateProjectArchiveAsync(string customer, string projectId)
    {
        try
        {
            _logger.LogInformation("Creating archive for project {Customer}/{ProjectId}", customer, projectId);

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Add section files from output/data
                var dataPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "data");
                if (Directory.Exists(dataPath))
                {
                    var sectionFiles = Directory.GetFiles(dataPath, "*.xml");
                    foreach (var filePath in sectionFiles)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var entry = archive.CreateEntry($"data/{fileName}", CompressionLevel.Optimal);

                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(filePath);
                        await fileStream.CopyToAsync(entryStream);

                        _logger.LogDebug("Added section file: {FileName}", fileName);
                    }
                }

                // Add all images from input/images
                var imagesPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "images");
                if (Directory.Exists(imagesPath))
                {
                    await AddDirectoryToArchiveAsync(archive, imagesPath, "images");
                }

                // Add hierarchy files from input/metadata (if exists)
                var metadataPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "metadata");
                if (Directory.Exists(metadataPath))
                {
                    var hierarchyFiles = Directory.GetFiles(metadataPath, "*.xml");
                    foreach (var filePath in hierarchyFiles)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var entry = archive.CreateEntry($"metadata/{fileName}", CompressionLevel.Optimal);

                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(filePath);
                        await fileStream.CopyToAsync(entryStream);

                        _logger.LogDebug("Added metadata file: {FileName}", fileName);
                    }
                }

                // Add hierarchy.xml from output directory (if exists)
                var hierarchyFilePath = Path.Combine(_outputBasePath, customer, "projects", projectId, "hierarchy.xml");
                if (File.Exists(hierarchyFilePath))
                {
                    var entry = archive.CreateEntry("hierarchy.xml", CompressionLevel.Optimal);

                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(hierarchyFilePath);
                    await fileStream.CopyToAsync(entryStream);

                    _logger.LogDebug("Added hierarchy.xml");
                }

                // Add normalized.xml from output directory (if exists)
                var normalizedFilePath = Path.Combine(_outputBasePath, customer, "projects", projectId, "normalized.xml");
                if (File.Exists(normalizedFilePath))
                {
                    var entry = archive.CreateEntry("normalized.xml", CompressionLevel.Optimal);

                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(normalizedFilePath);
                    await fileStream.CopyToAsync(entryStream);

                    _logger.LogDebug("Added normalized.xml");
                }
            }

            memoryStream.Position = 0;
            var zipBytes = memoryStream.ToArray();

            _logger.LogInformation("Created archive for {Customer}/{ProjectId}: {Size} bytes",
                customer, projectId, zipBytes.Length);

            return zipBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create archive for {Customer}/{ProjectId}", customer, projectId);
            return null;
        }
    }

    public string GetArchiveFilename(string customer, string projectId)
    {
        return $"{customer}-{projectId}.zip";
    }

    public Task<bool> HasFilesToArchiveAsync(string customer, string projectId)
    {
        try
        {
            // Check for section XML files in output/data
            var outputDataPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "data");
            var hasOutputSections = Directory.Exists(outputDataPath) && Directory.GetFiles(outputDataPath, "*.xml").Any();

            // Check for hierarchy XML in either input hierarchy.xml or input/metadata/*.xml
            var inputHierarchyPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "hierarchy.xml");
            var inputMetadataPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "metadata");
            var hasHierarchy = File.Exists(inputHierarchyPath) ||
                              (Directory.Exists(inputMetadataPath) && Directory.GetFiles(inputMetadataPath, "*.xml").Any());

            // Both conditions must be met for download to be available
            return Task.FromResult(hasOutputSections && hasHierarchy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for files to archive: {Customer}/{ProjectId}", customer, projectId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Generate manifest.yml content from tracked files
    /// </summary>
    private string GenerateManifestYml(ManifestData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Taxxor TDM Package Manifest");
        sb.AppendLine("# Generated by PDF Conversion Tool");
        sb.AppendLine();

        // Hierarchy section
        sb.AppendLine("hierarchy:");
        sb.AppendLine($"  file: {data.Hierarchy ?? "none"}");
        sb.AppendLine();

        // Sections
        sb.AppendLine("sections:");
        sb.AppendLine($"  count: {data.Sections.Count}");
        sb.AppendLine("  files:");
        if (data.Sections.Any())
        {
            foreach (var section in data.Sections.OrderBy(s => s))
            {
                sb.AppendLine($"    - {section}");
            }
        }
        else
        {
            sb.AppendLine("    []");
        }
        sb.AppendLine();

        // Images
        sb.AppendLine("images:");
        sb.AppendLine($"  count: {data.Images.Count}");
        sb.AppendLine("  files:");
        if (data.Images.Any())
        {
            foreach (var image in data.Images.OrderBy(i => i))
            {
                sb.AppendLine($"    - {image}");
            }
        }
        else
        {
            sb.AppendLine("    []");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively add directory contents to ZIP archive
    /// </summary>
    private async Task AddDirectoryToArchiveAsync(ZipArchive archive, string sourcePath, string archivePath)
    {
        var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(sourcePath, filePath);
            var entryName = Path.Combine(archivePath, relativePath).Replace('\\', '/');

            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

            using var entryStream = entry.Open();
            using var fileStream = File.OpenRead(filePath);
            await fileStream.CopyToAsync(entryStream);

            _logger.LogDebug("Added file: {EntryName}", entryName);
        }
    }

    /// <summary>
    /// Helper class to track files for manifest generation
    /// </summary>
    private class ManifestData
    {
        public List<string> Sections { get; } = new();
        public List<string> Images { get; } = new();
        public string? Hierarchy { get; private set; }

        public void AddSection(string path) => Sections.Add(path);
        public void AddImage(string path) => Images.Add(path);
        public void SetHierarchy(string path) => Hierarchy = path;
    }
}

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

    public async Task<byte[]?> CreateProjectArchiveAsync(string customer, string projectId, string hierarchyFileName)
    {
        try
        {
            _logger.LogInformation("Creating archive for {Customer}/{ProjectId} with hierarchy: {Hierarchy}",
                customer, projectId, hierarchyFileName);

            var manifestData = new ManifestData();

            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // 1. Add section XML files from output/data
                var dataPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "data");
                if (Directory.Exists(dataPath))
                {
                    var sectionFiles = Directory.GetFiles(dataPath, "*.xml");
                    foreach (var filePath in sectionFiles)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var entryName = $"data/{fileName}";
                        await AddFileToArchiveAsync(archive, filePath, entryName);
                        manifestData.AddSection(entryName);
                        _logger.LogDebug("Added section file: {FileName}", fileName);
                    }
                }

                // 2. Add selected hierarchy file from input/metadata
                var hierarchyPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "metadata", hierarchyFileName);
                if (File.Exists(hierarchyPath))
                {
                    var entryName = $"metadata/{hierarchyFileName}";
                    await AddFileToArchiveAsync(archive, hierarchyPath, entryName);
                    manifestData.SetHierarchy(entryName);
                    _logger.LogDebug("Added hierarchy file: {FileName}", hierarchyFileName);
                }
                else
                {
                    _logger.LogWarning("Hierarchy file not found: {Path}", hierarchyPath);
                }

                // 3. Add all images recursively
                var imagesPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "images");
                if (Directory.Exists(imagesPath))
                {
                    await AddDirectoryToArchiveAsync(archive, imagesPath, "images", manifestData);
                    _logger.LogDebug("Added {Count} images", manifestData.Images.Count);
                }

                // 4. Generate and add manifest.yml
                var manifestYml = GenerateManifestYml(manifestData);
                var manifestEntry = archive.CreateEntry("manifest.yml", CompressionLevel.Optimal);
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    await writer.WriteAsync(manifestYml);
                }
                _logger.LogDebug("Added manifest.yml");
            }

            memoryStream.Position = 0;
            var zipBytes = memoryStream.ToArray();

            _logger.LogInformation("Created archive for {Customer}/{ProjectId}: {Size} bytes, {Sections} sections, {Images} images",
                customer, projectId, zipBytes.Length, manifestData.Sections.Count, manifestData.Images.Count);

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
    /// Add a single file to the ZIP archive
    /// </summary>
    private async Task AddFileToArchiveAsync(ZipArchive archive, string filePath, string entryName)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

        using var entryStream = entry.Open();
        using var fileStream = File.OpenRead(filePath);
        await fileStream.CopyToAsync(entryStream);

        _logger.LogDebug("Added file: {EntryName}", entryName);
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
    /// Recursively add directory contents to ZIP archive and track in manifest
    /// </summary>
    private async Task AddDirectoryToArchiveAsync(ZipArchive archive, string sourcePath, string archivePath, ManifestData manifestData)
    {
        var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories);

        foreach (var filePath in files)
        {
            var relativePath = Path.GetRelativePath(sourcePath, filePath);
            var entryName = Path.Combine(archivePath, relativePath).Replace('\\', '/');

            await AddFileToArchiveAsync(archive, filePath, entryName);
            manifestData.AddImage(entryName);
        }
    }

    public async Task<string?> CreateAndSavePackageAsync(string customer, string projectId, string hierarchyFilePath, string packageName)
    {
        try
        {
            _logger.LogInformation("Creating package '{PackageName}' for {Customer}/{ProjectId} with hierarchy: {Hierarchy}",
                packageName, customer, projectId, hierarchyFilePath);

            // Ensure packages directory exists
            var packagesPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "packages");
            Directory.CreateDirectory(packagesPath);

            var packagePath = Path.Combine(packagesPath, $"{packageName}.zip");

            var manifestData = new ManifestData();

            // Delete existing package if it exists
            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
                _logger.LogDebug("Deleted existing package: {PackagePath}", packagePath);
            }

            using (var fileStream = new FileStream(packagePath, FileMode.Create))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                // 1. Add section XML files from output/data
                var dataPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "data");
                if (Directory.Exists(dataPath))
                {
                    var sectionFiles = Directory.GetFiles(dataPath, "*.xml");
                    foreach (var filePath in sectionFiles)
                    {
                        var fileName = Path.GetFileName(filePath);
                        var entryName = $"data/{fileName}";
                        await AddFileToArchiveAsync(archive, filePath, entryName);
                        manifestData.AddSection(entryName);
                    }
                    _logger.LogDebug("Added {Count} section files", sectionFiles.Length);
                }

                // 2. Add the hierarchy file that was used in conversion
                if (File.Exists(hierarchyFilePath))
                {
                    var hierarchyFileName = Path.GetFileName(hierarchyFilePath);
                    var entryName = $"metadata/{hierarchyFileName}";
                    await AddFileToArchiveAsync(archive, hierarchyFilePath, entryName);
                    manifestData.SetHierarchy(entryName);
                    _logger.LogDebug("Added hierarchy file: {FileName}", hierarchyFileName);
                }
                else
                {
                    _logger.LogWarning("Hierarchy file not found: {Path}", hierarchyFilePath);
                }

                // 3. Add all images recursively
                var imagesPath = Path.Combine(_inputBasePath, customer, "projects", projectId, "images");
                if (Directory.Exists(imagesPath))
                {
                    await AddDirectoryToArchiveAsync(archive, imagesPath, "images", manifestData);
                    _logger.LogDebug("Added {Count} images", manifestData.Images.Count);
                }

                // 4. Generate and add manifest.yml
                var manifestYml = GenerateManifestYml(manifestData);
                var manifestEntry = archive.CreateEntry("manifest.yml", CompressionLevel.Optimal);
                using (var entryStream = manifestEntry.Open())
                using (var writer = new StreamWriter(entryStream))
                {
                    await writer.WriteAsync(manifestYml);
                }
            }

            var fileInfo = new FileInfo(packagePath);
            _logger.LogInformation("Created package '{PackageName}' for {Customer}/{ProjectId}: {Size} bytes, {Sections} sections, {Images} images",
                packageName, customer, projectId, fileInfo.Length, manifestData.Sections.Count, manifestData.Images.Count);

            return packagePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create package '{PackageName}' for {Customer}/{ProjectId}", packageName, customer, projectId);
            return null;
        }
    }

    public Task<List<string>> GetAvailablePackagesAsync(string customer, string projectId)
    {
        try
        {
            var packagesPath = Path.Combine(_outputBasePath, customer, "projects", projectId, "packages");

            if (!Directory.Exists(packagesPath))
            {
                return Task.FromResult(new List<string>());
            }

            var packages = Directory.GetFiles(packagesPath, "*.zip")
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Cast<string>()
                .OrderBy(f => f)
                .ToList();

            _logger.LogDebug("Found {Count} packages for {Customer}/{ProjectId}", packages.Count, customer, projectId);
            return Task.FromResult(packages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available packages for {Customer}/{ProjectId}", customer, projectId);
            return Task.FromResult(new List<string>());
        }
    }

    public string? GetPackagePath(string customer, string projectId, string packageFileName)
    {
        try
        {
            var packagePath = Path.Combine(_outputBasePath, customer, "projects", projectId, "packages", packageFileName);

            if (File.Exists(packagePath))
            {
                return packagePath;
            }

            _logger.LogWarning("Package not found: {PackagePath}", packagePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get package path for {Customer}/{ProjectId}/{FileName}", customer, projectId, packageFileName);
            return null;
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

using System.IO.Compression;
using System.Xml;
using PdfConversion.Models;

namespace PdfConversion.Services;

public interface IFileService
{
    Task<(bool Success, string Message)> UploadXmlFileAsync(string projectId, string fileName, Stream fileStream);
    Task<(bool Success, string Message)> UploadXsltFileAsync(Stream fileStream);
    Task<byte[]> GenerateZipArchiveAsync(string projectId);
    Task<byte[]> GenerateAllResultsZipAsync();
    Task<byte[]> GetTransformationLogsAsync();
    bool ValidateXmlFile(Stream fileStream);
    bool ValidateXsltFile(Stream fileStream);
    IEnumerable<string> GetXsltFiles();
    Task<(bool Success, string Message, int AmpersandCount, int LessThanCount, int GreaterThanCount)> FixInvalidXmlCharactersAsync(string projectId, string fileName);
    Task<(bool Success, string Message)> SanitizeXmlFileAsync(string projectId, string fileName);
}

public class FileService : IFileService
{
    private readonly IProjectManagementService _projectService;
    private readonly ILogger<FileService> _logger;
    private readonly IXslt3ServiceClient? _xslt3Client;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public FileService(IProjectManagementService projectService, ILogger<FileService> logger, IXslt3ServiceClient? xslt3Client = null)
    {
        _projectService = projectService;
        _logger = logger;
        _xslt3Client = xslt3Client;
    }

    public async Task<(bool Success, string Message)> UploadXmlFileAsync(string projectId, string fileName, Stream fileStream)
    {
        try
        {
            if (fileStream.Length > MaxFileSize)
            {
                return (false, "File size exceeds 10MB limit");
            }

            if (!ValidateXmlFile(fileStream))
            {
                return (false, "Invalid XML file structure");
            }

            fileStream.Position = 0;
            var (organization, id) = ParseProjectId(projectId);
            var projectPath = Path.Combine("/app/data/input", organization, "projects", id);
            Directory.CreateDirectory(projectPath);

            var filePath = Path.Combine(projectPath, fileName);
            using (var fileStreamOut = File.Create(filePath))
            {
                await fileStream.CopyToAsync(fileStreamOut);
            }

            _logger.LogInformation("Uploaded XML file {FileName} to project {Organization}/{ProjectId}", fileName, organization, id);
            return (true, $"Successfully uploaded {fileName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading XML file {FileName} to project {ProjectId}", fileName, projectId);
            return (false, $"Upload failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> UploadXsltFileAsync(Stream fileStream)
    {
        try
        {
            if (fileStream.Length > MaxFileSize)
            {
                return (false, "File size exceeds 10MB limit");
            }

            if (!ValidateXsltFile(fileStream))
            {
                return (false, "Invalid XSLT file structure");
            }

            fileStream.Position = 0;
            var xsltPath = "/app/xslt/transformation.xslt";

            // Backup existing XSLT
            if (File.Exists(xsltPath))
            {
                var backupPath = $"{xsltPath}.backup.{DateTime.Now:yyyyMMddHHmmss}";
                File.Copy(xsltPath, backupPath);
                _logger.LogInformation("Backed up existing XSLT to {BackupPath}", backupPath);
            }

            using (var fileStreamOut = File.Create(xsltPath))
            {
                await fileStream.CopyToAsync(fileStreamOut);
            }

            _logger.LogInformation("Uploaded new XSLT template");
            return (true, "Successfully uploaded XSLT template (previous version backed up)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading XSLT file");
            return (false, $"Upload failed: {ex.Message}");
        }
    }

    public async Task<byte[]> GenerateZipArchiveAsync(string projectId)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var (organization, id) = ParseProjectId(projectId);
            var outputPath = Path.Combine("/app/data/output", organization, "projects", id);
            if (Directory.Exists(outputPath))
            {
                foreach (var file in Directory.GetFiles(outputPath))
                {
                    var entry = archive.CreateEntry(Path.GetFileName(file));
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(file);
                    await fileStream.CopyToAsync(entryStream);
                }
            }
        }

        return memoryStream.ToArray();
    }

    public async Task<byte[]> GenerateAllResultsZipAsync()
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var outputBasePath = "/app/data/output";
            if (Directory.Exists(outputBasePath))
            {
                // Iterate through organization directories
                foreach (var orgDir in Directory.GetDirectories(outputBasePath))
                {
                    var organization = Path.GetFileName(orgDir);
                    if (organization.StartsWith('.'))
                        continue;

                    var projectsPath = Path.Combine(orgDir, "projects");
                    if (!Directory.Exists(projectsPath))
                        continue;

                    // Iterate through project directories
                    foreach (var projectDir in Directory.GetDirectories(projectsPath))
                    {
                        var projectId = Path.GetFileName(projectDir);
                        foreach (var file in Directory.GetFiles(projectDir))
                        {
                            var entryName = $"{organization}/{projectId}/{Path.GetFileName(file)}";
                            var entry = archive.CreateEntry(entryName);
                            using var entryStream = entry.Open();
                            using var fileStream = File.OpenRead(file);
                            await fileStream.CopyToAsync(entryStream);
                        }
                    }
                }
            }
        }

        return memoryStream.ToArray();
    }

    public async Task<byte[]> GetTransformationLogsAsync()
    {
        var logPath = "/app/logs";
        if (!Directory.Exists(logPath))
        {
            return Array.Empty<byte>();
        }

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var logFile in Directory.GetFiles(logPath, "*.log"))
            {
                var entry = archive.CreateEntry(Path.GetFileName(logFile));
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(logFile);
                await fileStream.CopyToAsync(entryStream);
            }
        }

        return memoryStream.ToArray();
    }

    public bool ValidateXmlFile(Stream fileStream)
    {
        try
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream, leaveOpen: true);
            var content = reader.ReadToEnd();
            fileStream.Position = 0;

            // Basic XML validation
            var doc = new XmlDocument();
            doc.LoadXml(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ValidateXsltFile(Stream fileStream)
    {
        try
        {
            fileStream.Position = 0;
            using var reader = new StreamReader(fileStream, leaveOpen: true);
            var content = reader.ReadToEnd();
            fileStream.Position = 0;

            // Basic XSLT validation - check for xsl:stylesheet element
            return content.Contains("<xsl:stylesheet") || content.Contains("<xsl:transform");
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<string> GetXsltFiles()
    {
        try
        {
            var xsltBasePath = "/app/xslt";
            if (!Directory.Exists(xsltBasePath))
            {
                _logger.LogWarning("XSLT directory not found at {Path}", xsltBasePath);
                return Enumerable.Empty<string>();
            }

            // Get all .xslt files recursively and return relative paths
            var files = Directory.GetFiles(xsltBasePath, "*.xslt", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(xsltBasePath, f))
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation("Found {Count} XSLT files", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving XSLT files");
            return Enumerable.Empty<string>();
        }
    }

    public async Task<(bool Success, string Message, int AmpersandCount, int LessThanCount, int GreaterThanCount)> FixInvalidXmlCharactersAsync(string projectId, string fileName)
    {
        try
        {
            var (organization, id) = ParseProjectId(projectId);
            var filePath = Path.Combine("/app/data/input", organization, "projects", id, fileName);

            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {Path}", filePath);
                return (false, "File not found", 0, 0, 0);
            }

            var content = await File.ReadAllTextAsync(filePath);
            var originalContent = content;

            // Multi-pass approach to handle each character type separately
            // Pass 1: Fix standalone '&' not already part of an entity
            // Matches '&' not followed by alphanumeric/# and semicolon (entity pattern)
            var ampersandRegex = new System.Text.RegularExpressions.Regex(@"&(?![a-zA-Z0-9#]+;)");
            var ampersandMatches = ampersandRegex.Matches(content);
            var ampersandCount = ampersandMatches.Count;

            if (ampersandCount > 0)
            {
                content = ampersandRegex.Replace(content, "&amp;");
                _logger.LogInformation("Fixed {Count} standalone ampersands in {FileName}", ampersandCount, fileName);
            }

            // Pass 2: Fix standalone '<' not part of valid XML tags
            // Matches '<' NOT followed by '?', '!', '/' or a letter (not a valid opening/closing tag, processing instruction, or comment)
            var lessThanRegex = new System.Text.RegularExpressions.Regex(@"<(?![?!/a-zA-Z])");
            var lessThanMatches = lessThanRegex.Matches(content);
            var lessThanCount = lessThanMatches.Count;

            if (lessThanCount > 0)
            {
                content = lessThanRegex.Replace(content, "&lt;");
                _logger.LogInformation("Fixed {Count} standalone less-than signs in {FileName}", lessThanCount, fileName);
            }

            // Pass 3: Fix standalone '>' not part of valid XML tags
            // Matches '>' NOT preceded by '?', '/', '-', letters, digits, or quotes (not a valid tag, processing instruction end, or comment end)
            // This is more conservative to avoid breaking valid tags
            var greaterThanRegex = new System.Text.RegularExpressions.Regex(@"(?<![?/\-a-zA-Z0-9""])>");
            var greaterThanMatches = greaterThanRegex.Matches(content);
            var greaterThanCount = greaterThanMatches.Count;

            if (greaterThanCount > 0)
            {
                content = greaterThanRegex.Replace(content, "&gt;");
                _logger.LogInformation("Fixed {Count} standalone greater-than signs in {FileName}", greaterThanCount, fileName);
            }

            var totalFixed = ampersandCount + lessThanCount + greaterThanCount;

            // Only write to file if changes were made
            if (content != originalContent)
            {
                await File.WriteAllTextAsync(filePath, content);
                _logger.LogInformation(
                    "Fixed {AmpersandCount} ampersands, {LessThanCount} less-than signs, {GreaterThanCount} greater-than signs (total: {TotalFixed}) in {FileName}",
                    ampersandCount, lessThanCount, greaterThanCount, totalFixed, fileName);
            }
            else
            {
                _logger.LogInformation("No invalid XML characters found in {FileName}", fileName);
            }

            // Build detailed message
            string message;
            if (totalFixed == 0)
            {
                message = "No invalid characters found";
            }
            else
            {
                var parts = new List<string>();
                if (ampersandCount > 0) parts.Add($"{ampersandCount} ampersand{(ampersandCount == 1 ? "" : "s")}");
                if (lessThanCount > 0) parts.Add($"{lessThanCount} less-than sign{(lessThanCount == 1 ? "" : "s")}");
                if (greaterThanCount > 0) parts.Add($"{greaterThanCount} greater-than sign{(greaterThanCount == 1 ? "" : "s")}");

                message = $"Fixed {string.Join(", ", parts)} ({totalFixed} total)";
            }

            return (true, message, ampersandCount, lessThanCount, greaterThanCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fixing invalid XML characters in {FileName} for project {ProjectId}", fileName, projectId);
            return (false, $"Error: {ex.Message}", 0, 0, 0);
        }
    }

    /// <summary>
    /// Creates a sanitized version of an XML file with lorem ipsum placeholder text.
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <param name="fileName">The source XML file name</param>
    /// <returns>Success status and result message</returns>
    public async Task<(bool Success, string Message)> SanitizeXmlFileAsync(string projectId, string fileName)
    {
        try
        {
            _logger.LogInformation("Starting sanitization for {FileName} in project {ProjectId}", fileName, projectId);

            // Check if XSLT3Service is available
            if (_xslt3Client == null)
            {
                var message = "XSLT3Service is not available - cannot perform sanitization";
                _logger.LogWarning(message);
                return (false, message);
            }

            // Build source file path
            var (organization, id) = ParseProjectId(projectId);
            var sourcePath = Path.Combine("/app/data/input", organization, "projects", id, fileName);

            if (!File.Exists(sourcePath))
            {
                var message = $"Source file not found: {fileName}";
                _logger.LogWarning(message);
                return (false, message);
            }

            // Read source XML content
            var xmlContent = await File.ReadAllTextAsync(sourcePath);

            // Read lorem ipsum XSLT content
            var xsltPath = "/app/xslt/_system/lorem_replace_text.xsl";
            if (!File.Exists(xsltPath))
            {
                var message = "Lorem ipsum XSLT template not found";
                _logger.LogError(message);
                return (false, message);
            }

            var xsltContent = await File.ReadAllTextAsync(xsltPath);

            // Transform using lorem ipsum XSLT
            var transformResult = await _xslt3Client.TransformAsync(
                xmlContent,
                xsltContent,
                new Dictionary<string, string>() // No parameters needed
            );

            if (!transformResult.IsSuccess)
            {
                var message = $"Sanitization failed: {transformResult.ErrorMessage}";
                _logger.LogError(message);
                return (false, message);
            }

            // Generate output filename: insert -lorem before extension
            var outputFileName = GenerateLoremFileName(fileName);
            var outputPath = Path.Combine("/app/data/input", organization, "projects", id, outputFileName);

            // Write sanitized XML
            await File.WriteAllTextAsync(outputPath, transformResult.OutputContent ?? string.Empty);

            var successMessage = $"Successfully created lorem version: {outputFileName}";
            _logger.LogInformation(successMessage);
            return (true, successMessage);
        }
        catch (Exception ex)
        {
            var message = $"Failed to sanitize XML: {ex.Message}";
            _logger.LogError(ex, "Error during XML sanitization");
            return (false, message);
        }
    }

    /// <summary>
    /// Generates output filename by inserting -lorem before file extension.
    /// </summary>
    /// <param name="fileName">Original filename (e.g., "report.xml")</param>
    /// <returns>Lorem filename (e.g., "report-lorem.xml")</returns>
    private string GenerateLoremFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName); // ".xml"
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName); // "report"
        return $"{nameWithoutExtension}-lorem{extension}"; // "report-lorem.xml"
    }

    private (string organization, string projectId) ParseProjectId(string projectId)
    {
        // Support both formats: "organization/projectId" and legacy "projectId"
        if (projectId.Contains('/'))
        {
            var parts = projectId.Split('/', 2);
            return (parts[0], parts[1]);
        }

        // Legacy format - assume optiver for backward compatibility
        return ("optiver", projectId);
    }
}

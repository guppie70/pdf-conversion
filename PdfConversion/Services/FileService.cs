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
}

public class FileService : IFileService
{
    private readonly IProjectManagementService _projectService;
    private readonly ILogger<FileService> _logger;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

    public FileService(IProjectManagementService projectService, ILogger<FileService> logger)
    {
        _projectService = projectService;
        _logger = logger;
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
            var projectPath = $"/app/data/input/optiver/projects/{projectId}";
            Directory.CreateDirectory(projectPath);

            var filePath = Path.Combine(projectPath, fileName);
            using (var fileStreamOut = File.Create(filePath))
            {
                await fileStream.CopyToAsync(fileStreamOut);
            }

            _logger.LogInformation("Uploaded XML file {FileName} to project {ProjectId}", fileName, projectId);
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
            var outputPath = $"/app/data/output/optiver/projects/{projectId}";
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
            var outputBasePath = "/app/data/output/optiver/projects";
            if (Directory.Exists(outputBasePath))
            {
                foreach (var projectDir in Directory.GetDirectories(outputBasePath))
                {
                    var projectId = Path.GetFileName(projectDir);
                    foreach (var file in Directory.GetFiles(projectDir))
                    {
                        var entryName = $"{projectId}/{Path.GetFileName(file)}";
                        var entry = archive.CreateEntry(entryName);
                        using var entryStream = entry.Open();
                        using var fileStream = File.OpenRead(file);
                        await fileStream.CopyToAsync(entryStream);
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
}

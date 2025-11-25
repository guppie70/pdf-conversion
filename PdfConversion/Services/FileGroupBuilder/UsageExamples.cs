using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Usage examples showing how different pages would use the new FileGroupBuilderService.
/// This file is for documentation purposes and shows migration patterns.
/// </summary>
public static class FileGroupBuilderUsageExamples
{
    /// <summary>
    /// Transform.razor - Shows XML files from source and normalized folders for transformation.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> TransformPageExample(IFileGroupBuilderService service)
    {
        // NEW WAY - Using fluent query builder
        var fileGroups = await service.CreateQuery()
            .UseInputBase(true)                    // Search in input directory
            .SourceXmlFiles()                      // XML files from source/ folder
            .NormalizedFiles()                     // XHTML/XML files from normalized/ folder
            .RootXmlFiles()                        // XML files from project root
            .OnlyActiveProjects(false)             // Include all projects
            .ForCustomer("optiver")                // Optional: filter by customer
            .ForProject("ar24-3")                  // Optional: filter by project
            .BuildAsync();

        // Alternative: More explicit configuration
        var explicitGroups = await service.CreateQuery()
            .UseInputBase(true)
            .FromProjectPath("source")             // Look in source/ folder
            .WithExtensions(".xml")                // Only XML files
            .FromProjectPath("normalized")         // Also look in normalized/ folder
            .WithExtensions(".xml", ".xhtml")      // XML and XHTML files
            .FromProjectPath("")                   // Also check root
            .WithExtensions(".xml")                // XML files in root
            .BuildAsync();

        return fileGroups;
    }

    /// <summary>
    /// DoclingConvert.razor - Shows PDF and Word documents for conversion.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> DoclingConvertPageExample(IFileGroupBuilderService service)
    {
        // NEW WAY - Using preset
        var fileGroups = await service.CreateQuery()
            .DocumentFiles()                       // Preset for PDF/DOCX/DOC files
            .OnlyActiveProjects(false)
            .BuildAsync();

        // Alternative: Custom document types
        var customDocs = await service.CreateQuery()
            .UseInputBase(true)
            .FromProjectPath("")                   // Look in project root
            .WithExtensions(".pdf", ".docx", ".doc", ".pptx", ".xlsx")
            .BuildAsync();

        return fileGroups;
    }

    /// <summary>
    /// Convert.razor - Shows normalized XML files for section generation.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> ConvertPageExample(IFileGroupBuilderService service)
    {
        // NEW WAY - Multiple sources
        var fileGroups = await service.CreateQuery()
            .UseInputBase(true)
            .NormalizedFiles()                     // Normalized folder files
            .SourceXmlFiles()                      // Source folder files
            .RootXmlFiles()                        // Root XML files
            .BuildAsync();

        return fileGroups;
    }

    /// <summary>
    /// GenerateHierarchy.razor - Shows source XML for hierarchy generation.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> GenerateHierarchyPageExample(IFileGroupBuilderService service)
    {
        // NEW WAY - Source and normalized files
        var fileGroups = await service.CreateQuery()
            .UseInputBase(true)
            .SourceXmlFiles()                      // Source XML files
            .NormalizedFiles()                     // Normalized files
            .OnlyActiveProjects(false)
            .BuildAsync();

        return fileGroups;
    }

    /// <summary>
    /// Advanced example: Finding specific pattern files.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> AdvancedPatternExample(IFileGroupBuilderService service)
    {
        // Find all normalized XML files matching specific pattern
        var normalizedReports = await service.CreateQuery()
            .UseInputBase(true)
            .FromProjectPath("normalized")
            .WithPattern("report-*.xml")           // Only files matching pattern
            .BuildAsync();

        // Find files with regex
        var versionedFiles = await service.CreateQuery()
            .UseInputBase(true)
            .FromProjectPath("")
            .WithRegex(@"v\d+\.xml$")             // Files ending with v1.xml, v2.xml, etc.
            .BuildAsync();

        // Exclude backup files
        var nonBackupFiles = await service.CreateQuery()
            .UseInputBase(true)
            .WithExtensions(".xml")
            .ExcludePattern("*.backup")            // Exclude backup files
            .ExcludePattern("temp-*")              // Exclude temp files
            .BuildAsync();

        return normalizedReports;
    }

    /// <summary>
    /// Advanced example: Custom filtering logic.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> CustomFilterExample(IFileGroupBuilderService service)
    {
        // Files modified in last 24 hours
        var recentFiles = await service.CreateQuery()
            .UseInputBase(true)
            .WithExtensions(".xml")
            .WithFilter(filePath =>
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.LastWriteTime > DateTime.Now.AddDays(-1);
            })
            .BuildAsync();

        // Large XML files only (> 1MB)
        var largeFiles = await service.CreateQuery()
            .UseInputBase(true)
            .WithExtensions(".xml")
            .WithFilter(filePath =>
            {
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Length > 1024 * 1024; // 1MB
            })
            .BuildAsync();

        return recentFiles;
    }

    /// <summary>
    /// Example: Getting output files (hierarchy, sections).
    /// </summary>
    public static async Task<List<ProjectFileGroup>> OutputFilesExample(IFileGroupBuilderService service)
    {
        // Get hierarchy and normalized files from output
        var outputFiles = await service.CreateQuery()
            .UseOutputBase(true)                   // Use output directory
            .UseInputBase(false)                   // Don't search input
            .OutputXmlFiles()                       // Preset for output XML files
            .BuildAsync();

        // Get section files
        var sectionFiles = await service.CreateQuery()
            .SectionFiles()                        // Preset for section files
            .ForCustomer("optiver")
            .BuildAsync();

        return outputFiles;
    }

    /// <summary>
    /// Example: Getting flat file list without grouping.
    /// </summary>
    public static async Task<List<FileResult>> FlatFileListExample(IFileGroupBuilderService service)
    {
        // Get all XML files as flat list with metadata
        var allXmlFiles = await service.CreateQuery()
            .UseInputBase(true)
            .WithExtensions(".xml")
            .GetFilesAsync();                     // Returns FileResult list instead of groups

        // Files can be sorted, filtered, etc.
        var sortedByDate = allXmlFiles
            .OrderByDescending(f => f.LastModified)
            .Take(10)
            .ToList();

        var largestFiles = allXmlFiles
            .OrderByDescending(f => f.SizeInBytes)
            .Take(5)
            .ToList();

        return allXmlFiles;
    }

    /// <summary>
    /// Example: Multiple folder sources with different filters.
    /// </summary>
    public static async Task<List<ProjectFileGroup>> MultipleFolderExample(IFileGroupBuilderService service)
    {
        // Complex query combining multiple sources with different filters
        var complexQuery = await service.CreateQuery()
            .UseInputBase(true)

            // XML files from source folder
            .FromProjectPath("source")
            .WithExtensions(".xml")

            // XHTML files from normalized folder
            .FromProjectPath("normalized")
            .WithExtensions(".xhtml")

            // PDF files from root (for reference)
            .FromProjectPath("")
            .WithExtensions(".pdf")

            // Custom folder with specific pattern
            .FromProjectPath("archives")
            .WithPattern("backup-*.xml")

            .OnlyActiveProjects(true)
            .BuildAsync();

        return complexQuery;
    }

    /// <summary>
    /// Migration helper: Shows how to migrate from old method calls.
    /// </summary>
    public static class MigrationExamples
    {
        // OLD WAY:
        // await FileGroupBuilder.BuildXmlFileGroupsAsync(
        //     includeInputFiles: true,
        //     includeOutputFiles: false,
        //     onlyActiveProjects: false,
        //     customer: "optiver",
        //     projectId: "ar24-3",
        //     includeNormalizedFolder: false);

        // NEW WAY:
        public static async Task<List<ProjectFileGroup>> MigrateOldXmlCall(IFileGroupBuilderService service)
        {
            return await service.CreateQuery()
                .UseInputBase(true)                // includeInputFiles
                .UseOutputBase(false)               // includeOutputFiles
                .RootXmlFiles()                     // Root XML files
                .SourceXmlFiles()                   // Source folder files
                // Note: NOT including normalized (includeNormalizedFolder: false)
                .OnlyActiveProjects(false)          // onlyActiveProjects
                .ForCustomer("optiver")             // customer filter
                .ForProject("ar24-3")               // project filter
                .BuildAsync();
        }

        // OLD WAY:
        // await FileGroupBuilder.BuildDocumentFileGroupsAsync(
        //     extensions: new[] { ".pdf", ".docx" },
        //     onlyActiveProjects: true);

        // NEW WAY:
        public static async Task<List<ProjectFileGroup>> MigrateOldDocumentCall(IFileGroupBuilderService service)
        {
            return await service.CreateQuery()
                .UseInputBase(true)
                .FromProjectPath("")
                .WithExtensions(".pdf", ".docx")
                .OnlyActiveProjects(true)
                .BuildAsync();
        }
    }
}
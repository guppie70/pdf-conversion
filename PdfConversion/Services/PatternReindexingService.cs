using System.Text.Json;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Service for re-indexing pattern training material
/// </summary>
public interface IPatternReindexingService
{
    Task<ReindexingResult> ReindexPatternsAsync(IProgress<string> progress, CancellationToken cancellationToken = default);
}

public class PatternReindexingService : IPatternReindexingService
{
    private readonly ILogger<PatternReindexingService> _logger;
    private readonly PatternLearningService _patternLearningService;
    private const string TrainingDirectory = "data/training-material/hierarchies";
    private const string OutputPath = "data/patterns/learned-rules.json";

    public PatternReindexingService(
        ILogger<PatternReindexingService> logger,
        PatternLearningService patternLearningService)
    {
        _logger = logger;
        _patternLearningService = patternLearningService;
    }

    public async Task<ReindexingResult> ReindexPatternsAsync(IProgress<string> progress, CancellationToken cancellationToken = default)
    {
        var result = new ReindexingResult();
        var startTime = DateTime.UtcNow;

        try
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Starting re-indexing process...");

            // Step 1: Validate training directory
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Checking training directory: {TrainingDirectory}");

            if (!Directory.Exists(TrainingDirectory))
            {
                var errorMsg = $"Training directory not found: {TrainingDirectory}";
                progress?.Report($"[{DateTime.Now:HH:mm:ss}] ERROR: {errorMsg}");
                result.Success = false;
                result.ErrorMessage = errorMsg;
                return result;
            }

            // Step 2: Count hierarchy files
            var files = Directory.GetFiles(TrainingDirectory, "*.xml", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).StartsWith("hierarchy", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Found {files.Length} hierarchy files across {GetCustomerCount(files)} customers");
            result.FilesProcessed = files.Length;

            if (files.Length == 0)
            {
                var errorMsg = "No hierarchy files found in training directory";
                progress?.Report($"[{DateTime.Now:HH:mm:ss}] WARNING: {errorMsg}");
                result.Success = false;
                result.ErrorMessage = errorMsg;
                return result;
            }

            // Step 3: Analyze patterns
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Analyzing patterns from training hierarchies...");
            var database = await _patternLearningService.AnalyzeTrainingHierarchies(TrainingDirectory);

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Pattern analysis complete");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Total items analyzed: {database.TotalItemsAnalyzed}");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Level profiles extracted: {database.LevelProfiles.Count}");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Section patterns found: {database.CommonSections.Count}");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Sequence patterns found: {database.TypicalSequences.Count}");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Numbering patterns found: {database.NumberingPatterns.Count}");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}]   - Parent-child patterns found: {database.ParentChildPatterns.Count}");

            result.ItemsAnalyzed = database.TotalItemsAnalyzed;
            result.PatternsExtracted = database.CommonSections.Count;

            // Step 4: Ensure output directory exists
            var outputDir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                progress?.Report($"[{DateTime.Now:HH:mm:ss}] Created output directory: {outputDir}");
            }

            // Step 5: Save pattern database
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Saving pattern database to: {OutputPath}");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(database, options);
            await File.WriteAllTextAsync(OutputPath, json, cancellationToken);

            var fileInfo = new FileInfo(OutputPath);
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Pattern database saved ({FormatFileSize(fileInfo.Length)})");

            // Step 6: Complete
            var duration = DateTime.UtcNow - startTime;
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ✓ Re-indexing complete in {duration.TotalSeconds:F2} seconds");
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] NOTE: Restart the application to load the new patterns");

            result.Success = true;
            result.DurationSeconds = duration.TotalSeconds;

            _logger.LogInformation("Pattern re-indexing completed successfully: {Files} files, {Items} items, {Patterns} patterns in {Duration}s",
                result.FilesProcessed, result.ItemsAnalyzed, result.PatternsExtracted, result.DurationSeconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] Re-indexing cancelled by user");
            result.Success = false;
            result.ErrorMessage = "Operation cancelled";
            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Unexpected error: {ex.Message}";
            progress?.Report($"[{DateTime.Now:HH:mm:ss}] ERROR: {errorMsg}");
            _logger.LogError(ex, "Failed to re-index patterns");

            result.Success = false;
            result.ErrorMessage = errorMsg;
            return result;
        }
    }

    private static int GetCustomerCount(string[] files)
    {
        return files
            .Select(f => GetCustomerName(f))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .Count();
    }

    private static string? GetCustomerName(string filePath)
    {
        // Extract customer name from path like: data/training-material/hierarchies/optiver/hierarchy.xml
        var parts = filePath.Split(Path.DirectorySeparatorChar);
        var hierarchiesIndex = Array.IndexOf(parts, "hierarchies");

        if (hierarchiesIndex >= 0 && hierarchiesIndex < parts.Length - 1)
        {
            return parts[hierarchiesIndex + 1];
        }

        return null;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}

public class ReindexingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int FilesProcessed { get; set; }
    public int ItemsAnalyzed { get; set; }
    public int PatternsExtracted { get; set; }
    public double DurationSeconds { get; set; }
}

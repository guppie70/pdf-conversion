using PdfConversion.Models;
using PdfConversion.Utils;
using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for orchestrating round-trip validation of document splitting
/// </summary>
public interface IRoundTripValidationService
{
    /// <summary>
    /// Validates that sections can be reconstructed back to the original normalized XML
    /// </summary>
    /// <param name="projectId">The project identifier (format: "customer/projectId")</param>
    /// <param name="hierarchyFile">The hierarchy XML filename</param>
    /// <returns>Validation result with diff information</returns>
    Task<RoundTripValidationResult> ValidateRoundTripAsync(string projectId, string sourceFile, string hierarchyFile);
}

/// <summary>
/// Implementation of round-trip validation service
/// </summary>
public class RoundTripValidationService : IRoundTripValidationService
{
    private readonly ILogger<RoundTripValidationService> _logger;
    private readonly IDocumentReconstructionService _reconstructionService;
    private readonly IDiffService _diffService;
    private readonly IXsltTransformationService _xsltService;
    private readonly IProjectManagementService _projectService;

    public RoundTripValidationService(
        ILogger<RoundTripValidationService> logger,
        IDocumentReconstructionService reconstructionService,
        IDiffService diffService,
        IXsltTransformationService xsltService,
        IProjectManagementService projectService)
    {
        _logger = logger;
        _reconstructionService = reconstructionService;
        _diffService = diffService;
        _xsltService = xsltService;
        _projectService = projectService;
    }

    /// <summary>
    /// Normalizes XML for comparison by removing insignificant whitespace and applying consistent formatting
    /// </summary>
    /// <param name="xml">The XML string to normalize</param>
    /// <returns>Normalized XML string with consistent formatting</returns>
    private string NormalizeXmlForComparison(string xml)
    {
        try
        {
            _logger.LogDebug("Normalizing XML for comparison (size: {Size} chars)", xml.Length);

            // Parse the XML without preserving insignificant whitespace
            var document = XDocument.Parse(xml, LoadOptions.None);

            // Re-serialize with consistent pretty-printing
            // SaveOptions.None = pretty-printed with indentation
            var normalizedXml = document.ToString(SaveOptions.None);

            _logger.LogDebug("XML normalized successfully (new size: {Size} chars)", normalizedXml.Length);
            return normalizedXml;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to normalize XML, using original content. XML preview: {Preview}",
                xml.Length > 200 ? xml.Substring(0, 200) + "..." : xml);
            return xml;
        }
    }

    public async Task<RoundTripValidationResult> ValidateRoundTripAsync(string projectId, string sourceFile, string hierarchyFile)
    {
        var startTime = DateTime.UtcNow;
        var result = new RoundTripValidationResult();

        try
        {
            _logger.LogInformation("Starting round-trip validation for project {ProjectId}", projectId);

            // Extract customer and project from projectId format "customer/projectId"
            var projectParts = projectId.Split('/', 2);
            if (projectParts.Length != 2)
            {
                result.Success = false;
                result.ErrorMessage = $"Invalid projectId format: {projectId}. Expected 'customer/projectId'";
                _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);
                return result;
            }
            var customer = projectParts[0];
            var project = projectParts[1];

            // 1. Define file paths
            var hierarchyXmlPath = Path.Combine("/app/data/input", customer, "projects", project, "metadata", hierarchyFile);
            var sectionsDirectory = Path.Combine("/app/data/output", customer, "projects", project, "data");

            // Debug output directory
            var debugOutputDirectory = Path.Combine("/app/data/output", customer, "projects", project, "debug");
            Directory.CreateDirectory(debugOutputDirectory);
            _logger.LogInformation("Debug files will be saved to: {DebugDirectory}", debugOutputDirectory);

            // 2. Validate hierarchy and sections directory exist
            if (!File.Exists(hierarchyXmlPath))
            {
                result.Success = false;
                result.ErrorMessage = $"Hierarchy XML file not found at: {hierarchyXmlPath}";
                _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);
                return result;
            }

            if (!Directory.Exists(sectionsDirectory))
            {
                result.Success = false;
                result.ErrorMessage = $"Sections directory not found at: {sectionsDirectory}";
                _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);
                return result;
            }

            // 3. Validate source file exists
            _logger.LogInformation("Using source file: {SourceFile}", sourceFile);

            var sourceFilePath = Path.Combine("/app/data/input", customer, "projects", project, sourceFile);
            if (!File.Exists(sourceFilePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Source XML file not found at: {sourceFilePath}";
                _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);
                return result;
            }

            // 4. Get original XML content
            // Read source XML content
            var sourceXmlContent = await _projectService.ReadInputFileAsync(projectId, sourceFile);
            _logger.LogInformation("Source XML size: {Size} characters", sourceXmlContent.Length);

            string originalXml;

            // Check if source file is already normalized (in normalized/ folder)
            // If so, use it directly instead of re-transforming
            if (sourceFile.StartsWith("normalized/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Source file is already normalized ({SourceFile}), using content directly without XSLT transformation", sourceFile);
                originalXml = sourceXmlContent;
            }
            else
            {
                // Perform fresh XSLT transformation for non-normalized source files
                _logger.LogInformation("Performing fresh XSLT transformation on source XML");

                // Auto-detect XSLT based on source filename
                var xsltPath = XsltPathResolver.GetTransformationPath(sourceFile);

                _logger.LogInformation("Using XSLT file: {XsltPath} (detected from source: {SourceFile})", xsltPath, sourceFile);

                if (!File.Exists(xsltPath))
                {
                    result.Success = false;
                    result.ErrorMessage = $"XSLT transformation file not found: {xsltPath}";
                    _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);
                    return result;
                }

                var xsltContent = await File.ReadAllTextAsync(xsltPath);

                // Configure transformation options
                var transformOptions = new TransformationOptions
                {
                    UseXslt3Service = true,
                    NormalizeHeaders = false, // Headers are normalized per section
                    Parameters = new Dictionary<string, string>
                    {
                        { "project-id", projectId },
                        { "file-name", sourceFile },
                        { "generate-ids", "true" }
                    }
                };

                _logger.LogInformation("XSLT transformation options: UseXslt3Service={UseXslt3}, NormalizeHeaders={NormalizeHeaders}, Parameters={Parameters}",
                    transformOptions.UseXslt3Service,
                    transformOptions.NormalizeHeaders,
                    string.Join(", ", transformOptions.Parameters.Select(p => $"{p.Key}={p.Value}")));

                // Perform transformation (pass xsltPath so includes can be resolved)
                _logger.LogInformation("Starting XSLT transformation - Source size: {Size} chars, XSLT size: {XsltSize} chars",
                    sourceXmlContent.Length, xsltContent.Length);

                var transformResult = await _xsltService.TransformAsync(sourceXmlContent, xsltContent, transformOptions, xsltPath);

                _logger.LogInformation("XSLT transformation result - Success: {Success}, Output size: {Size} chars, Error: {Error}",
                    transformResult.IsSuccess,
                    transformResult.OutputContent?.Length ?? 0,
                    transformResult.ErrorMessage ?? "none");

                if (!transformResult.IsSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = $"XSLT transformation failed: {transformResult.ErrorMessage}";
                    _logger.LogWarning("Validation failed: {Error}", result.ErrorMessage);

                    // Save error details for debugging
                    var errorLogPath = Path.Combine(debugOutputDirectory, "transformation-error.txt");
                    await File.WriteAllTextAsync(errorLogPath, $"Transformation Error:\n{transformResult.ErrorMessage}");
                    _logger.LogInformation("Error details saved to: {ErrorLogPath}", errorLogPath);

                    return result;
                }

                originalXml = transformResult.OutputContent;
                _logger.LogInformation("Fresh transformation completed successfully, output size: {Size} characters", originalXml.Length);
            }

            // DEBUG: Save fresh transformation output
            var freshTransformPath = Path.Combine(debugOutputDirectory, "1-fresh-transformation.xml");
            await File.WriteAllTextAsync(freshTransformPath, originalXml);
            _logger.LogInformation("Saved fresh transformation output to: {Path}", freshTransformPath);

            // 5. Reconstruct document from sections
            _logger.LogInformation("Reconstructing document from sections");
            var reconstructionResult = await _reconstructionService.ReconstructNormalizedXmlWithDetailsAsync(
                hierarchyXmlPath,
                sectionsDirectory);
            var reconstructedXml = reconstructionResult.ReconstructedXml;
            _logger.LogInformation("Document reconstruction completed, output size: {Size} characters", reconstructedXml.Length);
            _logger.LogInformation("Template usage: {TemplatesUsed} sections used template content", reconstructionResult.TemplatesUsed);

            // DEBUG: Save reconstructed output
            var reconstructedPath = Path.Combine(debugOutputDirectory, "2-reconstructed-from-sections.xml");
            await File.WriteAllTextAsync(reconstructedPath, reconstructedXml);
            _logger.LogInformation("Saved reconstructed document to: {Path}", reconstructedPath);

            // Also write to _work directory for development context
            try
            {
                var workPath = "/app/data/_work";
                if (!Directory.Exists(workPath))
                {
                    Directory.CreateDirectory(workPath);
                }
                var contextRoundtripPath = Path.Combine(workPath, "_roundtrip.xml");
                await File.WriteAllTextAsync(contextRoundtripPath, reconstructedXml);
                _logger.LogInformation("Saved context roundtrip XML to {Path}", contextRoundtripPath);
            }
            catch (Exception workEx)
            {
                _logger.LogWarning(workEx, "Failed to save context roundtrip XML to _work folder");
                // Non-critical, continue
            }

            // 6. Normalize both XML documents before comparison
            _logger.LogInformation("Normalizing both XML documents for whitespace-insensitive comparison");
            var normalizedOriginalXml = NormalizeXmlForComparison(originalXml);
            var normalizedReconstructedXml = NormalizeXmlForComparison(reconstructedXml);

            // DEBUG: Save normalized versions for inspection
            var normalizedOriginalPath = Path.Combine(debugOutputDirectory, "3-normalized-original.xml");
            var normalizedReconstructedPath = Path.Combine(debugOutputDirectory, "4-normalized-reconstructed.xml");
            await File.WriteAllTextAsync(normalizedOriginalPath, normalizedOriginalXml);
            await File.WriteAllTextAsync(normalizedReconstructedPath, normalizedReconstructedXml);
            _logger.LogInformation("Saved normalized documents: {NormOrig} and {NormRecon}",
                normalizedOriginalPath, normalizedReconstructedPath);

            // 7. Generate diff
            _logger.LogInformation("Generating diff between normalized XML documents");
            var diffResult = _diffService.GenerateXmlDiff(normalizedOriginalXml, normalizedReconstructedXml, debugOutputDirectory);

            // DEBUG: Save what's being compared after body extraction (from DiffService internals)
            _logger.LogInformation("Diff generation complete");
            _logger.LogInformation("Diff statistics - Unchanged: {Unchanged}, Modified: {Modified}, Added: {Added}, Deleted: {Deleted}",
                diffResult.LinesUnchanged, diffResult.LinesModified, diffResult.LinesAdded, diffResult.LinesDeleted);

            // 8. Populate result
            result.Success = true;
            result.IsPerfectMatch = diffResult.IsPerfectMatch;
            result.MatchPercentage = diffResult.MatchPercentage;
            result.LinesAdded = diffResult.LinesAdded;
            result.LinesDeleted = diffResult.LinesDeleted;
            result.LinesModified = diffResult.LinesModified;
            result.LinesUnchanged = diffResult.LinesUnchanged;
            result.TotalDifferences = diffResult.TotalDifferences;
            result.DiffResult = diffResult;
            result.Duration = DateTime.UtcNow - startTime;
            result.DebugOutputDirectory = debugOutputDirectory;
            result.TemplatesUsed = reconstructionResult.TemplatesUsed;
            result.TemplateUsedForSections = reconstructionResult.TemplateUsedForSections;

            _logger.LogInformation("Round-trip validation complete: {Status}, Match: {Match}%",
                result.IsPerfectMatch ? "Perfect Match" : "Differences Found",
                result.MatchPercentage.ToString("F2"));
            _logger.LogInformation("Debug files saved to: {DebugDirectory}", debugOutputDirectory);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during round-trip validation for project {ProjectId}", projectId);
            result.Success = false;
            result.ErrorMessage = $"Validation error: {ex.Message}";
            result.Duration = DateTime.UtcNow - startTime;
            return result;
        }
    }
}

/// <summary>
/// Result of a round-trip validation operation
/// </summary>
public class RoundTripValidationResult
{
    /// <summary>
    /// Whether the validation operation completed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether the reconstructed document matches the original perfectly
    /// </summary>
    public bool IsPerfectMatch { get; set; }

    /// <summary>
    /// Percentage of lines that match (0-100)
    /// </summary>
    public double MatchPercentage { get; set; }

    /// <summary>
    /// Number of lines added in the reconstruction
    /// </summary>
    public int LinesAdded { get; set; }

    /// <summary>
    /// Number of lines deleted from the original
    /// </summary>
    public int LinesDeleted { get; set; }

    /// <summary>
    /// Number of lines that were modified
    /// </summary>
    public int LinesModified { get; set; }

    /// <summary>
    /// Number of lines that are unchanged
    /// </summary>
    public int LinesUnchanged { get; set; }

    /// <summary>
    /// Total number of differences
    /// </summary>
    public int TotalDifferences { get; set; }

    /// <summary>
    /// Detailed diff result
    /// </summary>
    public DiffResult? DiffResult { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to complete validation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Path to directory containing debug output files
    /// </summary>
    public string? DebugOutputDirectory { get; set; }

    /// <summary>
    /// Number of sections that used template placeholder content
    /// </summary>
    public int TemplatesUsed { get; set; }

    /// <summary>
    /// List of section information that used template content
    /// </summary>
    public List<TemplateSectionInfo> TemplateUsedForSections { get; set; } = new();

    /// <summary>
    /// Returns a formatted summary of the validation results
    /// </summary>
    public override string ToString() =>
        Success
            ? IsPerfectMatch
                ? $"Perfect Match! All lines match. Duration: {Duration.TotalSeconds:F1}s"
                : $"Differences Found: {TotalDifferences} differences ({MatchPercentage:F2}% match), Duration: {Duration.TotalSeconds:F1}s"
            : $"Validation Failed: {ErrorMessage}";
}

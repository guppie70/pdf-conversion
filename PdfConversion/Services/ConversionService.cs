using PdfConversion.Models;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PdfConversion.Services;

/// <summary>
/// Service for orchestrating the PDF to Taxxor section conversion process
/// </summary>
public interface IConversionService
{
    /// <summary>
    /// Loads hierarchy XML file and returns flattened list of items
    /// </summary>
    Task<List<HierarchyItem>> LoadHierarchyAsync(string path);

    /// <summary>
    /// Loads the template XML file from fixed location
    /// </summary>
    Task<XDocument> LoadTemplateAsync();

    /// <summary>
    /// Transforms source XML using existing XSLT pipeline
    /// </summary>
    Task<XDocument?> TransformSourceXmlAsync(string projectId, string sourceFile);

    /// <summary>
    /// Validates all files and configuration before conversion
    /// </summary>
    Task<ValidationResult> ValidateConversionAsync(string projectId, string sourceFile, string hierarchyFile);

    /// <summary>
    /// Prepares the output folder by cleaning up existing XML files or creating the directory structure
    /// </summary>
    Task PrepareOutputFolderAsync(string projectId);

    /// <summary>
    /// Matches hierarchy items to headers in the transformed XHTML document
    /// </summary>
    Task<List<HeaderMatch>> MatchHeadersAsync(XDocument transformedXhtml, List<HierarchyItem> hierarchyItems);

    /// <summary>
    /// Starts the complete conversion workflow from PDF to Taxxor sections
    /// </summary>
    /// <param name="projectId">The project identifier</param>
    /// <param name="sourceFile">The source XML file name</param>
    /// <param name="hierarchyFile">The hierarchy XML file name</param>
    /// <param name="logCallback">Callback for real-time logging to UI</param>
    /// <param name="duplicateSelectionCallback">Callback for user selection when duplicate headers are found.
    /// Parameters: duplicateMatches, transformedDocument, processedMatches, allHierarchyItems, currentHierarchyItem</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Conversion result with statistics and created files</returns>
    Task<ConversionResult> StartConversionAsync(
        string projectId,
        string sourceFile,
        string hierarchyFile,
        Action<string> logCallback,
        Func<List<HeaderMatch>, XDocument, List<HeaderMatch>, List<HierarchyItem>, HierarchyItem, Task<HeaderMatch?>>? duplicateSelectionCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of conversion service
/// </summary>
public class ConversionService : IConversionService
{
    private readonly ILogger<ConversionService> _logger;
    private readonly IXsltTransformationService _xsltService;
    private readonly IProjectManagementService _projectService;
    private readonly IHeaderMatchingService _headerMatchingService;
    private readonly IContentExtractionService _contentExtractionService;
    private readonly IHeaderNormalizationService _headerNormalizationService;
    private const string TemplateFilePath = "/app/data/input/template.xml";

    public ConversionService(
        ILogger<ConversionService> logger,
        IXsltTransformationService xsltService,
        IProjectManagementService projectService,
        IHeaderMatchingService headerMatchingService,
        IContentExtractionService contentExtractionService,
        IHeaderNormalizationService headerNormalizationService)
    {
        _logger = logger;
        _xsltService = xsltService;
        _projectService = projectService;
        _headerMatchingService = headerMatchingService;
        _contentExtractionService = contentExtractionService;
        _headerNormalizationService = headerNormalizationService;
    }

    public async Task<List<HierarchyItem>> LoadHierarchyAsync(string path)
    {
        try
        {
            _logger.LogInformation("Loading hierarchy from {Path}", path);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Hierarchy file not found: {path}");
            }

            var content = await File.ReadAllTextAsync(path);
            var doc = XDocument.Parse(content);

            // Check for default namespace
            var defaultNs = doc.Root?.GetDefaultNamespace();
            XNamespace ns = defaultNs ?? XNamespace.None;

            // Use XPath to get all items: /items/structured/item/sub_items//item
            // This flattens the nested structure
            var items = new List<HierarchyItem>();

            // Start from structured element
            var structuredElement = doc.Root?.Element(ns + "structured");
            if (structuredElement == null)
            {
                throw new InvalidOperationException("Hierarchy XML missing <structured> element");
            }

            // Recursively extract all items
            ExtractHierarchyItems(structuredElement, ns, items);

            _logger.LogInformation("Loaded {Count} hierarchy items from {Path}", items.Count, path);
            return items;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "XML parsing error in hierarchy file: {Path}", path);
            throw new InvalidOperationException($"Hierarchy file is malformed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading hierarchy from {Path}", path);
            throw;
        }
    }

    private void ExtractHierarchyItems(XElement parent, XNamespace ns, List<HierarchyItem> items)
    {
        // Find all direct item children
        foreach (var itemElement in parent.Elements(ns + "item"))
        {
            var item = ParseHierarchyItem(itemElement, ns);
            if (item != null)
            {
                items.Add(item);
            }

            // Recursively process sub_items
            var subItemsElement = itemElement.Element(ns + "sub_items");
            if (subItemsElement != null)
            {
                ExtractHierarchyItems(subItemsElement, ns, items);
            }
        }
    }

    private HierarchyItem? ParseHierarchyItem(XElement itemElement, XNamespace ns)
    {
        try
        {
            var id = itemElement.Attribute("id")?.Value;
            var levelStr = itemElement.Attribute("level")?.Value;
            var dataRef = itemElement.Attribute("data-ref")?.Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(levelStr) || string.IsNullOrEmpty(dataRef))
            {
                _logger.LogWarning("Hierarchy item missing required attributes (id, level, or data-ref)");
                return null;
            }

            if (!int.TryParse(levelStr, out var level))
            {
                _logger.LogWarning("Invalid level value for item {Id}: {Level}", id, levelStr);
                return null;
            }

            var webPageElement = itemElement.Element(ns + "web_page");
            var linkName = webPageElement?.Element(ns + "linkname")?.Value ?? string.Empty;
            var webPagePath = webPageElement?.Element(ns + "path")?.Value ?? string.Empty;

            if (string.IsNullOrEmpty(linkName))
            {
                _logger.LogWarning("Hierarchy item {Id} missing linkname element", id);
            }

            return new HierarchyItem
            {
                Id = id,
                Level = level,
                DataRef = dataRef,
                LinkName = linkName,
                WebPagePath = webPagePath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing hierarchy item element");
            return null;
        }
    }

    public async Task<XDocument> LoadTemplateAsync()
    {
        try
        {
            _logger.LogInformation("Loading template from {Path}", TemplateFilePath);

            if (!File.Exists(TemplateFilePath))
            {
                throw new FileNotFoundException($"Template file not found: {TemplateFilePath}");
            }

            var content = await File.ReadAllTextAsync(TemplateFilePath);
            var doc = XDocument.Parse(content);

            // Basic structure validation
            if (doc.Root?.Name.LocalName != "data")
            {
                throw new InvalidOperationException("Template file must have <data> root element");
            }

            _logger.LogInformation("Template loaded successfully");
            return doc;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "XML parsing error in template file");
            throw new InvalidOperationException($"Template file is malformed: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template");
            throw;
        }
    }

    public async Task<XDocument?> TransformSourceXmlAsync(string projectId, string sourceFile)
    {
        try
        {
            _logger.LogInformation("Transforming source file: {Project}/{File}", projectId, sourceFile);

            // Read source XML content
            var xmlContent = await _projectService.ReadInputFileAsync(projectId, sourceFile);

            // Read main XSLT file
            var xsltPath = "/app/xslt/transformation.xslt";
            if (!File.Exists(xsltPath))
            {
                throw new FileNotFoundException($"XSLT transformation file not found: {xsltPath}");
            }

            var xsltContent = await File.ReadAllTextAsync(xsltPath);

            // Configure transformation options
            var options = new TransformationOptions
            {
                UseXslt3Service = true,
                NormalizeHeaders = false, // We'll do this per section in later phases
                Parameters = new Dictionary<string, string>
                {
                    { "project-id", projectId },
                    { "file-name", sourceFile },
                    { "generate-ids", "true" }
                }
            };

            // Perform transformation
            var result = await _xsltService.TransformAsync(xmlContent, xsltContent, options);

            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"XSLT transformation failed: {result.ErrorMessage}");
            }

            // Parse the transformed output
            var transformedDoc = XDocument.Parse(result.OutputContent);

            _logger.LogInformation("Source file transformed successfully in {Ms}ms", result.ProcessingTimeMs);
            return transformedDoc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming source XML: {Project}/{File}", projectId, sourceFile);
            throw;
        }
    }

    public async Task<ValidationResult> ValidateConversionAsync(string projectId, string sourceFile, string hierarchyFile)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            _logger.LogInformation("Validating conversion configuration for project {ProjectId}", projectId);

            // 1. Validate project exists
            if (!await _projectService.ProjectExistsAsync(projectId))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Project not found: {projectId}";
                return result;
            }

            // 2. Validate source file exists
            var sourceFilePath = Path.Combine("/app/data/input/optiver/projects", projectId, sourceFile);
            var sourceValidation = await ValidateSourceFileAsync(sourceFilePath);
            if (!sourceValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessage = sourceValidation.ErrorMessage;
                return result;
            }

            // 3. Validate hierarchy file exists and structure
            var hierarchyFilePath = Path.Combine("/app/data/input/optiver/projects", projectId, "metadata", hierarchyFile);
            var hierarchyValidation = await ValidateHierarchyFileAsync(hierarchyFilePath);
            if (!hierarchyValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessage = hierarchyValidation.ErrorMessage;
                return result;
            }

            // 4. Validate template file
            var templateValidation = await ValidateTemplateFileAsync();
            if (!templateValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessage = templateValidation.ErrorMessage;
                return result;
            }

            // 5. Validate output folder is writable
            var outputValidation = await ValidateOutputFolderWritableAsync(projectId);
            if (!outputValidation.IsValid)
            {
                result.IsValid = false;
                result.ErrorMessage = outputValidation.ErrorMessage;
                return result;
            }

            _logger.LogInformation("All validations passed for project {ProjectId}", projectId);
            result.IsValid = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            result.IsValid = false;
            result.ErrorMessage = $"Validation error: {ex.Message}";
            return result;
        }
    }

    private async Task<ValidationResult> ValidateSourceFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ValidationResult.Failure($"Source file not found: {path}");
            }

            // Check if file is valid XML
            var content = await File.ReadAllTextAsync(path);
            try
            {
                XDocument.Parse(content);
            }
            catch (XmlException ex)
            {
                return ValidationResult.Failure($"Source file is not valid XML: {ex.Message}", ex.LineNumber, ex.LinePosition);
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Error validating source file: {ex.Message}");
        }
    }

    private async Task<ValidationResult> ValidateHierarchyFileAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return ValidationResult.Failure($"Hierarchy file not found: {path}");
            }

            // Load and validate structure
            var content = await File.ReadAllTextAsync(path);
            XDocument doc;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (XmlException ex)
            {
                return ValidationResult.Failure($"Hierarchy file is not valid XML: {ex.Message}", ex.LineNumber, ex.LinePosition);
            }

            // Check for required elements
            var defaultNs = doc.Root?.GetDefaultNamespace();
            XNamespace ns = defaultNs ?? XNamespace.None;

            if (doc.Root == null || doc.Root.Name.LocalName != "items")
            {
                return ValidationResult.Failure("Hierarchy file must have <items> root element");
            }

            var structuredElement = doc.Root.Element(ns + "structured");
            if (structuredElement == null)
            {
                return ValidationResult.Failure("Hierarchy file missing <structured> element");
            }

            // Validate at least one item exists
            var items = new List<HierarchyItem>();
            ExtractHierarchyItems(structuredElement, ns, items);

            if (items.Count == 0)
            {
                return ValidationResult.Failure("Hierarchy file contains no items");
            }

            // Validate all items have required fields
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Id))
                {
                    return ValidationResult.Failure($"Hierarchy item missing 'id' attribute");
                }

                if (string.IsNullOrEmpty(item.DataRef))
                {
                    return ValidationResult.Failure($"Hierarchy item '{item.Id}' missing 'data-ref' attribute");
                }

                if (string.IsNullOrEmpty(item.LinkName))
                {
                    return ValidationResult.Failure($"Hierarchy item '{item.Id}' missing 'linkname' element");
                }
            }

            _logger.LogInformation("Hierarchy file validated successfully: {Count} items found", items.Count);
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Error validating hierarchy file: {ex.Message}");
        }
    }

    private async Task<ValidationResult> ValidateTemplateFileAsync()
    {
        try
        {
            if (!File.Exists(TemplateFilePath))
            {
                return ValidationResult.Failure($"Template file not found: {TemplateFilePath}");
            }

            var content = await File.ReadAllTextAsync(TemplateFilePath);
            XDocument doc;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (XmlException ex)
            {
                return ValidationResult.Failure($"Template file is not valid XML: {ex.Message}", ex.LineNumber, ex.LinePosition);
            }

            // Check for required structure
            if (doc.Root?.Name.LocalName != "data")
            {
                return ValidationResult.Failure("Template file must have <data> root element");
            }

            var systemElement = doc.Root.Element("system");
            if (systemElement == null)
            {
                return ValidationResult.Failure("Template file missing <system> element");
            }

            var dateCreated = systemElement.Element("date_created");
            var dateModified = systemElement.Element("date_modified");
            if (dateCreated == null || dateModified == null)
            {
                return ValidationResult.Failure("Template file missing date fields in <system> element");
            }

            var contentElement = doc.Root.Element("content");
            if (contentElement == null)
            {
                return ValidationResult.Failure("Template file missing <content> element");
            }

            var articleElement = contentElement.Element("article");
            if (articleElement == null)
            {
                return ValidationResult.Failure("Template file missing <article> element");
            }

            var sectionElement = articleElement.Descendants("section").FirstOrDefault();
            if (sectionElement == null)
            {
                return ValidationResult.Failure("Template file missing <section> element");
            }

            _logger.LogInformation("Template file validated successfully");
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Error validating template file: {ex.Message}");
        }
    }

    private async Task<ValidationResult> ValidateOutputFolderWritableAsync(string projectId)
    {
        try
        {
            var outputPath = Path.Combine("/app/data/output/optiver/projects", projectId, "data");

            // Check if parent directory exists
            var parentDir = Path.GetDirectoryName(outputPath);
            if (parentDir == null)
            {
                return ValidationResult.Failure("Invalid output path");
            }

            // Try to create directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                    _logger.LogInformation("Created output directory: {Path}", outputPath);
                }
                catch (UnauthorizedAccessException)
                {
                    return ValidationResult.Failure($"Cannot create output folder (permission denied): {outputPath}");
                }
                catch (IOException ex)
                {
                    return ValidationResult.Failure($"Cannot create output folder (I/O error): {ex.Message}");
                }
            }

            // Test write permissions by creating a temporary file
            var testFile = Path.Combine(outputPath, ".write_test");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                return ValidationResult.Failure($"Cannot write to output folder (permission denied): {outputPath}");
            }
            catch (IOException ex)
            {
                return ValidationResult.Failure($"Cannot write to output folder (I/O error): {ex.Message}");
            }

            _logger.LogInformation("Output folder is writable: {Path}", outputPath);
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Error validating output folder: {ex.Message}");
        }
    }

    public async Task PrepareOutputFolderAsync(string projectId)
    {
        try
        {
            var outputPath = $"/app/data/output/optiver/projects/{projectId}/data";

            _logger.LogInformation("Preparing output folder for project {ProjectId}", projectId);

            // Create directory if it doesn't exist
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
                _logger.LogInformation("Created output directory: {Path}", outputPath);
            }
            else
            {
                // Clean up existing XML files
                var xmlFiles = Directory.GetFiles(outputPath, "*.xml");

                if (xmlFiles.Length > 0)
                {
                    _logger.LogInformation("Found {Count} existing XML file(s) to clean up", xmlFiles.Length);

                    foreach (var file in xmlFiles)
                    {
                        File.Delete(file);
                        _logger.LogDebug("Deleted existing file: {FileName}", Path.GetFileName(file));
                    }

                    _logger.LogInformation("Cleaned up {Count} existing XML file(s)", xmlFiles.Length);
                }
                else
                {
                    _logger.LogInformation("Output directory already exists and is empty (no XML files)");
                }
            }

            _logger.LogInformation("Output folder prepared successfully: {Path}", outputPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied when preparing output folder for project {ProjectId}", projectId);
            throw new InvalidOperationException($"Cannot access output folder (permission denied)", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error when preparing output folder for project {ProjectId}", projectId);
            throw new InvalidOperationException($"Cannot prepare output folder (I/O error): {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error preparing output folder for project {ProjectId}", projectId);
            throw new InvalidOperationException($"Error preparing output folder: {ex.Message}", ex);
        }
    }

    public async Task<List<HeaderMatch>> MatchHeadersAsync(XDocument transformedXhtml, List<HierarchyItem> hierarchyItems)
    {
        try
        {
            _logger.LogInformation("Starting header matching for {Count} hierarchy items", hierarchyItems.Count);

            var matches = await _headerMatchingService.FindExactMatchesAsync(transformedXhtml, hierarchyItems);

            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during header matching");
            throw;
        }
    }

    public async Task<ConversionResult> StartConversionAsync(
        string projectId,
        string sourceFile,
        string hierarchyFile,
        Action<string> logCallback,
        Func<List<HeaderMatch>, XDocument, List<HeaderMatch>, List<HierarchyItem>, HierarchyItem, Task<HeaderMatch?>>? duplicateSelectionCallback = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ConversionResult
        {
            CreatedFiles = new List<string>(),
            Errors = new List<string>()
        };

        try
        {
            // 1. Validate configuration
            logCallback("Validating configuration...");
            var validation = await ValidateConversionAsync(projectId, sourceFile, hierarchyFile);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.Errors.Add($"Validation failed: {validation.ErrorMessage}");
                logCallback($"✗ Validation failed: {validation.ErrorMessage}");
                return result;
            }

            logCallback("✓ All validations passed");

            // 2. Load hierarchy
            logCallback("Loading hierarchy...");
            var hierarchyPath = Path.Combine("/app/data/input/optiver/projects", projectId, "metadata", hierarchyFile);
            var hierarchyItems = await LoadHierarchyAsync(hierarchyPath);
            result.TotalSections = hierarchyItems.Count;
            logCallback($"✓ Loaded {hierarchyItems.Count} hierarchy items");

            // 3. Load template
            logCallback("Loading template...");
            var template = await LoadTemplateAsync();
            logCallback("✓ Template loaded successfully");

            // 4. Transform source XML
            logCallback("Transforming source XML...");
            var transformedXhtml = await TransformSourceXmlAsync(projectId, sourceFile);

            if (transformedXhtml == null)
            {
                result.Success = false;
                result.Errors.Add("Source XML transformation failed");
                logCallback("✗ Source XML transformation failed");
                return result;
            }

            logCallback("✓ Source XML transformed successfully");

            // 5. Match headers
            logCallback("Matching headers...");
            var matches = await MatchHeadersAsync(transformedXhtml, hierarchyItems);
            logCallback($"✓ Matched {matches.Count} headers");

            // 6. Prepare output folder
            logCallback("Preparing output folder...");
            await PrepareOutputFolderAsync(projectId);
            logCallback("✓ Output folder prepared");

            // 7. Process each match
            logCallback("");
            logCallback("Processing sections...");

            // Track resolved duplicates to avoid asking twice
            var resolvedDuplicates = new Dictionary<string, HeaderMatch>();

            for (int i = 0; i < matches.Count; i++)
            {
                // Check for cancellation before processing each section
                if (cancellationToken.IsCancellationRequested)
                {
                    logCallback("⚠ Conversion cancelled by user");
                    result.Success = false;
                    result.WasCancelled = true;
                    result.Duration = DateTime.UtcNow - startTime;
                    break;
                }

                var match = matches[i];
                var progress = $"[{i + 1}/{matches.Count}]";

                // Handle duplicates with user selection
                if (match.IsDuplicate)
                {
                    // Collect all duplicate matches for this linkname
                    var linkName = match.HierarchyItem.LinkName;

                    // Check if we already resolved this from a previous boundary selection
                    if (resolvedDuplicates.ContainsKey(linkName))
                    {
                        logCallback($"{progress} ℹ Using previously selected duplicate for '{linkName}'");
                        match = resolvedDuplicates[linkName];
                        // Remove from resolved list as we've now used it
                        resolvedDuplicates.Remove(linkName);
                    }
                    else
                    {
                        // Find all duplicates for THIS SPECIFIC hierarchy item (by Id, not LinkName)
                        // This ensures we don't mix duplicates from different hierarchy items with the same LinkName
                        var duplicateMatches = matches
                            .Where(m => m.HierarchyItem.Id == match.HierarchyItem.Id && m.IsDuplicate)
                            .ToList();

                        // If we haven't processed this duplicate group yet
                        if (duplicateMatches.Any() && duplicateMatches[0] == match)
                        {
                            logCallback($"{progress} ℹ Duplicate found for '{linkName}', asking user...");

                            HeaderMatch? selectedMatch = null;

                            if (duplicateSelectionCallback != null)
                            {
                                try
                                {
                                    // Get list of matches processed so far (for context visualization)
                                    var processedMatches = matches.Take(i).ToList();

                                    // Get current hierarchy item (the one with duplicates)
                                    var currentHierarchyItem = match.HierarchyItem;

                                    selectedMatch = await duplicateSelectionCallback(
                                        duplicateMatches,
                                        transformedXhtml,
                                        processedMatches,
                                        hierarchyItems,
                                        currentHierarchyItem);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error in duplicate selection callback");
                                    logCallback($"{progress} ✗ Error in duplicate selection: {ex.Message}");
                                }
                            }

                            if (selectedMatch != null)
                            {
                                logCallback($"{progress} ✓ User selected match {selectedMatch.DuplicateIndex + 1} of {selectedMatch.DuplicateCount}: {selectedMatch.MatchedText}");
                                match = selectedMatch; // Use selected match
                            }
                            else
                            {
                                logCallback($"{progress} ⚠ User skipped duplicate: {linkName}");
                                result.SkippedSections += duplicateMatches.Count;
                                // Skip all remaining duplicates in this group
                                i += duplicateMatches.Count - 1;
                                continue;
                            }
                        }
                        else
                        {
                            // This is not the first duplicate in the group, skip it
                            // (already handled when we processed the first one)
                            continue;
                        }
                    }
                }

                // Skip unmatched
                if (match.MatchedHeader == null)
                {
                    logCallback($"{progress} ⚠ Skipping unmatched: {match.HierarchyItem.LinkName}");
                    result.SkippedSections++;
                    continue;
                }

                try
                {
                    // Find the next matched header to use as boundary
                    XElement? nextHeader = null;

                    // Find current item's position in hierarchy
                    var currentHierarchyIndex = hierarchyItems.IndexOf(match.HierarchyItem);

                    // Look for the next hierarchical item
                    if (currentHierarchyIndex >= 0 && currentHierarchyIndex < hierarchyItems.Count - 1)
                    {
                        var nextHierarchyItem = hierarchyItems[currentHierarchyIndex + 1];
                        var nextLinkName = nextHierarchyItem.LinkName;

                        // Find all matches for this SPECIFIC next item (by Id, not LinkName)
                        // This ensures we don't get matches for other hierarchy items with the same LinkName
                        var nextMatches = matches
                            .Where(m => m.HierarchyItem.Id == nextHierarchyItem.Id && m.MatchedHeader != null)
                            .ToList();

                        if (nextMatches.Count > 1)
                        {
                            // Multiple matches found - try smart detection first
                            logCallback($"{progress} ℹ Multiple headers found for boundary '{nextLinkName}'");

                            // Try to disambiguate by looking at subsequent headers
                            var disambiguatedMatch = TryDisambiguateBySequence(
                                nextMatches,
                                hierarchyItems,
                                currentHierarchyIndex + 1,
                                transformedXhtml,
                                matches,
                                logCallback,
                                progress);

                            if (disambiguatedMatch != null)
                            {
                                // Smart detection succeeded
                                nextHeader = disambiguatedMatch.MatchedHeader;
                                logCallback($"{progress} ✓ Auto-selected boundary based on header sequence: {disambiguatedMatch.MatchedText}");
                                // Store this selection for reuse
                                resolvedDuplicates[nextLinkName] = disambiguatedMatch;
                            }
                            else
                            {
                                // Still ambiguous - ask user
                                logCallback($"{progress} ℹ Could not auto-resolve, asking user...");

                                HeaderMatch? selectedNextMatch = null;

                                if (duplicateSelectionCallback != null)
                                {
                                    try
                                    {
                                        var processedMatches = matches.Take(i).ToList();
                                        selectedNextMatch = await duplicateSelectionCallback(
                                            nextMatches,
                                            transformedXhtml,
                                            processedMatches,
                                            hierarchyItems,
                                            nextHierarchyItem);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error in duplicate selection for boundary");
                                        logCallback($"{progress} ✗ Error selecting boundary: {ex.Message}");
                                    }
                                }

                                if (selectedNextMatch != null)
                                {
                                    nextHeader = selectedNextMatch.MatchedHeader;
                                    logCallback($"{progress} ✓ User selected boundary: {selectedNextMatch.MatchedText}");
                                    // Store this selection for reuse when processing the next section
                                    resolvedDuplicates[nextLinkName] = selectedNextMatch;
                                }
                                else
                                {
                                    logCallback($"{progress} ⚠ User skipped boundary selection, extracting to end");
                                }
                            }
                        }
                        else if (nextMatches.Count == 1)
                        {
                            nextHeader = nextMatches[0].MatchedHeader;
                        }
                    }

                    // Extract content with explicit boundary
                    var extractedContent = _contentExtractionService.ExtractContent(
                        transformedXhtml,
                        match.MatchedHeader,
                        nextHeader); // Pass the selected next header as explicit boundary

                    // Normalize headers
                    var normalizedContent = _headerNormalizationService.NormalizeHeaders(extractedContent);

                    // Populate template
                    var sectionXml = PopulateTemplate(template, match.HierarchyItem, normalizedContent);

                    // Write to file (always complete current file write before cancelling)
                    var outputPath = Path.Combine("/app/data/output/optiver/projects", projectId, "data", match.HierarchyItem.DataRef);
                    await File.WriteAllTextAsync(outputPath, sectionXml.ToString(), cancellationToken);

                    result.CreatedFiles.Add(match.HierarchyItem.DataRef);
                    result.SuccessfulSections++;
                    logCallback($"{progress} ✓ Created: {match.HierarchyItem.DataRef}");
                }
                catch (Exception ex)
                {
                    result.FailedSections++;
                    var errorMsg = $"{match.HierarchyItem.LinkName}: {ex.Message}";
                    result.Errors.Add(errorMsg);
                    logCallback($"{progress} ✗ Failed: {errorMsg}");
                    _logger.LogError(ex, "Error processing section {LinkName}", match.HierarchyItem.LinkName);
                }
            }

            // Set success status (only if not cancelled)
            if (!result.WasCancelled)
            {
                result.Success = result.FailedSections == 0;
                result.Duration = DateTime.UtcNow - startTime;
            }

            // Final summary
            logCallback("");
            if (result.WasCancelled)
            {
                logCallback("=== Conversion Cancelled ===");
            }
            else
            {
                logCallback("=== Conversion Complete ===");
            }

            logCallback($"Duration: {result.Duration.TotalSeconds:F1}s");
            logCallback($"Successful: {result.SuccessfulSections}");
            logCallback($"Failed: {result.FailedSections}");
            logCallback($"Skipped: {result.SkippedSections}");
            logCallback($"Total: {result.TotalSections}");

            if (result.Errors.Any())
            {
                logCallback("");
                logCallback("Errors:");
                foreach (var error in result.Errors)
                {
                    logCallback($"  - {error}");
                }
            }

            _logger.LogInformation("Conversion {Status}: {Result}",
                result.WasCancelled ? "cancelled" : "completed",
                result.ToString());
            return result;
        }
        catch (OperationCanceledException)
        {
            // Handle explicit cancellation (if thrown)
            result.Success = false;
            result.WasCancelled = true;
            result.Duration = DateTime.UtcNow - startTime;
            logCallback("✗ Conversion cancelled by user");
            _logger.LogInformation("Conversion cancelled for project {ProjectId}", projectId);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            result.Duration = DateTime.UtcNow - startTime;
            logCallback($"✗ Conversion failed: {ex.Message}");
            _logger.LogError(ex, "Conversion failed for project {ProjectId}", projectId);
            throw;
        }
    }

    /// <summary>
    /// Populates the template XML with hierarchy item metadata and extracted content
    /// </summary>
    private XDocument PopulateTemplate(
        XDocument template,
        HierarchyItem hierarchyItem,
        XDocument extractedContent)
    {
        // Clone template to avoid modifying original
        var populated = new XDocument(template);

        var ns = populated.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var systemElement = populated.Root?.Element(ns + "system");

        if (systemElement != null)
        {
            // Update id attribute
            var idElement = systemElement.Element(ns + "id");
            if (idElement != null)
                idElement.Value = hierarchyItem.Id;

            // Update level attribute
            var levelElement = systemElement.Element(ns + "level");
            if (levelElement != null)
                levelElement.Value = hierarchyItem.Level.ToString();

            // Update webpage_path
            var webpagePathElement = systemElement.Element(ns + "webpage_path");
            if (webpagePathElement != null)
                webpagePathElement.Value = hierarchyItem.WebPagePath ?? "";

            // Update timestamps
            var currentTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var dateCreatedElement = systemElement.Element(ns + "date_created");
            if (dateCreatedElement != null)
                dateCreatedElement.Value = currentTime;

            var dateModifiedElement = systemElement.Element(ns + "date_modified");
            if (dateModifiedElement != null)
                dateModifiedElement.Value = currentTime;

            // Update user info
            var createdByElement = systemElement.Element(ns + "created_by");
            if (createdByElement != null)
                createdByElement.Value = "system@taxxor.com";

            var modifiedByElement = systemElement.Element(ns + "modified_by");
            if (modifiedByElement != null)
                modifiedByElement.Value = "system@taxxor.com";
        }

        // Insert content into the template
        var contentElement = populated.Root?.Element(ns + "content");
        if (contentElement != null)
        {
            // Find the section element in the article
            var articleElement = contentElement.Element(ns + "article");
            var sectionElement = articleElement?.Descendants(ns + "section").FirstOrDefault();

            if (sectionElement != null)
            {
                // Clear existing content placeholder
                sectionElement.RemoveAll();

                // Extract body content from the extracted XHTML
                var xhtmlNs = XNamespace.Get("http://www.w3.org/1999/xhtml");
                var bodyContent = extractedContent.Descendants(xhtmlNs + "body")
                    .FirstOrDefault()?
                    .Elements();

                if (bodyContent != null && bodyContent.Any())
                {
                    // Add content to section, stripping XHTML namespace
                    foreach (var element in bodyContent)
                    {
                        sectionElement.Add(StripNamespace(element));
                    }
                }
                else
                {
                    _logger.LogWarning("No body content found in extracted content for {LinkName}", hierarchyItem.LinkName);
                }
            }
            else
            {
                _logger.LogWarning("No section element found in template for {LinkName}", hierarchyItem.LinkName);
            }
        }

        return populated;
    }

    /// <summary>
    /// Recursively strips namespace from an element and all its descendants
    /// </summary>
    private XElement StripNamespace(XElement element)
    {
        // Create new element without namespace
        var newElement = new XElement(
            element.Name.LocalName,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration),
            element.Nodes().Select(n =>
            {
                if (n is XElement e)
                    return StripNamespace(e);
                return n;
            })
        );

        return newElement;
    }

    /// <summary>
    /// Tries to disambiguate duplicate header matches by looking at the sequence of subsequent headers
    /// </summary>
    private HeaderMatch? TryDisambiguateBySequence(
        List<HeaderMatch> duplicateMatches,
        List<HierarchyItem> hierarchyItems,
        int startHierarchyIndex,
        XDocument transformedXhtml,
        List<HeaderMatch> allMatches,
        Action<string> logCallback,
        string progress)
    {
        try
        {
            logCallback($"{progress}   Checking next 3-4 headers to auto-resolve ambiguity...");

            // Determine how many headers to look ahead (max 4, or until end of hierarchy)
            // Start from the NEXT item after the duplicate, not the duplicate itself
            int lookAheadCount = Math.Min(4, hierarchyItems.Count - startHierarchyIndex - 1);

            // Get the next few hierarchy items to check (AFTER the duplicate)
            var upcomingHierarchy = new List<HierarchyItem>();
            for (int j = 0; j < lookAheadCount && startHierarchyIndex + 1 + j < hierarchyItems.Count; j++)
            {
                upcomingHierarchy.Add(hierarchyItems[startHierarchyIndex + 1 + j]);
            }

            if (upcomingHierarchy.Count < 1)
            {
                // Not enough headers to disambiguate
                logCallback($"{progress}   Not enough subsequent headers to disambiguate");
                return null;
            }

            // Get the XHTML namespace
            var xhtmlNs = XNamespace.Get("http://www.w3.org/1999/xhtml");
            var allHeaders = transformedXhtml.Descendants()
                .Where(e => IsHeaderElement(e))
                .ToList();

            // Score each duplicate by how well the subsequent headers match
            var candidateScores = new Dictionary<HeaderMatch, int>();

            foreach (var duplicate in duplicateMatches)
            {
                if (duplicate.MatchedHeader == null) continue;

                int score = 0;
                var duplicateIndex = allHeaders.IndexOf(duplicate.MatchedHeader);

                if (duplicateIndex < 0) continue;

                // Look for the subsequent hierarchy items after this duplicate
                for (int j = 0; j < upcomingHierarchy.Count; j++)
                {
                    var expectedItem = upcomingHierarchy[j];
                    bool found = false;

                    // Look in the next headers after the duplicate (within next 20 headers)
                    for (int k = duplicateIndex + 1; k < allHeaders.Count && k < duplicateIndex + 20; k++)
                    {
                        var header = allHeaders[k];
                        var headerText = NormalizeHeaderText(header.Value);

                        if (HeaderMatchesHierarchy(headerText, expectedItem.LinkName))
                        {
                            found = true;
                            score += (5 - j); // Higher score for closer matches (5 for 1st, 4 for 2nd, etc.)
                            logCallback($"{progress}     Found expected header '{expectedItem.LinkName}' at position {j + 1}");
                            break;
                        }
                    }

                    if (!found)
                    {
                        // Penalty for missing expected header
                        score -= 2;
                        logCallback($"{progress}     Expected header '{expectedItem.LinkName}' not found (penalty: -2)");
                    }
                }

                candidateScores[duplicate] = score;
                logCallback($"{progress}     Candidate {duplicate.DuplicateIndex + 1}: '{duplicate.MatchedText}' - Score: {score}");
            }

            // Find the best scoring candidate
            if (candidateScores.Count > 0)
            {
                var bestCandidate = candidateScores.OrderByDescending(kvp => kvp.Value).First();
                var secondBest = candidateScores.OrderByDescending(kvp => kvp.Value).Skip(1).FirstOrDefault();

                // Only auto-select if there's a clear winner (at least 3 points difference)
                if (secondBest.Key == null || bestCandidate.Value - secondBest.Value >= 3)
                {
                    logCallback($"{progress}   Best match found with score {bestCandidate.Value}");
                    return bestCandidate.Key;
                }
                else
                {
                    logCallback($"{progress}   Scores too close to auto-select (best: {bestCandidate.Value}, second: {secondBest.Value})");
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in disambiguation logic");
            logCallback($"{progress}   Error during disambiguation: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Checks if an element is a header element (h1-h6)
    /// </summary>
    private bool IsHeaderElement(XElement element)
    {
        var localName = element.Name.LocalName.ToLowerInvariant();
        return localName.Length == 2 && localName[0] == 'h' && char.IsDigit(localName[1]);
    }

    /// <summary>
    /// Normalizes header text for comparison
    /// </summary>
    private string NormalizeHeaderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Remove extra whitespace and trim
        return System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s+", " ");
    }

    /// <summary>
    /// Checks if header text matches hierarchy link name (case-insensitive)
    /// </summary>
    private bool HeaderMatchesHierarchy(string headerText, string hierarchyLinkName)
    {
        if (string.IsNullOrWhiteSpace(headerText) || string.IsNullOrWhiteSpace(hierarchyLinkName))
            return false;

        // Simple case-insensitive comparison
        return string.Equals(headerText, hierarchyLinkName, StringComparison.OrdinalIgnoreCase);
    }
}

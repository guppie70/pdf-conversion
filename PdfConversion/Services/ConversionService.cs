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
}

/// <summary>
/// Implementation of conversion service
/// </summary>
public class ConversionService : IConversionService
{
    private readonly ILogger<ConversionService> _logger;
    private readonly IXsltTransformationService _xsltService;
    private readonly IProjectManagementService _projectService;
    private const string TemplateFilePath = "/app/data/input/template.xml";

    public ConversionService(
        ILogger<ConversionService> logger,
        IXsltTransformationService xsltService,
        IProjectManagementService projectService)
    {
        _logger = logger;
        _xsltService = xsltService;
        _projectService = projectService;
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
}

using PdfConversion.Models;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace PdfConversion.Services;

/// <summary>
/// Service for XSLT transformations with caching and validation
/// </summary>
public interface IXsltTransformationService
{
    /// <summary>
    /// Transforms XML content using XSLT
    /// </summary>
    Task<TransformationResult> TransformAsync(string xmlContent, string xsltContent, TransformationOptions? options = null);

    /// <summary>
    /// Validates XSLT syntax
    /// </summary>
    Task<ValidationResult> ValidateXsltAsync(string xsltContent);

    /// <summary>
    /// Normalizes header hierarchy in XHTML content
    /// </summary>
    Task<string> NormalizeHeadersAsync(string xhtmlContent);
}

/// <summary>
/// Implementation of XSLT transformation service
/// </summary>
public class XsltTransformationService : IXsltTransformationService
{
    private readonly ILogger<XsltTransformationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IXslt3ServiceClient? _xslt3Client;
    private readonly ITransformationLogService? _transformationLogService;
    private readonly IDistributedCacheService? _distributedCacheService;
    private readonly IPerformanceMonitoringService? _performanceMonitoring;
    private const string CacheKeyPrefix = "XsltTemplate_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public XsltTransformationService(
        ILogger<XsltTransformationService> logger,
        IMemoryCache cache,
        IXslt3ServiceClient? xslt3Client = null,
        ITransformationLogService? transformationLogService = null,
        IDistributedCacheService? distributedCacheService = null,
        IPerformanceMonitoringService? performanceMonitoring = null)
    {
        _logger = logger;
        _cache = cache;
        _xslt3Client = xslt3Client;
        _transformationLogService = transformationLogService;
        _distributedCacheService = distributedCacheService;
        _performanceMonitoring = performanceMonitoring;
    }

    public async Task<TransformationResult> TransformAsync(string xmlContent, string xsltContent, TransformationOptions? options = null)
    {
        options ??= new TransformationOptions();
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationResult();
        var projectId = options.Parameters.GetValueOrDefault("project-id");

        // Track operation performance
        using var perfTracker = _performanceMonitoring?.TrackOperation("XsltTransformation", projectId);

        try
        {
            _logger.LogDebug("Starting XSLT transformation (UseXslt3Service: {UseXslt3Service})", options.UseXslt3Service);

            // Check distributed cache first if available
            if (_distributedCacheService != null)
            {
                var xmlHash = ComputeHash(xmlContent);
                var xsltHash = ComputeHash(xsltContent);

                var cachedResult = await _distributedCacheService.GetTransformationResultAsync(xmlHash, xsltHash);
                if (cachedResult != null)
                {
                    _logger.LogInformation("Transformation result found in distributed cache");
                    _performanceMonitoring?.RecordTransformation(true, cachedResult.ProcessingTimeMs, projectId);
                    return cachedResult;
                }
            }

            // Use XSLT3Service if requested and available
            if (options.UseXslt3Service && _xslt3Client != null)
            {
                _logger.LogDebug("Using XSLT3Service for transformation");
                result = await _xslt3Client.TransformAsync(xmlContent, xsltContent, options.Parameters);

                // If XSLT3Service failed, fall back to local transformation
                if (!result.IsSuccess && result.WarningMessages.Any(w => w.Contains("fallback")))
                {
                    _logger.LogWarning("XSLT3Service failed, falling back to local XSLT 1.0 processor");
                    // Continue with local transformation below
                }
                else
                {
                    // XSLT3Service succeeded or failed without fallback option
                    if (result.IsSuccess && options.NormalizeHeaders)
                    {
                        var (normalized, headersNormalized) = await NormalizeHeadersInternalAsync(result.OutputContent);
                        result.OutputContent = normalized;
                        result.HeadersNormalized = headersNormalized;
                    }
                    result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                    return result;
                }
            }
            else if (options.UseXslt3Service && _xslt3Client == null)
            {
                _logger.LogWarning("XSLT3Service requested but client not available, using local XSLT 1.0");
                result.WarningMessages.Add("XSLT3Service not available, using XSLT 1.0 fallback");
            }

            // Validate XSLT first (for local transformation)
            var validation = await ValidateXsltAsync(xsltContent);
            if (!validation.IsValid)
            {
                result.IsSuccess = false;
                result.ErrorMessage = $"XSLT validation failed: {validation.ErrorMessage}";
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Get or compile XSLT transform
            var cacheKey = CacheKeyPrefix + ComputeHash(xsltContent);
            var transform = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                _logger.LogDebug("Compiling and caching XSLT template");
                return CompileXslt(xsltContent);
            });

            if (transform == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Failed to compile XSLT";
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Perform transformation
            var transformedContent = await Task.Run(() =>
            {
                using var stringReader = new StringReader(xmlContent);
                using var xmlReader = XmlReader.Create(stringReader);
                using var stringWriter = new StringWriter();
                using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings
                {
                    Indent = true,
                    IndentChars = "  ",
                    OmitXmlDeclaration = false,
                    Encoding = Encoding.UTF8
                });

                // Create argument list for parameters
                var args = new XsltArgumentList();
                foreach (var param in options.Parameters)
                {
                    args.AddParam(param.Key, "", param.Value);
                }

                transform.Transform(xmlReader, args, xmlWriter);
                return stringWriter.ToString();
            });

            result.OutputContent = transformedContent;

            // Normalize headers if requested
            if (options.NormalizeHeaders)
            {
                _logger.LogDebug("Normalizing headers");
                var (normalized, headersNormalized) = await NormalizeHeadersInternalAsync(transformedContent);
                result.OutputContent = normalized;
                result.HeadersNormalized = headersNormalized;
            }

            // Collect statistics
            result.TablesProcessed = CountMatches(result.OutputContent, @"<table[\s>]");

            result.IsSuccess = true;
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Transformation completed successfully in {ElapsedMs}ms", result.ProcessingTimeMs);

            // Cache successful transformation result
            if (_distributedCacheService != null)
            {
                var xmlHash = ComputeHash(xmlContent);
                var xsltHash = ComputeHash(xsltContent);
                await _distributedCacheService.SetTransformationResultAsync(xmlHash, xsltHash, result);
                _logger.LogDebug("Cached transformation result");
            }

            // Log transformation to the transformation log service
            _transformationLogService?.LogTransformation(new TransformationLog
            {
                ProjectId = options.Parameters.GetValueOrDefault("project-id") ?? "unknown",
                FileName = options.Parameters.GetValueOrDefault("file-name"),
                StartTime = DateTime.Now.AddMilliseconds(-result.ProcessingTimeMs),
                EndTime = DateTime.Now,
                Status = "Success",
                Details = $"Processed successfully. Tables: {result.TablesProcessed}, Headers normalized: {result.HeadersNormalized}",
                ProcessingTimeMs = result.ProcessingTimeMs,
                HeadersNormalized = result.HeadersNormalized,
                TablesProcessed = result.TablesProcessed
            });

            // Record performance metrics
            _performanceMonitoring?.RecordTransformation(true, result.ProcessingTimeMs, projectId);

            return result;
        }
        catch (XsltException ex)
        {
            _logger.LogError(ex, "XSLT transformation error at line {LineNumber}", ex.LineNumber);
            result.IsSuccess = false;
            result.ErrorMessage = $"XSLT error at line {ex.LineNumber}: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            // Log error transformation
            _transformationLogService?.LogTransformation(new TransformationLog
            {
                ProjectId = options?.Parameters.GetValueOrDefault("project-id") ?? "unknown",
                FileName = options?.Parameters.GetValueOrDefault("file-name"),
                StartTime = DateTime.Now.AddMilliseconds(-result.ProcessingTimeMs),
                EndTime = DateTime.Now,
                Status = "Error",
                Details = $"XSLT error at line {ex.LineNumber}: {ex.Message}",
                ProcessingTimeMs = result.ProcessingTimeMs
            });

            // Record failed transformation
            _performanceMonitoring?.RecordTransformation(false, result.ProcessingTimeMs, projectId);

            return result;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "XML parsing error at line {LineNumber}", ex.LineNumber);
            result.IsSuccess = false;
            result.ErrorMessage = $"XML error at line {ex.LineNumber}: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            // Log error transformation
            _transformationLogService?.LogTransformation(new TransformationLog
            {
                ProjectId = options?.Parameters.GetValueOrDefault("project-id") ?? "unknown",
                FileName = options?.Parameters.GetValueOrDefault("file-name"),
                StartTime = DateTime.Now.AddMilliseconds(-result.ProcessingTimeMs),
                EndTime = DateTime.Now,
                Status = "Error",
                Details = $"XML error at line {ex.LineNumber}: {ex.Message}",
                ProcessingTimeMs = result.ProcessingTimeMs
            });

            // Record failed transformation
            _performanceMonitoring?.RecordTransformation(false, result.ProcessingTimeMs, projectId);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during transformation");
            result.IsSuccess = false;
            result.ErrorMessage = $"Transformation failed: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            // Log error transformation
            _transformationLogService?.LogTransformation(new TransformationLog
            {
                ProjectId = options?.Parameters.GetValueOrDefault("project-id") ?? "unknown",
                FileName = options?.Parameters.GetValueOrDefault("file-name"),
                StartTime = DateTime.Now.AddMilliseconds(-result.ProcessingTimeMs),
                EndTime = DateTime.Now,
                Status = "Error",
                Details = $"Transformation failed: {ex.Message}",
                ProcessingTimeMs = result.ProcessingTimeMs
            });

            // Record failed transformation
            _performanceMonitoring?.RecordTransformation(false, result.ProcessingTimeMs, projectId);

            return result;
        }
    }

    public async Task<ValidationResult> ValidateXsltAsync(string xsltContent)
    {
        try
        {
            await Task.Run(() =>
            {
                using var stringReader = new StringReader(xsltContent);
                using var xmlReader = XmlReader.Create(stringReader);

                var transform = new XslCompiledTransform();
                transform.Load(xmlReader);
            });

            _logger.LogDebug("XSLT validation successful");
            return ValidationResult.Success();
        }
        catch (XsltException ex)
        {
            _logger.LogWarning(ex, "XSLT validation failed at line {LineNumber}", ex.LineNumber);
            return ValidationResult.Failure(ex.Message, ex.LineNumber, ex.LinePosition);
        }
        catch (XmlException ex)
        {
            _logger.LogWarning(ex, "XML validation failed at line {LineNumber}", ex.LineNumber);
            return ValidationResult.Failure(ex.Message, ex.LineNumber, ex.LinePosition);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Validation failed");
            return ValidationResult.Failure(ex.Message);
        }
    }

    public async Task<string> NormalizeHeadersAsync(string xhtmlContent)
    {
        var (normalized, _) = await NormalizeHeadersInternalAsync(xhtmlContent);
        return normalized;
    }

    private async Task<(string normalizedContent, int headersNormalized)> NormalizeHeadersInternalAsync(string xhtmlContent)
    {
        return await Task.Run(() =>
        {
            try
            {
                var doc = XDocument.Parse(xhtmlContent);
                var headersNormalized = 0;
                var currentLevel = 0;

                // Find all header elements (h1-h6)
                var headers = doc.Descendants()
                    .Where(e => Regex.IsMatch(e.Name.LocalName, @"^h[1-6]$", RegexOptions.IgnoreCase))
                    .ToList();

                foreach (var header in headers)
                {
                    var match = Regex.Match(header.Name.LocalName, @"^h(\d)$", RegexOptions.IgnoreCase);
                    if (!match.Success) continue;

                    var level = int.Parse(match.Groups[1].Value);

                    // First header should always be h1
                    if (currentLevel == 0)
                    {
                        if (level != 1)
                        {
                            header.Name = header.Name.Namespace + "h1";
                            headersNormalized++;
                            _logger.LogDebug("Normalized first header from h{OldLevel} to h1", level);
                        }
                        currentLevel = 1;
                        continue;
                    }

                    // Headers can only increase by 1 level at a time
                    var maxAllowedLevel = currentLevel + 1;
                    if (level > maxAllowedLevel)
                    {
                        header.Name = header.Name.Namespace + $"h{maxAllowedLevel}";
                        headersNormalized++;
                        _logger.LogDebug("Normalized header from h{OldLevel} to h{NewLevel}", level, maxAllowedLevel);
                        currentLevel = maxAllowedLevel;
                    }
                    else
                    {
                        currentLevel = level;
                    }
                }

                if (headersNormalized > 0)
                {
                    _logger.LogInformation("Normalized {Count} headers", headersNormalized);
                }

                return (doc.ToString(), headersNormalized);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to normalize headers, returning original content");
                return (xhtmlContent, 0);
            }
        });
    }

    private XslCompiledTransform? CompileXslt(string xsltContent)
    {
        try
        {
            var transform = new XslCompiledTransform();
            using var stringReader = new StringReader(xsltContent);
            using var xmlReader = XmlReader.Create(stringReader);

            transform.Load(xmlReader);
            return transform;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compile XSLT");
            return null;
        }
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static int CountMatches(string content, string pattern)
    {
        try
        {
            return Regex.Matches(content, pattern, RegexOptions.IgnoreCase).Count;
        }
        catch
        {
            return 0;
        }
    }
}

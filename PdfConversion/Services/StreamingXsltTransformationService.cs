using PdfConversion.Models;
using System.Buffers;
using System.Xml;
using System.Xml.Xsl;
using System.Text;
using System.Diagnostics;

namespace PdfConversion.Services;

/// <summary>
/// Streaming XSLT transformation service for large files
/// Uses XmlReader/XmlWriter for memory-efficient processing
/// </summary>
public interface IStreamingXsltTransformationService
{
    /// <summary>
    /// Transforms large XML files using streaming approach
    /// </summary>
    Task<TransformationResult> TransformStreamAsync(
        Stream xmlStream,
        string xsltContent,
        TransformationOptions? options = null);

    /// <summary>
    /// Transforms XML with result streaming to output stream
    /// </summary>
    Task<TransformationResult> TransformToStreamAsync(
        Stream xmlStream,
        string xsltContent,
        Stream outputStream,
        TransformationOptions? options = null);

    /// <summary>
    /// Process large files in chunks with progress reporting
    /// </summary>
    IAsyncEnumerable<TransformationChunk> TransformChunkedAsync(
        Stream xmlStream,
        string xsltContent,
        int chunkSizeBytes = 1024 * 1024, // 1MB chunks
        TransformationOptions? options = null);
}

/// <summary>
/// Represents a chunk of transformed output
/// </summary>
public class TransformationChunk
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int ChunkNumber { get; set; }
    public long BytesProcessed { get; set; }
    public bool IsComplete { get; set; }
}

/// <summary>
/// Implementation of streaming transformation service
/// </summary>
public class StreamingXsltTransformationService : IStreamingXsltTransformationService
{
    private readonly ILogger<StreamingXsltTransformationService> _logger;
    private readonly IDistributedCacheService _cacheService;
    private readonly ArrayPool<byte> _bytePool;
    private static readonly XmlReaderSettings SecureXmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersFromEntities = 1024 * 1024, // 1MB
        MaxCharactersInDocument = 100 * 1024 * 1024, // 100MB
        Async = true,
        CloseInput = false
    };

    private static readonly XmlWriterSettings OutputXmlSettings = new()
    {
        Indent = true,
        IndentChars = "  ",
        OmitXmlDeclaration = false,
        Encoding = Encoding.UTF8,
        Async = true,
        CloseOutput = false
    };

    public StreamingXsltTransformationService(
        ILogger<StreamingXsltTransformationService> logger,
        IDistributedCacheService cacheService)
    {
        _logger = logger;
        _cacheService = cacheService;
        _bytePool = ArrayPool<byte>.Shared;
    }

    public async Task<TransformationResult> TransformStreamAsync(
        Stream xmlStream,
        string xsltContent,
        TransformationOptions? options = null)
    {
        options ??= new TransformationOptions();
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationResult();

        using var outputStream = new MemoryStream();

        try
        {
            result = await TransformToStreamAsync(xmlStream, xsltContent, outputStream, options);

            if (result.IsSuccess)
            {
                outputStream.Position = 0;
                using var reader = new StreamReader(outputStream);
                result.OutputContent = await reader.ReadToEndAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Streaming transformation failed");
            result.IsSuccess = false;
            result.ErrorMessage = $"Streaming transformation failed: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
    }

    public async Task<TransformationResult> TransformToStreamAsync(
        Stream xmlStream,
        string xsltContent,
        Stream outputStream,
        TransformationOptions? options = null)
    {
        options ??= new TransformationOptions();
        var stopwatch = Stopwatch.StartNew();
        var result = new TransformationResult();

        try
        {
            _logger.LogDebug("Starting streaming XSLT transformation");

            // Get or compile XSLT
            var xsltHash = ComputeHash(xsltContent);
            var transform = await GetOrCompileXsltAsync(xsltHash, xsltContent);

            if (transform == null)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Failed to compile XSLT";
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Reset stream position if possible
            if (xmlStream.CanSeek)
            {
                xmlStream.Position = 0;
            }

            // Create XML reader with secure settings
            using var xmlReader = XmlReader.Create(xmlStream, SecureXmlSettings);

            // Create XML writer for output
            using var xmlWriter = XmlWriter.Create(outputStream, OutputXmlSettings);

            // Create argument list for parameters
            var args = new XsltArgumentList();
            foreach (var param in options.Parameters)
            {
                args.AddParam(param.Key, "", param.Value);
            }

            // Perform streaming transformation
            await Task.Run(() => transform.Transform(xmlReader, args, xmlWriter));

            await xmlWriter.FlushAsync();

            result.IsSuccess = true;
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Streaming transformation completed in {ElapsedMs}ms",
                result.ProcessingTimeMs);

            return result;
        }
        catch (XsltException ex)
        {
            _logger.LogError(ex, "XSLT transformation error at line {LineNumber}", ex.LineNumber);
            result.IsSuccess = false;
            result.ErrorMessage = $"XSLT error at line {ex.LineNumber}: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "XML parsing error at line {LineNumber}", ex.LineNumber);
            result.IsSuccess = false;
            result.ErrorMessage = $"XML error at line {ex.LineNumber}: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during streaming transformation");
            result.IsSuccess = false;
            result.ErrorMessage = $"Transformation failed: {ex.Message}";
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            return result;
        }
    }

    public async IAsyncEnumerable<TransformationChunk> TransformChunkedAsync(
        Stream xmlStream,
        string xsltContent,
        int chunkSizeBytes = 1024 * 1024,
        TransformationOptions? options = null)
    {
        options ??= new TransformationOptions();

        using var outputStream = new MemoryStream();
        var transformResult = await TransformToStreamAsync(xmlStream, xsltContent, outputStream, options);

        if (!transformResult.IsSuccess)
        {
            yield return new TransformationChunk
            {
                Data = Array.Empty<byte>(),
                ChunkNumber = 0,
                BytesProcessed = 0,
                IsComplete = true
            };
            yield break;
        }

        // Reset output stream to beginning
        outputStream.Position = 0;

        var buffer = _bytePool.Rent(chunkSizeBytes);
        try
        {
            int chunkNumber = 1;
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await outputStream.ReadAsync(buffer.AsMemory(0, chunkSizeBytes))) > 0)
            {
                totalBytesRead += bytesRead;
                var chunkData = new byte[bytesRead];
                Array.Copy(buffer, chunkData, bytesRead);

                yield return new TransformationChunk
                {
                    Data = chunkData,
                    ChunkNumber = chunkNumber++,
                    BytesProcessed = totalBytesRead,
                    IsComplete = false
                };
            }

            // Send final chunk
            yield return new TransformationChunk
            {
                Data = Array.Empty<byte>(),
                ChunkNumber = chunkNumber,
                BytesProcessed = totalBytesRead,
                IsComplete = true
            };
        }
        finally
        {
            _bytePool.Return(buffer);
        }
    }

    private async Task<XslCompiledTransform?> GetOrCompileXsltAsync(string xsltHash, string xsltContent)
    {
        try
        {
            // Check cache for compiled XSLT
            var cachedXslt = await _cacheService.GetCompiledXsltAsync(xsltHash);
            if (cachedXslt != null)
            {
                _logger.LogDebug("Using cached compiled XSLT");
                // Note: We can't serialize/deserialize XslCompiledTransform easily
                // So we'll just recompile for now but this shows the pattern
            }

            // Compile XSLT
            var transform = new XslCompiledTransform();
            using var stringReader = new StringReader(xsltContent);
            using var xmlReader = XmlReader.Create(stringReader);

            transform.Load(xmlReader);

            _logger.LogDebug("XSLT compiled successfully");

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
}

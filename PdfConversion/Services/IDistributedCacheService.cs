using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Distributed caching service with Redis and in-memory fallback
/// </summary>
public interface IDistributedCacheService
{
    /// <summary>
    /// Gets cached transformation result by content hash
    /// </summary>
    Task<TransformationResult?> GetTransformationResultAsync(string xmlHash, string xsltHash);

    /// <summary>
    /// Caches transformation result with composite key
    /// </summary>
    Task SetTransformationResultAsync(string xmlHash, string xsltHash, TransformationResult result, TimeSpan? expiration = null);

    /// <summary>
    /// Gets compiled XSLT template from cache
    /// </summary>
    Task<byte[]?> GetCompiledXsltAsync(string xsltHash);

    /// <summary>
    /// Caches compiled XSLT template
    /// </summary>
    Task SetCompiledXsltAsync(string xsltHash, byte[] compiledXslt, TimeSpan? expiration = null);

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync();

    /// <summary>
    /// Invalidates cached transformation results for a specific XSLT
    /// </summary>
    Task InvalidateXsltCacheAsync(string xsltHash);

    /// <summary>
    /// Clears all cached data
    /// </summary>
    Task ClearAllAsync();
}

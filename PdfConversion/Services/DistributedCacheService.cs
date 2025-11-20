using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using PdfConversion.Models;
using System.Text.Json;
using System.Collections.Concurrent;

namespace PdfConversion.Services;

/// <summary>
/// Hybrid caching implementation with Redis and in-memory fallback
/// </summary>
public class DistributedCacheService : IDistributedCacheService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly bool _isRedisAvailable;
    private readonly ConcurrentDictionary<string, long> _cacheStats = new();

    private const string STATS_HITS = "stats:hits";
    private const string STATS_MISSES = "stats:misses";
    private const string STATS_TIME_SAVED = "stats:timeSaved";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(30);

    public DistributedCacheService(
        IDistributedCache? distributedCache,
        IMemoryCache memoryCache,
        ILogger<DistributedCacheService> logger)
    {
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _logger = logger;
        _isRedisAvailable = distributedCache != null;

        // Initialize stats
        _cacheStats[STATS_HITS] = 0;
        _cacheStats[STATS_MISSES] = 0;
        _cacheStats[STATS_TIME_SAVED] = 0;

        _logger.LogInformation("Cache service initialized (Redis: {RedisAvailable})", _isRedisAvailable);
    }

    public async Task<TransformationResult?> GetTransformationResultAsync(string xmlHash, string xsltHash)
    {
        var cacheKey = $"transform:{xmlHash}:{xsltHash}";

        try
        {
            // Try memory cache first (fastest)
            if (_memoryCache.TryGetValue<TransformationResult>(cacheKey, out var memCached))
            {
                _cacheStats.AddOrUpdate(STATS_HITS, 1, (k, v) => v + 1);
                _logger.LogDebug("Memory cache hit for {Key}", cacheKey);
                return memCached;
            }

            // Try distributed cache (Redis)
            if (_isRedisAvailable && _distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(cacheKey);
                if (bytes != null && bytes.Length > 0)
                {
                    var result = JsonSerializer.Deserialize<TransformationResult>(bytes);
                    if (result != null)
                    {
                        // Populate memory cache for next time
                        _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                            Size = EstimateSize(result)
                        });

                        _cacheStats.AddOrUpdate(STATS_HITS, 1, (k, v) => v + 1);
                        _cacheStats.AddOrUpdate(STATS_TIME_SAVED, result.ProcessingTimeMs, (k, v) => v + result.ProcessingTimeMs);
                        _logger.LogDebug("Redis cache hit for {Key}", cacheKey);
                        return result;
                    }
                }
            }

            _cacheStats.AddOrUpdate(STATS_MISSES, 1, (k, v) => v + 1);
            _logger.LogDebug("Cache miss for {Key}", cacheKey);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache, treating as miss");
            _cacheStats.AddOrUpdate(STATS_MISSES, 1, (k, v) => v + 1);
            return null;
        }
    }

    public async Task SetTransformationResultAsync(string xmlHash, string xsltHash, TransformationResult result, TimeSpan? expiration = null)
    {
        var cacheKey = $"transform:{xmlHash}:{xsltHash}";
        expiration ??= DefaultExpiration;

        try
        {
            // Always set in memory cache
            _memoryCache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                Size = EstimateSize(result)
            });

            // Set in distributed cache if available
            if (_isRedisAvailable && _distributedCache != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(result);
                await _distributedCache.SetAsync(cacheKey, bytes, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                });

                _logger.LogDebug("Cached transformation result to Redis and Memory: {Key}", cacheKey);
            }
            else
            {
                _logger.LogDebug("Cached transformation result to Memory only: {Key}", cacheKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching transformation result");
        }
    }

    public async Task<byte[]?> GetCompiledXsltAsync(string xsltHash)
    {
        var cacheKey = $"xslt:compiled:{xsltHash}";

        try
        {
            // Try memory cache first
            if (_memoryCache.TryGetValue<byte[]>(cacheKey, out var memCached))
            {
                _cacheStats.AddOrUpdate(STATS_HITS, 1, (k, v) => v + 1);
                return memCached;
            }

            // Try distributed cache
            if (_isRedisAvailable && _distributedCache != null)
            {
                var bytes = await _distributedCache.GetAsync(cacheKey);
                if (bytes != null && bytes.Length > 0)
                {
                    // Populate memory cache
                    _memoryCache.Set(cacheKey, bytes, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                        Size = bytes.Length
                    });

                    _cacheStats.AddOrUpdate(STATS_HITS, 1, (k, v) => v + 1);
                    return bytes;
                }
            }

            _cacheStats.AddOrUpdate(STATS_MISSES, 1, (k, v) => v + 1);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving compiled XSLT from cache");
            _cacheStats.AddOrUpdate(STATS_MISSES, 1, (k, v) => v + 1);
            return null;
        }
    }

    public async Task SetCompiledXsltAsync(string xsltHash, byte[] compiledXslt, TimeSpan? expiration = null)
    {
        var cacheKey = $"xslt:compiled:{xsltHash}";
        expiration ??= TimeSpan.FromHours(1);

        try
        {
            // Set in memory cache
            _memoryCache.Set(cacheKey, compiledXslt, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
                Size = compiledXslt.Length
            });

            // Set in distributed cache if available
            if (_isRedisAvailable && _distributedCache != null)
            {
                await _distributedCache.SetAsync(cacheKey, compiledXslt, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching compiled XSLT");
        }
    }

    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        var hits = _cacheStats.GetValueOrDefault(STATS_HITS, 0);
        var misses = _cacheStats.GetValueOrDefault(STATS_MISSES, 0);
        var timeSaved = _cacheStats.GetValueOrDefault(STATS_TIME_SAVED, 0);

        return await Task.FromResult(new CacheStatistics
        {
            Hits = hits,
            Misses = misses,
            TotalTimeSavedMs = timeSaved,
            AverageTimeSavedMs = hits > 0 ? (double)timeSaved / hits : 0,
            IsRedisAvailable = _isRedisAvailable,
            CacheType = _isRedisAvailable ? "Hybrid (Redis + Memory)" : "Memory Only"
        });
    }

    public async Task InvalidateXsltCacheAsync(string xsltHash)
    {
        try
        {
            var pattern = $"transform:*:{xsltHash}";
            _logger.LogInformation("Invalidating cache for XSLT hash: {XsltHash}", xsltHash);

            // Memory cache - remove by scanning entries (limited capability)
            // Note: MemoryCache doesn't support pattern-based removal, so we just log
            _logger.LogWarning("Memory cache invalidation requires restart or expiration");

            // For Redis, we'd need to use a pattern scan, but that's expensive
            // Better approach: just let the cache expire naturally
            if (_isRedisAvailable && _distributedCache != null)
            {
                // Redis pattern-based removal would go here if we implement it
                await Task.CompletedTask;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating XSLT cache");
        }
    }

    public async Task ClearAllAsync()
    {
        try
        {
            _logger.LogInformation("Clearing all cache data");

            // Clear memory cache stats
            _cacheStats.Clear();
            _cacheStats[STATS_HITS] = 0;
            _cacheStats[STATS_MISSES] = 0;
            _cacheStats[STATS_TIME_SAVED] = 0;

            // Note: Can't easily clear all MemoryCache entries without restart
            // Redis would require FLUSHDB which is dangerous in production

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    private static int EstimateSize(TransformationResult result)
    {
        // Rough estimate: output content size + overhead
        return (result.OutputContent?.Length ?? 0) + 1000;
    }
}

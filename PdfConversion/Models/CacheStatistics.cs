namespace PdfConversion.Models;

/// <summary>
/// Statistics about cache usage and performance
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Total number of cache hits
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Total number of cache misses
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Cache hit rate (0.0 to 1.0)
    /// </summary>
    public double HitRate => (Hits + Misses) > 0 ? (double)Hits / (Hits + Misses) : 0.0;

    /// <summary>
    /// Total number of cached items
    /// </summary>
    public long TotalItems { get; set; }

    /// <summary>
    /// Total memory used by cache in bytes
    /// </summary>
    public long TotalMemoryBytes { get; set; }

    /// <summary>
    /// Average time saved per cache hit in milliseconds
    /// </summary>
    public double AverageTimeSavedMs { get; set; }

    /// <summary>
    /// Total time saved by cache hits in milliseconds
    /// </summary>
    public long TotalTimeSavedMs { get; set; }

    /// <summary>
    /// Whether Redis cache is available
    /// </summary>
    public bool IsRedisAvailable { get; set; }

    /// <summary>
    /// Cache type being used (Redis, Memory, or Hybrid)
    /// </summary>
    public string CacheType { get; set; } = "Memory";
}

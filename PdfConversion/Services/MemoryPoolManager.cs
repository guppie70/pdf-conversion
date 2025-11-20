using Microsoft.Extensions.ObjectPool;
using System.Buffers;
using System.Text;

namespace PdfConversion.Services;

/// <summary>
/// Manages object pools and array pools for memory optimization
/// </summary>
public interface IMemoryPoolManager
{
    /// <summary>
    /// Rent a byte buffer from the array pool
    /// </summary>
    byte[] RentBuffer(int minimumLength);

    /// <summary>
    /// Return a byte buffer to the array pool
    /// </summary>
    void ReturnBuffer(byte[] buffer, bool clearBuffer = true);

    /// <summary>
    /// Rent a StringBuilder from the pool
    /// </summary>
    StringBuilder RentStringBuilder();

    /// <summary>
    /// Return a StringBuilder to the pool
    /// </summary>
    void ReturnStringBuilder(StringBuilder builder);

    /// <summary>
    /// Get memory usage statistics
    /// </summary>
    MemoryStatistics GetMemoryStatistics();
}

/// <summary>
/// Memory usage statistics
/// </summary>
public class MemoryStatistics
{
    public long TotalAllocatedBytes { get; set; }
    public long TotalReturnedBytes { get; set; }
    public long CurrentRentedBytes { get; set; }
    public int BuffersRented { get; set; }
    public int BuffersReturned { get; set; }
    public int StringBuildersRented { get; set; }
    public int StringBuildersReturned { get; set; }
    public double MemoryEfficiencyRatio => TotalReturnedBytes > 0
        ? (double)TotalReturnedBytes / TotalAllocatedBytes
        : 0.0;
}

/// <summary>
/// Implementation of memory pool manager
/// </summary>
public class MemoryPoolManager : IMemoryPoolManager
{
    private readonly ArrayPool<byte> _bytePool;
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    private readonly ILogger<MemoryPoolManager> _logger;

    private long _totalAllocatedBytes;
    private long _totalReturnedBytes;
    private long _currentRentedBytes;
    private int _buffersRented;
    private int _buffersReturned;
    private int _stringBuildersRented;
    private int _stringBuildersReturned;

    public MemoryPoolManager(ILogger<MemoryPoolManager> logger)
    {
        _logger = logger;
        _bytePool = ArrayPool<byte>.Shared;

        // Create StringBuilder pool with custom policy
        var policy = new StringBuilderPooledObjectPolicy
        {
            InitialCapacity = 1024,
            MaximumRetainedCapacity = 64 * 1024 // 64KB max retained
        };
        _stringBuilderPool = new DefaultObjectPool<StringBuilder>(policy);

        _logger.LogInformation("Memory pool manager initialized");
    }

    public byte[] RentBuffer(int minimumLength)
    {
        try
        {
            var buffer = _bytePool.Rent(minimumLength);

            Interlocked.Increment(ref _buffersRented);
            Interlocked.Add(ref _totalAllocatedBytes, buffer.Length);
            Interlocked.Add(ref _currentRentedBytes, buffer.Length);

            _logger.LogTrace("Rented buffer of {Size} bytes (requested {MinSize})",
                buffer.Length, minimumLength);

            return buffer;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rent buffer of size {Size}", minimumLength);
            throw;
        }
    }

    public void ReturnBuffer(byte[] buffer, bool clearBuffer = true)
    {
        try
        {
            if (buffer == null)
                return;

            var size = buffer.Length;
            _bytePool.Return(buffer, clearBuffer);

            Interlocked.Increment(ref _buffersReturned);
            Interlocked.Add(ref _totalReturnedBytes, size);
            Interlocked.Add(ref _currentRentedBytes, -size);

            _logger.LogTrace("Returned buffer of {Size} bytes", size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return buffer");
        }
    }

    public StringBuilder RentStringBuilder()
    {
        try
        {
            var builder = _stringBuilderPool.Get();

            Interlocked.Increment(ref _stringBuildersRented);

            _logger.LogTrace("Rented StringBuilder with capacity {Capacity}", builder.Capacity);

            return builder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rent StringBuilder");
            throw;
        }
    }

    public void ReturnStringBuilder(StringBuilder builder)
    {
        try
        {
            if (builder == null)
                return;

            builder.Clear();
            _stringBuilderPool.Return(builder);

            Interlocked.Increment(ref _stringBuildersReturned);

            _logger.LogTrace("Returned StringBuilder");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to return StringBuilder");
        }
    }

    public MemoryStatistics GetMemoryStatistics()
    {
        return new MemoryStatistics
        {
            TotalAllocatedBytes = Interlocked.Read(ref _totalAllocatedBytes),
            TotalReturnedBytes = Interlocked.Read(ref _totalReturnedBytes),
            CurrentRentedBytes = Interlocked.Read(ref _currentRentedBytes),
            BuffersRented = _buffersRented,
            BuffersReturned = _buffersReturned,
            StringBuildersRented = _stringBuildersRented,
            StringBuildersReturned = _stringBuildersReturned
        };
    }
}

/// <summary>
/// Custom pooled object policy for StringBuilder
/// </summary>
public class StringBuilderPooledObjectPolicy : IPooledObjectPolicy<StringBuilder>
{
    public int InitialCapacity { get; set; } = 1024;
    public int MaximumRetainedCapacity { get; set; } = 64 * 1024;

    public StringBuilder Create()
    {
        return new StringBuilder(InitialCapacity);
    }

    public bool Return(StringBuilder obj)
    {
        if (obj == null)
            return false;

        // Don't retain very large builders
        if (obj.Capacity > MaximumRetainedCapacity)
        {
            return false;
        }

        obj.Clear();
        return true;
    }
}

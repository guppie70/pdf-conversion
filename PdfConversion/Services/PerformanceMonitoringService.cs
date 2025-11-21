using System.Diagnostics;
using System.Collections.Concurrent;

namespace PdfConversion.Services;

/// <summary>
/// Performance metrics for transformations
/// </summary>
public class PerformanceMetrics
{
    public long TotalTransformations { get; set; }
    public long SuccessfulTransformations { get; set; }
    public long FailedTransformations { get; set; }
    public double AverageTransformationTimeMs { get; set; }
    public long MinTransformationTimeMs { get; set; }
    public long MaxTransformationTimeMs { get; set; }
    public long TotalProcessingTimeMs { get; set; }
    public double TransformationsPerSecond { get; set; }
    public long SlowOperations { get; set; } // > 5 seconds
    public long CurrentMemoryUsageMB { get; set; }
    public long PeakMemoryUsageMB { get; set; }
    public Dictionary<string, long> TransformationsByProject { get; set; } = new();
    public List<SlowOperation> RecentSlowOperations { get; set; } = new();
}

/// <summary>
/// Information about a slow operation
/// </summary>
public class SlowOperation
{
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public string? ProjectId { get; set; }
    public string? FileName { get; set; }
    public long MemoryUsedMB { get; set; }
}

/// <summary>
/// Service for monitoring and tracking performance metrics
/// </summary>
public interface IPerformanceMonitoringService
{
    /// <summary>
    /// Start tracking an operation
    /// </summary>
    IDisposable TrackOperation(string operationName, string? projectId = null, string? fileName = null);

    /// <summary>
    /// Record a transformation completion
    /// </summary>
    void RecordTransformation(bool success, long durationMs, string? projectId = null);

    /// <summary>
    /// Get current performance metrics
    /// </summary>
    Task<PerformanceMetrics> GetMetricsAsync();

    /// <summary>
    /// Reset all metrics
    /// </summary>
    Task ResetMetricsAsync();

    /// <summary>
    /// Check if current memory usage is high
    /// </summary>
    bool IsMemoryPressureHigh();
}

/// <summary>
/// Implementation of performance monitoring service
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly ILogger<PerformanceMonitoringService> _logger;
    private readonly Stopwatch _uptimeStopwatch;
    private readonly ConcurrentDictionary<string, long> _projectCounts = new();
    private readonly ConcurrentQueue<SlowOperation> _slowOperations = new();

    private long _totalTransformations;
    private long _successfulTransformations;
    private long _failedTransformations;
    private long _totalProcessingTimeMs;
    private long _minTransformationTimeMs = long.MaxValue;
    private long _maxTransformationTimeMs;
    private long _slowOperationCount;
    private long _peakMemoryUsageMB;

    private const long SlowOperationThresholdMs = 5000; // 5 seconds
    private const int MaxSlowOperationsStored = 100;
    private const long HighMemoryThresholdMB = 1024; // 1GB

    public PerformanceMonitoringService(ILogger<PerformanceMonitoringService> logger)
    {
        _logger = logger;
        _uptimeStopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Performance monitoring service initialized");
    }

    public IDisposable TrackOperation(string operationName, string? projectId = null, string? fileName = null)
    {
        return new OperationTracker(this, operationName, projectId, fileName, _logger);
    }

    public void RecordTransformation(bool success, long durationMs, string? projectId = null)
    {
        Interlocked.Increment(ref _totalTransformations);

        if (success)
        {
            Interlocked.Increment(ref _successfulTransformations);
        }
        else
        {
            Interlocked.Increment(ref _failedTransformations);
        }

        Interlocked.Add(ref _totalProcessingTimeMs, durationMs);

        // Update min/max
        UpdateMinMax(durationMs);

        // Track by project
        if (!string.IsNullOrEmpty(projectId))
        {
            _projectCounts.AddOrUpdate(projectId, 1, (k, v) => v + 1);
        }

        // Check if slow
        if (durationMs > SlowOperationThresholdMs)
        {
            Interlocked.Increment(ref _slowOperationCount);
            _logger.LogWarning("Slow transformation detected: {Duration}ms for project {ProjectId}",
                durationMs, projectId);
        }

        // Track memory
        var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        if (currentMemoryMB > Interlocked.Read(ref _peakMemoryUsageMB))
        {
            Interlocked.Exchange(ref _peakMemoryUsageMB, currentMemoryMB);
        }
    }

    public async Task<PerformanceMetrics> GetMetricsAsync()
    {
        var total = Interlocked.Read(ref _totalTransformations);
        var totalTime = Interlocked.Read(ref _totalProcessingTimeMs);
        var avgTime = total > 0 ? (double)totalTime / total : 0;

        var uptimeSeconds = _uptimeStopwatch.Elapsed.TotalSeconds;
        var transformationsPerSecond = uptimeSeconds > 0 ? total / uptimeSeconds : 0;

        var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        var peakMemoryMB = Interlocked.Read(ref _peakMemoryUsageMB);

        var metrics = new PerformanceMetrics
        {
            TotalTransformations = total,
            SuccessfulTransformations = Interlocked.Read(ref _successfulTransformations),
            FailedTransformations = Interlocked.Read(ref _failedTransformations),
            AverageTransformationTimeMs = avgTime,
            MinTransformationTimeMs = _minTransformationTimeMs == long.MaxValue ? 0 : _minTransformationTimeMs,
            MaxTransformationTimeMs = _maxTransformationTimeMs,
            TotalProcessingTimeMs = totalTime,
            TransformationsPerSecond = transformationsPerSecond,
            SlowOperations = Interlocked.Read(ref _slowOperationCount),
            CurrentMemoryUsageMB = currentMemoryMB,
            PeakMemoryUsageMB = peakMemoryMB,
            TransformationsByProject = new Dictionary<string, long>(_projectCounts),
            RecentSlowOperations = _slowOperations.TakeLast(20).ToList()
        };

        return await Task.FromResult(metrics);
    }

    public async Task ResetMetricsAsync()
    {
        _logger.LogInformation("Resetting performance metrics");

        Interlocked.Exchange(ref _totalTransformations, 0);
        Interlocked.Exchange(ref _successfulTransformations, 0);
        Interlocked.Exchange(ref _failedTransformations, 0);
        Interlocked.Exchange(ref _totalProcessingTimeMs, 0);
        Interlocked.Exchange(ref _minTransformationTimeMs, long.MaxValue);
        Interlocked.Exchange(ref _maxTransformationTimeMs, 0);
        Interlocked.Exchange(ref _slowOperationCount, 0);

        _projectCounts.Clear();
        while (_slowOperations.TryDequeue(out _)) { }

        _uptimeStopwatch.Restart();

        await Task.CompletedTask;
    }

    public bool IsMemoryPressureHigh()
    {
        var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
        return currentMemoryMB > HighMemoryThresholdMB;
    }

    private void UpdateMinMax(long durationMs)
    {
        // Update min
        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minTransformationTimeMs);
            if (durationMs >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minTransformationTimeMs, durationMs, currentMin) != currentMin);

        // Update max
        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxTransformationTimeMs);
            if (durationMs <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxTransformationTimeMs, durationMs, currentMax) != currentMax);
    }

    private void RecordSlowOperation(string operationName, long durationMs, string? projectId, string? fileName)
    {
        var slowOp = new SlowOperation
        {
            Timestamp = DateTime.Now,
            Operation = operationName,
            DurationMs = durationMs,
            ProjectId = projectId,
            FileName = fileName,
            MemoryUsedMB = GC.GetTotalMemory(false) / (1024 * 1024)
        };

        _slowOperations.Enqueue(slowOp);

        // Keep only recent slow operations
        while (_slowOperations.Count > MaxSlowOperationsStored)
        {
            _slowOperations.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Helper class for tracking individual operations
    /// </summary>
    private class OperationTracker : IDisposable
    {
        private readonly PerformanceMonitoringService _service;
        private readonly string _operationName;
        private readonly string? _projectId;
        private readonly string? _fileName;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private readonly long _initialMemoryMB;
        private bool _disposed;

        public OperationTracker(
            PerformanceMonitoringService service,
            string operationName,
            string? projectId,
            string? fileName,
            ILogger logger)
        {
            _service = service;
            _operationName = operationName;
            _projectId = projectId;
            _fileName = fileName;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
            _initialMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

            _logger.LogTrace("Started tracking operation: {Operation}", operationName);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _stopwatch.Stop();
            var durationMs = _stopwatch.ElapsedMilliseconds;
            var finalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
            var memoryDeltaMB = finalMemoryMB - _initialMemoryMB;

            _logger.LogTrace("Completed operation: {Operation} in {Duration}ms (Memory delta: {MemoryDelta}MB)",
                _operationName, durationMs, memoryDeltaMB);

            // Check if slow
            if (durationMs > SlowOperationThresholdMs)
            {
                _service.RecordSlowOperation(_operationName, durationMs, _projectId, _fileName);
                _logger.LogWarning("Slow operation: {Operation} took {Duration}ms", _operationName, durationMs);
            }

            _disposed = true;
        }
    }
}

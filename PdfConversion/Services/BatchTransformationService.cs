using PdfConversion.Models;
using System.Threading.Channels;
using System.Collections.Concurrent;

namespace PdfConversion.Services;

/// <summary>
/// Request for batch transformation
/// </summary>
public class BatchTransformationRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = string.Empty;
    public List<string> FileNames { get; set; } = new();
    public string XsltContent { get; set; } = string.Empty;
    public TransformationOptions Options { get; set; } = new();
    public IProgress<BatchTransformationProgress>? Progress { get; set; }
}

/// <summary>
/// Progress information for batch transformation
/// </summary>
public class BatchTransformationProgress
{
    public string RequestId { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int FilesCompleted { get; set; }
    public int FilesSucceeded { get; set; }
    public int FilesFailed { get; set; }
    public string? CurrentFile { get; set; }
    public double PercentComplete => TotalFiles > 0 ? (double)FilesCompleted / TotalFiles * 100 : 0;
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public bool IsComplete { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of batch transformation
/// </summary>
public class BatchTransformationResult
{
    public string RequestId { get; set; } = string.Empty;
    public int TotalFiles { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public Dictionary<string, TransformationResult> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Background service for batch transformations using producer-consumer pattern
/// </summary>
public interface IBatchTransformationService
{
    /// <summary>
    /// Queue batch transformation request
    /// </summary>
    Task<string> QueueBatchTransformationAsync(BatchTransformationRequest request);

    /// <summary>
    /// Get progress of batch transformation
    /// </summary>
    Task<BatchTransformationProgress?> GetProgressAsync(string requestId);

    /// <summary>
    /// Get result of completed batch transformation
    /// </summary>
    Task<BatchTransformationResult?> GetResultAsync(string requestId);

    /// <summary>
    /// Cancel batch transformation
    /// </summary>
    Task CancelBatchTransformationAsync(string requestId);

    /// <summary>
    /// Get statistics about the processing queue
    /// </summary>
    Task<QueueStatistics> GetQueueStatisticsAsync();
}

/// <summary>
/// Queue statistics
/// </summary>
public class QueueStatistics
{
    public int QueuedRequests { get; set; }
    public int ProcessingRequests { get; set; }
    public int CompletedRequests { get; set; }
    public int FailedRequests { get; set; }
    public int TotalProcessed { get; set; }
    public double AverageProcessingTimeMs { get; set; }
}

/// <summary>
/// Implementation of batch transformation service
/// </summary>
public class BatchTransformationService : IBatchTransformationService, IDisposable
{
    private readonly Channel<BatchTransformationRequest> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchTransformationService> _logger;
    private readonly ConcurrentDictionary<string, BatchTransformationProgress> _progress = new();
    private readonly ConcurrentDictionary<string, BatchTransformationResult> _results = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
    private readonly Task[] _workers;
    private readonly int _maxConcurrency;
    private long _totalProcessed;
    private long _totalProcessingTimeMs;

    public BatchTransformationService(
        IServiceProvider serviceProvider,
        ILogger<BatchTransformationService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Configure channel with bounded capacity
        var channelOptions = new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _channel = Channel.CreateBounded<BatchTransformationRequest>(channelOptions);

        // Configure concurrency level
        _maxConcurrency = configuration.GetValue<int>("BatchProcessing:MaxConcurrency", 4);
        _logger.LogInformation("Batch transformation service initialized with concurrency: {Concurrency}",
            _maxConcurrency);

        // Start worker tasks
        _workers = new Task[_maxConcurrency];
        for (int i = 0; i < _maxConcurrency; i++)
        {
            _workers[i] = Task.Run(() => ProcessBatchesAsync(i));
        }
    }

    public async Task<string> QueueBatchTransformationAsync(BatchTransformationRequest request)
    {
        request.RequestId = Guid.NewGuid().ToString();

        _logger.LogInformation("Queuing batch transformation {RequestId} with {FileCount} files",
            request.RequestId, request.FileNames.Count);

        // Initialize progress
        _progress[request.RequestId] = new BatchTransformationProgress
        {
            RequestId = request.RequestId,
            TotalFiles = request.FileNames.Count,
            FilesCompleted = 0,
            FilesSucceeded = 0,
            FilesFailed = 0
        };

        // Create cancellation token
        _cancellationTokens[request.RequestId] = new CancellationTokenSource();

        // Queue the request
        await _channel.Writer.WriteAsync(request);

        return request.RequestId;
    }

    public async Task<BatchTransformationProgress?> GetProgressAsync(string requestId)
    {
        await Task.CompletedTask;
        return _progress.TryGetValue(requestId, out var progress) ? progress : null;
    }

    public async Task<BatchTransformationResult?> GetResultAsync(string requestId)
    {
        await Task.CompletedTask;
        return _results.TryGetValue(requestId, out var result) ? result : null;
    }

    public async Task CancelBatchTransformationAsync(string requestId)
    {
        if (_cancellationTokens.TryGetValue(requestId, out var cts))
        {
            _logger.LogInformation("Cancelling batch transformation {RequestId}", requestId);
            cts.Cancel();
        }

        await Task.CompletedTask;
    }

    public async Task<QueueStatistics> GetQueueStatisticsAsync()
    {
        var queued = _channel.Reader.Count;
        var processing = _progress.Count(p => !p.Value.IsComplete);
        var completed = _results.Count(r => r.Value.ErrorCount == 0);
        var failed = _results.Count(r => r.Value.ErrorCount > 0);
        var avgTime = _totalProcessed > 0 ? (double)_totalProcessingTimeMs / _totalProcessed : 0;

        return await Task.FromResult(new QueueStatistics
        {
            QueuedRequests = queued,
            ProcessingRequests = processing,
            CompletedRequests = completed,
            FailedRequests = failed,
            TotalProcessed = (int)_totalProcessed,
            AverageProcessingTimeMs = avgTime
        });
    }

    private async Task ProcessBatchesAsync(int workerId)
    {
        _logger.LogInformation("Batch worker {WorkerId} started", workerId);

        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await ProcessBatchRequestAsync(request, workerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} failed to process batch {RequestId}",
                    workerId, request.RequestId);
            }
        }

        _logger.LogInformation("Batch worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessBatchRequestAsync(BatchTransformationRequest request, int workerId)
    {
        var requestId = request.RequestId;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("Worker {WorkerId} processing batch {RequestId}",
            workerId, requestId);

        // Get cancellation token
        var cts = _cancellationTokens.GetValueOrDefault(requestId);
        if (cts == null)
        {
            _logger.LogWarning("No cancellation token found for {RequestId}", requestId);
            return;
        }

        var result = new BatchTransformationResult
        {
            RequestId = requestId,
            TotalFiles = request.FileNames.Count
        };

        try
        {
            // Create a scope for scoped services
            using var scope = _serviceProvider.CreateScope();
            var transformService = scope.ServiceProvider.GetRequiredService<IXsltTransformationService>();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectManagementService>();

            // Process files in parallel with limited concurrency
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(4, request.FileNames.Count),
                CancellationToken = cts.Token
            };

            await Parallel.ForEachAsync(request.FileNames, parallelOptions, async (fileName, ct) =>
            {
                try
                {
                    // Update progress
                    UpdateProgress(requestId, fileName);

                    // Read file
                    var xmlContent = await projectService.ReadInputFileAsync(request.ProjectId, fileName);

                    // Transform
                    var transformResult = await transformService.TransformAsync(
                        xmlContent,
                        request.XsltContent,
                        request.Options);

                    // Store result
                    lock (result.Results)
                    {
                        result.Results[fileName] = transformResult;
                        if (transformResult.IsSuccess)
                        {
                            result.SuccessCount++;
                            IncrementProgress(requestId, true);
                        }
                        else
                        {
                            result.ErrorCount++;
                            result.Errors.Add($"{fileName}: {transformResult.ErrorMessage}");
                            IncrementProgress(requestId, false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing file {FileName} in batch {RequestId}",
                        fileName, requestId);

                    lock (result.Results)
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"{fileName}: {ex.Message}");
                        IncrementProgress(requestId, false);
                    }
                }
            });

            stopwatch.Stop();
            result.TotalDuration = stopwatch.Elapsed;

            // Store result
            _results[requestId] = result;

            // Mark progress as complete
            if (_progress.TryGetValue(requestId, out var progress))
            {
                progress.IsComplete = true;
                progress.ElapsedTime = stopwatch.Elapsed;
            }

            // Update statistics
            Interlocked.Increment(ref _totalProcessed);
            Interlocked.Add(ref _totalProcessingTimeMs, stopwatch.ElapsedMilliseconds);

            _logger.LogInformation(
                "Worker {WorkerId} completed batch {RequestId}: {Success}/{Total} succeeded in {Duration}ms",
                workerId, requestId, result.SuccessCount, result.TotalFiles, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch {RequestId} was cancelled", requestId);
            result.Errors.Add("Batch processing was cancelled");
            _results[requestId] = result;
        }
        finally
        {
            // Cleanup
            _cancellationTokens.TryRemove(requestId, out _);
        }
    }

    private void UpdateProgress(string requestId, string currentFile)
    {
        if (_progress.TryGetValue(requestId, out var progress))
        {
            progress.CurrentFile = currentFile;
        }
    }

    private void IncrementProgress(string requestId, bool success)
    {
        if (_progress.TryGetValue(requestId, out var progress))
        {
            progress.FilesCompleted++;
            if (success)
                progress.FilesSucceeded++;
            else
                progress.FilesFailed++;

            // Calculate estimated time remaining
            if (progress.FilesCompleted > 0)
            {
                var avgTimePerFile = progress.ElapsedTime.TotalMilliseconds / progress.FilesCompleted;
                var remainingFiles = progress.TotalFiles - progress.FilesCompleted;
                progress.EstimatedTimeRemaining = TimeSpan.FromMilliseconds(avgTimePerFile * remainingFiles);
            }
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Shutting down batch transformation service");

        // Signal completion
        _channel.Writer.Complete();

        // Wait for workers to complete
        Task.WaitAll(_workers, TimeSpan.FromSeconds(30));

        // Cancel any remaining operations
        foreach (var cts in _cancellationTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _cancellationTokens.Clear();
        _progress.Clear();
        _results.Clear();

        GC.SuppressFinalize(this);
    }
}

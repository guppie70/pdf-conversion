using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PdfConversion.Hubs;
using PdfConversion.Models;

namespace PdfConversion.Services;

/// <summary>
/// Background service that polls Docling job status and pushes updates via SignalR.
///
/// Tracks active jobs and polls the Docling service at regular intervals.
/// Pushes updates to subscribed clients via DoclingProgressHub.
/// </summary>
public class DoclingJobPollingService : BackgroundService
{
    private readonly ILogger<DoclingJobPollingService> _logger;
    private readonly IHubContext<DoclingProgressHub> _hubContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);

    // Tracks active jobs: jobId -> (userId, lastStatus)
    private readonly ConcurrentDictionary<string, JobTrackingInfo> _activeJobs = new();

    public DoclingJobPollingService(
        ILogger<DoclingJobPollingService> logger,
        IHubContext<DoclingProgressHub> hubContext,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Start tracking a job for a specific user.
    /// </summary>
    public void StartTracking(string jobId, string userId)
    {
        var info = new JobTrackingInfo
        {
            UserId = userId,
            LastPolledAt = DateTime.UtcNow,
            LastStatus = DoclingJobStatus.Queued
        };

        _activeJobs.TryAdd(jobId, info);
        _logger.LogInformation($"Started tracking job {jobId} for user {userId}");
    }

    /// <summary>
    /// Stop tracking a job.
    /// </summary>
    public void StopTracking(string jobId)
    {
        _activeJobs.TryRemove(jobId, out _);
        _logger.LogInformation($"Stopped tracking job {jobId}");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DoclingJobPollingService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollActiveJobs(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in polling loop");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("DoclingJobPollingService stopping");
    }

    private async Task PollActiveJobs(CancellationToken stoppingToken)
    {
        if (_activeJobs.IsEmpty)
        {
            return;
        }

        var tasks = _activeJobs.Keys.Select(jobId => PollJob(jobId, stoppingToken)).ToList();
        await Task.WhenAll(tasks);
    }

    private async Task PollJob(string jobId, CancellationToken stoppingToken)
    {
        try
        {
            if (!_activeJobs.TryGetValue(jobId, out var trackingInfo))
            {
                return;
            }

            // Create HTTP client for Docling service
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("http://docling-service:4808");
            client.Timeout = TimeSpan.FromSeconds(5);

            // Poll job status
            var response = await client.GetAsync($"/jobs/{jobId}", stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to poll job {jobId}: {response.StatusCode}");
                return;
            }

            var jobInfo = await response.Content.ReadFromJsonAsync<DoclingJobInfo>(
                cancellationToken: stoppingToken);

            if (jobInfo == null)
            {
                _logger.LogWarning($"Received null job info for {jobId}");
                return;
            }

            // Update tracking info
            trackingInfo.LastPolledAt = DateTime.UtcNow;
            trackingInfo.LastStatus = jobInfo.Status;

            // Push update to SignalR clients
            await _hubContext.Clients.Group($"job_{jobId}")
                .SendAsync("ProgressUpdate", jobInfo, stoppingToken);

            _logger.LogDebug(
                $"Job {jobId} progress: {jobInfo.Progress:P0} - {jobInfo.Message}");

            // Stop tracking if job is complete
            if (jobInfo.Status == DoclingJobStatus.Completed ||
                jobInfo.Status == DoclingJobStatus.Failed ||
                jobInfo.Status == DoclingJobStatus.Cancelled)
            {
                _logger.LogInformation(
                    $"Job {jobId} finished with status {jobInfo.Status}");

                // Keep tracking for a bit longer to ensure client receives final update
                _ = Task.Delay(TimeSpan.FromSeconds(10), stoppingToken)
                    .ContinueWith(_ => StopTracking(jobId), stoppingToken);
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error polling job {jobId}");
        }
    }

    private class JobTrackingInfo
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime LastPolledAt { get; set; }
        public DoclingJobStatus LastStatus { get; set; }
    }
}

/// <summary>
/// Service interface for managing Docling job tracking.
/// </summary>
public interface IDoclingJobPollingService
{
    void StartTracking(string jobId, string userId);
    void StopTracking(string jobId);
}

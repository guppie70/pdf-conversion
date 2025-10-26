using Microsoft.AspNetCore.SignalR;

namespace PdfConversion.Hubs;

/// <summary>
/// SignalR hub for real-time Docling conversion progress updates.
///
/// Allows clients to subscribe to job progress and receive live updates
/// without polling from the browser.
/// </summary>
public class DoclingProgressHub : Hub
{
    private readonly ILogger<DoclingProgressHub> _logger;

    public DoclingProgressHub(ILogger<DoclingProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to progress updates for a specific job.
    /// </summary>
    /// <param name="jobId">Job identifier to track</param>
    public async Task SubscribeToJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job_{jobId}");
        _logger.LogInformation($"Client {Context.ConnectionId} subscribed to job {jobId}");
    }

    /// <summary>
    /// Unsubscribe from progress updates for a specific job.
    /// </summary>
    /// <param name="jobId">Job identifier to stop tracking</param>
    public async Task UnsubscribeFromJob(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job_{jobId}");
        _logger.LogInformation($"Client {Context.ConnectionId} unsubscribed from job {jobId}");
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation($"Client {Context.ConnectionId} connected to DoclingProgressHub");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, $"Client {Context.ConnectionId} disconnected with error");
        }
        else
        {
            _logger.LogInformation($"Client {Context.ConnectionId} disconnected");
        }
        await base.OnDisconnectedAsync(exception);
    }
}

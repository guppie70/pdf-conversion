using PdfConversion.Models;
using System.Collections.Concurrent;

namespace PdfConversion.Services;

/// <summary>
/// Service for managing transformation logs
/// </summary>
public interface ITransformationLogService
{
    /// <summary>
    /// Log a transformation operation
    /// </summary>
    void LogTransformation(TransformationLog log);

    /// <summary>
    /// Get logs with optional filters
    /// </summary>
    IEnumerable<TransformationLog> GetLogs(string? projectId = null, string? status = null, DateTime? fromDate = null);

    /// <summary>
    /// Get the most recent logs
    /// </summary>
    IEnumerable<TransformationLog> GetRecentLogs(int count = 100);

    /// <summary>
    /// Clear all logs
    /// </summary>
    void ClearLogs();

    /// <summary>
    /// Get total count of logs in memory
    /// </summary>
    int GetTotalCount();
}

/// <summary>
/// Implementation of transformation log service
/// </summary>
public class TransformationLogService : ITransformationLogService
{
    private readonly ConcurrentQueue<TransformationLog> _logs = new();
    private readonly ILogger<TransformationLogService> _logger;
    private const int MaxLogsInMemory = 10000;

    public TransformationLogService(ILogger<TransformationLogService> logger)
    {
        _logger = logger;
    }

    public void LogTransformation(TransformationLog log)
    {
        log.Id = Guid.NewGuid();
        log.StartTime = log.StartTime == default ? DateTime.Now : log.StartTime;

        _logs.Enqueue(log);

        // Keep only last N logs in memory
        while (_logs.Count > MaxLogsInMemory)
        {
            _logs.TryDequeue(out _);
        }

        // Also log to Serilog for persistence
        var level = log.Status switch
        {
            "Success" => LogLevel.Information,
            "Error" => LogLevel.Error,
            "Warning" => LogLevel.Warning,
            _ => LogLevel.Debug
        };

        _logger.Log(level,
            "Transformation {Status} - Project: {ProjectId}, File: {FileName}, Duration: {Duration}ms, Details: {Details}",
            log.Status, log.ProjectId, log.FileName,
            (log.EndTime - log.StartTime).TotalMilliseconds, log.Details);
    }

    public IEnumerable<TransformationLog> GetLogs(string? projectId = null, string? status = null, DateTime? fromDate = null)
    {
        var query = _logs.AsEnumerable();

        if (!string.IsNullOrEmpty(projectId))
            query = query.Where(l => l.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        if (fromDate.HasValue)
            query = query.Where(l => l.StartTime >= fromDate.Value);

        return query.OrderByDescending(l => l.StartTime).ToList();
    }

    public IEnumerable<TransformationLog> GetRecentLogs(int count = 100)
    {
        return _logs.OrderByDescending(l => l.StartTime).Take(count).ToList();
    }

    public void ClearLogs()
    {
        _logs.Clear();
        _logger.LogInformation("Transformation logs cleared");
    }

    public int GetTotalCount()
    {
        return _logs.Count;
    }
}

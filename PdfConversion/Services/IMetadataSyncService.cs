namespace PdfConversion.Services;

/// <summary>
/// Service for auto-syncing project metadata with filesystem
/// </summary>
public interface IMetadataSyncService : IDisposable
{
    /// <summary>
    /// Manually trigger metadata sync
    /// </summary>
    Task SyncMetadataAsync();
}

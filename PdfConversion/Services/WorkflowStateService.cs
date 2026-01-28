using Microsoft.Extensions.Logging;

namespace PdfConversion.Services;

/// <summary>
/// Scoped service that tracks workflow outputs for cross-page navigation.
/// Lives for the duration of the user's Blazor circuit (session).
///
/// IMPORTANT: URL is the single source of truth for page state.
/// This service only tracks:
/// 1. Current project context (for clearing state on project switch)
/// 2. Workflow outputs (files saved by each stage for use by subsequent stages)
///
/// Pages should NOT sync their URL parameters to this service.
/// MainLayout uses the output tracking to build navigation URLs with appropriate defaults.
/// </summary>
public class WorkflowStateService
{
    private readonly ILogger<WorkflowStateService>? _logger;

    public WorkflowStateService(ILogger<WorkflowStateService>? logger = null)
    {
        _logger = logger;
    }

    // Current project context
    public string? CurrentCustomer { get; private set; }
    public string? CurrentProjectId { get; private set; }

    // Workflow output tracking (files saved by each stage)
    // These are used by MainLayout to build nav URLs with sensible defaults
    public string? LastNormalizedOutput { get; private set; }  // Path to last saved normalized.xml
    public string? LastHierarchyOutput { get; private set; }   // Path to last saved hierarchy.xml

    // Change notification for MainLayout to rebuild nav URLs
    public event Action? OnChange;

    private void NotifyStateChanged() => OnChange?.Invoke();

    /// <summary>
    /// Track the output from Transform page save operation.
    /// Called when user saves a normalized XML file.
    /// </summary>
    public void SetTransformOutput(string normalizedPath)
    {
        _logger?.LogInformation("[WorkflowState] SetTransformOutput: {Path}", normalizedPath);
        LastNormalizedOutput = normalizedPath;
        NotifyStateChanged();
    }

    /// <summary>
    /// Track the output from Hierarchy page save operation.
    /// Called when user saves a hierarchy XML file.
    /// </summary>
    public void SetHierarchyOutput(string hierarchyPath)
    {
        _logger?.LogInformation("[WorkflowState] SetHierarchyOutput: {Path}", hierarchyPath);
        LastHierarchyOutput = hierarchyPath;
        NotifyStateChanged();
    }

    /// <summary>
    /// Set project context. Clears workflow outputs when switching between projects.
    /// Called by pages in OnParametersSetAsync.
    /// </summary>
    public void SetProject(string customer, string projectId)
    {
        // Only clear outputs if we're switching FROM one project TO another
        // Don't clear if this is the first time setting project (CurrentCustomer is null)
        var isProjectSwitch = CurrentCustomer != null && CurrentProjectId != null
            && (CurrentCustomer != customer || CurrentProjectId != projectId);

        _logger?.LogInformation("[WorkflowState] SetProject: customer={Customer}, projectId={ProjectId}, isSwitch={IsSwitch}",
            customer, projectId, isProjectSwitch);

        if (isProjectSwitch)
        {
            _logger?.LogInformation("[WorkflowState] Project switch detected - clearing workflow outputs");
            Clear();
        }

        CurrentCustomer = customer;
        CurrentProjectId = projectId;
    }

    /// <summary>
    /// Clear workflow outputs (e.g., when switching projects)
    /// </summary>
    public void Clear()
    {
        LastNormalizedOutput = null;
        LastHierarchyOutput = null;
        NotifyStateChanged();
    }
}

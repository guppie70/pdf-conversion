namespace PdfConversion.Models;

/// <summary>
/// User preferences for UI settings and non-workflow page state.
/// NOTE: Page-specific state (file selections, view modes) is managed via URL + WorkflowStateService.
/// This model only stores cross-session UI preferences.
/// </summary>
public class UserSelection
{
    // ===========================================
    // UI Preferences (persisted across sessions)
    // ===========================================

    /// <summary>
    /// Last saved X position of validation modal (null = centered by Bootstrap)
    /// </summary>
    public int? ValidationModalX { get; set; }

    /// <summary>
    /// Last saved Y position of validation modal (null = centered by Bootstrap)
    /// </summary>
    public int? ValidationModalY { get; set; }

    // ===========================================
    // Docling Convert Page Preferences
    // ===========================================

    /// <summary>
    /// Last selected pipeline for transform page (e.g., "adobe", "docling")
    /// </summary>
    public string LastSelectedPipeline { get; set; } = string.Empty;

    /// <summary>
    /// Last selected file mode on DoclingConvert page ("NewUpload" or "ExistingFile")
    /// </summary>
    public string LastDoclingFileMode { get; set; } = "NewUpload";

    /// <summary>
    /// Last selected project on DoclingConvert page in "customer/project-id" format (e.g., "optiver/ar24-3" or "taxxor/ar25-1")
    /// </summary>
    public string LastDoclingProject { get; set; } = string.Empty;

    /// <summary>
    /// Last selected existing file path on DoclingConvert page (when in ExistingFile mode)
    /// </summary>
    public string LastDoclingExistingFile { get; set; } = string.Empty;

    /// <summary>
    /// Last selected output format on DoclingConvert page ("docbook", "html", or "markdown")
    /// </summary>
    public string LastDoclingOutputFormat { get; set; } = "docbook";

    // ===========================================
    // AI Training Preferences
    // ===========================================

    /// <summary>
    /// Last selected training hierarchy file paths for AI modal training material selections
    /// (e.g., ["/app/data/training-material/hierarchies/optiver/ar24-1/hierarchy.xml"])
    /// </summary>
    public List<string> LastSelectedTrainingHierarchies { get; set; } = new();

    // ===========================================
    // LEGACY (kept for JSON deserialization compatibility)
    // ===========================================

    /// <summary>
    /// DEPRECATED: No longer used - kept for JSON file compatibility
    /// </summary>
    public string LastSelectedProject { get; set; } = string.Empty;

    /// <summary>
    /// DEPRECATED: No longer used - kept for JSON file compatibility
    /// </summary>
    public Dictionary<string, ProjectSelection> Projects { get; set; } = new();

    /// <summary>
    /// DEPRECATED: No longer used - kept for JSON file compatibility
    /// </summary>
    public string LastSelectedSourceXml { get; set; } = string.Empty;

    /// <summary>
    /// DEPRECATED: No longer used - kept for JSON file compatibility
    /// </summary>
    public string LastSelectedHierarchyXml { get; set; } = string.Empty;
}

/// <summary>
/// DEPRECATED: Project-scoped selections - no longer used.
/// Kept for JSON deserialization compatibility with existing user-selections.json files.
/// Page state is now managed via URL + WorkflowStateService.
/// </summary>
public class ProjectSelection
{
    public string? SourceFile { get; set; }
    public string? HierarchyFile { get; set; }
    public string? XsltFile { get; set; }
    public string? ViewMode { get; set; }
    public string? HierarchyMode { get; set; }
    public DateTime LastAccessed { get; set; }
}

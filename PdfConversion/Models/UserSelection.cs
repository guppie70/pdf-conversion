namespace PdfConversion.Models;

/// <summary>
/// User's last selected files for transformation and conversion workflows
/// </summary>
public class UserSelection
{
    /// <summary>
    /// Last selected project in "customer/project-id" format (e.g., "optiver/ar24-3" or "taxxor/ar25-1")
    /// </summary>
    public string LastSelectedProject { get; set; } = string.Empty;

    /// <summary>
    /// NEW: Project-scoped selections (key: "customer/projectId")
    /// </summary>
    public Dictionary<string, ProjectSelection> Projects { get; set; } = new();

    /// <summary>
    /// LEGACY: Last selected source XML filename (e.g., "input.xml")
    /// Kept for backward compatibility during migration
    /// </summary>
    public string LastSelectedSourceXml { get; set; } = string.Empty;

    /// <summary>
    /// LEGACY: Last selected hierarchy XML filename for Convert page (e.g., "hierarchy.xml")
    /// Kept for backward compatibility during migration
    /// </summary>
    public string LastSelectedHierarchyXml { get; set; } = string.Empty;

    /// <summary>
    /// Last saved X position of validation modal (null = centered by Bootstrap)
    /// </summary>
    public int? ValidationModalX { get; set; }

    /// <summary>
    /// Last saved Y position of validation modal (null = centered by Bootstrap)
    /// </summary>
    public int? ValidationModalY { get; set; }

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

    /// <summary>
    /// Last selected training hierarchy file paths for AI modal training material selections
    /// (e.g., ["/app/data/training-material/hierarchies/optiver/ar24-1/hierarchy.xml"])
    /// </summary>
    public List<string> LastSelectedTrainingHierarchies { get; set; } = new();
}

/// <summary>
/// Project-scoped selections for a specific customer/projectId combination
/// </summary>
public class ProjectSelection
{
    /// <summary>
    /// Selected source file (e.g., "input.xml", "normalized/docling-pdf.xml")
    /// </summary>
    public string? SourceFile { get; set; }

    /// <summary>
    /// Selected hierarchy file (e.g., "hierarchy.xml", "hierarchy-pdf-xml.xml")
    /// </summary>
    public string? HierarchyFile { get; set; }

    /// <summary>
    /// Selected XSLT file (e.g., "transformation.xslt", "modules/headers.xslt")
    /// </summary>
    public string? XsltFile { get; set; }

    /// <summary>
    /// View mode preference ("source" or "rendered")
    /// </summary>
    public string? ViewMode { get; set; }

    /// <summary>
    /// Hierarchy editing mode ("restricted", "free", or "manual")
    /// </summary>
    public string? HierarchyMode { get; set; }

    /// <summary>
    /// Last time this project was accessed
    /// </summary>
    public DateTime LastAccessed { get; set; }
}

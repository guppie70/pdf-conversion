namespace PdfConversion.Models;

/// <summary>
/// User's last selected files for transformation and conversion workflows
/// </summary>
public class UserSelection
{
    /// <summary>
    /// Last selected project ID (e.g., "ar24-3")
    /// </summary>
    public string LastSelectedProject { get; set; } = string.Empty;

    /// <summary>
    /// Last selected source XML filename (e.g., "input.xml")
    /// </summary>
    public string LastSelectedSourceXml { get; set; } = string.Empty;

    /// <summary>
    /// Last selected XSLT filename (e.g., "transformation.xslt")
    /// </summary>
    public string LastSelectedXslt { get; set; } = "transformation.xslt";

    /// <summary>
    /// Last selected hierarchy XML filename for Convert page (e.g., "hierarchy.xml")
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
    /// Last selected project ID on DoclingConvert page
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
}

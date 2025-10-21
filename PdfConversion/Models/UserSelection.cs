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
}

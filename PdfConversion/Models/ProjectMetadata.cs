using System.Text.Json.Serialization;

namespace PdfConversion.Models;

public class ProjectMetadata
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProjectLifecycleStatus Status { get; set; } = ProjectLifecycleStatus.Open;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Supported language codes for project content
    /// </summary>
    public static readonly string[] SupportedLanguages = { "en", "nl", "de", "fr" };

    /// <summary>
    /// Display names for supported languages
    /// </summary>
    public static readonly Dictionary<string, string> LanguageDisplayNames = new()
    {
        { "en", "English" },
        { "nl", "Dutch (Nederlands)" },
        { "de", "German (Deutsch)" },
        { "fr", "French (Français)" }
    };
}

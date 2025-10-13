using System.Xml.Linq;

namespace PdfConversion.Services;

/// <summary>
/// Service for normalizing header levels in extracted content.
/// Ensures content always starts with h1 and maintains proper hierarchy.
/// </summary>
public class HeaderNormalizationService : IHeaderNormalizationService
{
    private readonly ILogger<HeaderNormalizationService> _logger;
    private static readonly string[] HeaderTags = { "h1", "h2", "h3", "h4", "h5", "h6" };

    public HeaderNormalizationService(ILogger<HeaderNormalizationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Normalizes all header levels in the content to ensure the first header is h1
    /// and maintains proper hierarchy throughout.
    /// </summary>
    /// <param name="content">The XML document containing headers to normalize</param>
    /// <returns>A new XDocument with normalized header levels</returns>
    /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
    public XDocument NormalizeHeaders(XDocument content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        var shiftAmount = CalculateShiftAmount(content);

        if (shiftAmount == 0)
        {
            _logger.LogDebug("No header normalization needed");
            return content;
        }

        _logger.LogInformation("Normalizing headers with shift amount: {ShiftAmount}", shiftAmount);

        // Clone the document to avoid modifying original
        var normalized = new XDocument(content);

        // Find all headers (h1-h6)
        var headers = normalized.Descendants()
            .Where(e => HeaderTags.Contains(e.Name.LocalName))
            .ToList();

        if (headers.Count == 0)
        {
            _logger.LogWarning("No headers found during normalization");
            return normalized;
        }

        foreach (var header in headers)
        {
            var currentLevel = int.Parse(header.Name.LocalName.Substring(1));
            var newLevel = Clamp(currentLevel + shiftAmount, 1, 6);

            _logger.LogDebug("  {CurrentHeader} -> h{NewLevel}: '{HeaderText}'",
                header.Name.LocalName, newLevel, TruncateText(header.Value));

            // Create new header element with shifted level
            var newHeader = new XElement(
                XName.Get($"h{newLevel}", header.Name.NamespaceName),
                header.Attributes(),
                header.Nodes());

            header.ReplaceWith(newHeader);
        }

        _logger.LogInformation("Normalized {HeaderCount} headers", headers.Count);

        return normalized;
    }

    /// <summary>
    /// Calculates the shift amount needed to normalize headers.
    /// For example, if first header is h3, returns -2 (to shift to h1).
    /// </summary>
    /// <param name="content">The XML document to analyze</param>
    /// <returns>The shift amount (negative to shift down, positive to shift up)</returns>
    /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
    public int CalculateShiftAmount(XDocument content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        // Find first header in document
        var firstHeader = content.Descendants()
            .FirstOrDefault(e => HeaderTags.Contains(e.Name.LocalName));

        if (firstHeader == null)
        {
            _logger.LogWarning("No headers found in content");
            return 0;
        }

        var currentLevel = int.Parse(firstHeader.Name.LocalName.Substring(1));
        var targetLevel = 1; // Always normalize to h1
        var shift = targetLevel - currentLevel;

        _logger.LogDebug("First header is {CurrentHeader}, shift amount: {ShiftAmount}",
            firstHeader.Name.LocalName, shift);

        return shift;
    }

    /// <summary>
    /// Clamps a value between a minimum and maximum.
    /// </summary>
    private int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// Truncates text for logging purposes.
    /// </summary>
    private string TruncateText(string text, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Remove newlines and extra whitespace
        text = string.Join(" ", text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()));

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength) + "...";
    }
}

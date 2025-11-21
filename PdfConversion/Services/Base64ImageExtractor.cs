using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using PdfConversion.Utils;

namespace PdfConversion.Services;

/// <summary>
/// Service for extracting base64-embedded images from HTML content.
/// </summary>
public interface IBase64ImageExtractor
{
    /// <summary>
    /// Extracts base64-embedded images from HTML content and saves them as PNG files.
    /// Replaces base64 src attributes with file references.
    /// </summary>
    /// <param name="htmlContent">HTML content with base64-embedded images</param>
    /// <param name="projectImagesPath">Directory to save images (e.g., data/input/{customer}/projects/{project-id}/images/from-conversion/)</param>
    /// <param name="baseFilename">Base filename for semantic naming (e.g., source PDF stem)</param>
    /// <returns>Modified HTML content with file references instead of base64</returns>
    Task<string> ExtractAndReplaceImagesAsync(string htmlContent, string projectImagesPath, string baseFilename);
}

public class Base64ImageExtractor : IBase64ImageExtractor
{
    private readonly ILogger<Base64ImageExtractor> _logger;

    // Regex pattern to match base64 image src attributes
    private static readonly Regex Base64ImagePattern = new(
        @"<img\s+([^>]*\s+)?src=""data:image/(png|jpeg|jpg|gif|webp);base64,([A-Za-z0-9+/=]+)""([^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    // Pattern to detect encoding from XML declaration or meta tag
    private static readonly Regex EncodingPattern = new(
        @"encoding=[""']?([A-Za-z0-9\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public Base64ImageExtractor(ILogger<Base64ImageExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractAndReplaceImagesAsync(
        string htmlContent,
        string projectImagesPath,
        string baseFilename)
    {
        try
        {
            // Ensure output directory exists
            Directory.CreateDirectory(projectImagesPath);

            // Detect original encoding
            var encoding = DetectEncoding(htmlContent);
            _logger.LogDebug("Detected encoding: {Encoding}", encoding.WebName);

            // Find all base64 images
            var matches = Base64ImagePattern.Matches(htmlContent);
            var imageCount = matches.Count;

            _logger.LogInformation("Found {ImageCount} base64-embedded images in HTML content", imageCount);

            if (imageCount == 0)
            {
                return htmlContent;
            }

            // Build header context map for semantic naming
            var headerContext = BuildHeaderContextMap(htmlContent);

            // Process each image
            var modifiedContent = htmlContent;
            var processedCount = 0;
            var failedCount = 0;

            // Process matches in reverse order to maintain string positions
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var match = matches[i];
                try
                {
                    var imageFormat = match.Groups[2].Value; // png, jpeg, etc.
                    var base64Data = match.Groups[3].Value;
                    var fullMatch = match.Value;

                    // Find semantic filename based on closest header
                    var semanticName = FindSemanticFilename(
                        htmlContent,
                        match.Index,
                        headerContext,
                        i + 1); // Use 1-based index for counter

                    var filename = $"{semanticName}.{imageFormat}";
                    var filePath = Path.Combine(projectImagesPath, filename);

                    // Extract and save image
                    var imageBytes = Convert.FromBase64String(base64Data);
                    await File.WriteAllBytesAsync(filePath, imageBytes);

                    _logger.LogInformation(
                        "Extracted image {ImageNumber}/{Total}: {Filename} ({Size} bytes)",
                        i + 1, imageCount, filename, imageBytes.Length);

                    // Replace base64 src with file reference
                    var beforeAttrs = match.Groups[1].Value;
                    var afterAttrs = match.Groups[4].Value;
                    var replacement = $"<img {beforeAttrs}src=\"{filename}\"{afterAttrs}>";

                    modifiedContent = modifiedContent.Substring(0, match.Index)
                        + replacement
                        + modifiedContent.Substring(match.Index + match.Length);

                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to extract image {ImageNumber}/{Total} at position {Position}",
                        i + 1, imageCount, match.Index);
                    failedCount++;
                }
            }

            _logger.LogInformation(
                "Image extraction complete: {Processed} processed, {Failed} failed out of {Total} total",
                processedCount, failedCount, imageCount);

            return modifiedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image extraction process");
            // Return original content on catastrophic failure
            return htmlContent;
        }
    }

    /// <summary>
    /// Builds a map of header positions to header text for semantic naming.
    /// Tracks the deepest (most specific) header at each position.
    /// </summary>
    private Dictionary<int, HeaderInfo> BuildHeaderContextMap(string htmlContent)
    {
        var headerMap = new Dictionary<int, HeaderInfo>();

        // Pattern to match headers (h1-h6)
        var headerPattern = new Regex(
            @"<(h[1-6])(?:\s+[^>]*)?>(.+?)</\1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline
        );

        var matches = headerPattern.Matches(htmlContent);
        HeaderInfo? currentHeader = null;

        foreach (Match match in matches)
        {
            var level = int.Parse(match.Groups[1].Value.Substring(1)); // Extract number from h1-h6
            var text = StripHtmlTags(match.Groups[2].Value).Trim();
            var position = match.Index + match.Length; // Position after the closing tag

            var headerInfo = new HeaderInfo
            {
                Level = level,
                Text = text,
                Position = position
            };

            // Track as current header if it's deeper or first header
            if (currentHeader == null || level >= currentHeader.Level)
            {
                currentHeader = headerInfo;
            }
            else if (level < currentHeader.Level)
            {
                // Moving to shallower level, update current
                currentHeader = headerInfo;
            }

            // Store this header context for content after it
            headerMap[position] = currentHeader;
        }

        _logger.LogDebug("Built header context map with {Count} headers", matches.Count);
        return headerMap;
    }

    /// <summary>
    /// Finds the semantic filename for an image based on closest header.
    /// </summary>
    private string FindSemanticFilename(
        string htmlContent,
        int imagePosition,
        Dictionary<int, HeaderInfo> headerContext,
        int fallbackCounter)
    {
        // Find the closest header before this image position
        HeaderInfo? closestHeader = null;
        int closestDistance = int.MaxValue;

        foreach (var kvp in headerContext)
        {
            var headerEndPosition = kvp.Key;
            if (headerEndPosition < imagePosition)
            {
                var distance = imagePosition - headerEndPosition;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHeader = kvp.Value;
                }
            }
        }

        string semanticName;
        if (closestHeader != null)
        {
            semanticName = FilenameUtils.NormalizeFileName(closestHeader.Text);

            // Add counter suffix to avoid duplicates
            semanticName = $"{semanticName}-{fallbackCounter}";

            _logger.LogDebug(
                "Image at position {Position} uses header: '{Header}' (level h{Level}) -> filename: {Filename}",
                imagePosition, closestHeader.Text, closestHeader.Level, semanticName);
        }
        else
        {
            // No header found, use generic name
            semanticName = $"image-{fallbackCounter}";
            _logger.LogDebug(
                "Image at position {Position} has no preceding header, using: {Filename}",
                imagePosition, semanticName);
        }

        return semanticName;
    }

    /// <summary>
    /// Strips HTML tags from text content.
    /// </summary>
    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Simple regex to remove tags - good enough for header text
        return Regex.Replace(html, @"<[^>]+>", string.Empty);
    }

    /// <summary>
    /// Detects encoding from HTML content.
    /// </summary>
    private Encoding DetectEncoding(string htmlContent)
    {
        // Look for encoding declaration
        var match = EncodingPattern.Match(htmlContent);
        if (match.Success)
        {
            try
            {
                var encodingName = match.Groups[1].Value;
                return Encoding.GetEncoding(encodingName);
            }
            catch
            {
                // Fall through to default
            }
        }

        // Default to UTF-8
        return Encoding.UTF8;
    }

    /// <summary>
    /// Helper class to track header information.
    /// </summary>
    private class HeaderInfo
    {
        public int Level { get; set; } // 1-6 for h1-h6
        public string Text { get; set; } = string.Empty;
        public int Position { get; set; } // Position in HTML content
    }
}

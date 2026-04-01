using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PdfConversion.Services;

public class WordPluginPreprocessorService : IWordPluginPreprocessorService
{
    private readonly ILogger<WordPluginPreprocessorService> _logger;
    private const string DebugDir = "/app/data/_work/word-plugin-debug";

    public WordPluginPreprocessorService(ILogger<WordPluginPreprocessorService> logger)
    {
        _logger = logger;
    }

    public async Task<PreprocessorResult> PreprocessAsync(
        string inputPath,
        string outputPath,
        string imagesOutputPath,
        bool writeDebugFiles = true,
        IProgress<PreprocessorProgress>? progress = null)
    {
        var stepLogs = new List<PreprocessorStepLog>();
        const int totalSteps = 4;

        try
        {
            if (writeDebugFiles)
                Directory.CreateDirectory(DebugDir);

            Directory.CreateDirectory(imagesOutputPath);

            var html = await File.ReadAllTextAsync(inputPath);
            if (writeDebugFiles)
                await File.WriteAllTextAsync(Path.Combine(DebugDir, "step-0-original.html"), html);

            // Step 1: Extract base64 images
            var sw = Stopwatch.StartNew();
            ReportProgress(progress, 1, totalSteps, "ExtractBase64Images", "running");
            var (step1Result, imageCount) = ExtractBase64Images(html, imagesOutputPath);
            html = step1Result;
            var summary1 = $"Extracted {imageCount} base64 images to {imagesOutputPath}";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("ExtractBase64Images", sw.Elapsed, summary1));
            _logger.LogInformation("Step 1: {Summary} ({Elapsed}ms)", summary1, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "step-1-images-extracted.html"), html);
            ReportProgress(progress, 1, totalSteps, "ExtractBase64Images", "completed");

            // Step 2: Wrap in XHTML
            sw.Restart();
            ReportProgress(progress, 2, totalSteps, "WrapInXhtml", "running");
            html = WrapInXhtml(html);
            var summary2 = $"Wrapped fragment in XHTML document structure";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("WrapInXhtml", sw.Elapsed, summary2));
            _logger.LogInformation("Step 2: {Summary} ({Elapsed}ms)", summary2, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "step-2-wrapped.xhtml"), html);
            ReportProgress(progress, 2, totalSteps, "WrapInXhtml", "completed");

            // Step 3: Fix self-closing tags
            sw.Restart();
            ReportProgress(progress, 3, totalSteps, "FixSelfClosingTags", "running");
            html = FixSelfClosingTags(html);
            var summary3 = $"Fixed self-closing tags (br, hr, img)";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("FixSelfClosingTags", sw.Elapsed, summary3));
            _logger.LogInformation("Step 3: {Summary} ({Elapsed}ms)", summary3, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "step-3-self-closing.xhtml"), html);
            ReportProgress(progress, 3, totalSteps, "FixSelfClosingTags", "completed");

            // Step 4: Replace HTML entities
            sw.Restart();
            ReportProgress(progress, 4, totalSteps, "ReplaceHtmlEntities", "running");
            html = ReplaceHtmlEntities(html);
            var summary4 = $"Replaced HTML named entities with XML-safe numeric equivalents";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("ReplaceHtmlEntities", sw.Elapsed, summary4));
            _logger.LogInformation("Step 4: {Summary} ({Elapsed}ms)", summary4, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "step-4-entities.xhtml"), html);
            ReportProgress(progress, 4, totalSteps, "ReplaceHtmlEntities", "completed");

            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null) Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(outputPath, html);

            return new PreprocessorResult(true, outputPath, null, stepLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Word plugin preprocessing failed");
            return new PreprocessorResult(false, null, ex.Message, stepLogs);
        }
    }

    private static void ReportProgress(IProgress<PreprocessorProgress>? progress, int step, int total, string name, string status)
    {
        progress?.Report(new PreprocessorProgress(step, total, name, status));
    }

    private (string result, int imageCount) ExtractBase64Images(string html, string imagesOutputPath)
    {
        var imageCounter = 0;
        var result = Regex.Replace(html, @"(<img\s[^>]*?)src\s*=\s*""data:image/([^;]+);base64,([^""]+)""", match =>
        {
            imageCounter++;
            var prefix = match.Groups[1].Value;
            var imageType = match.Groups[2].Value.ToLowerInvariant();
            var base64Data = match.Groups[3].Value;

            var extension = imageType switch
            {
                "jpeg" => "jpg",
                _ => imageType
            };

            var filename = $"image{imageCounter:D3}.{extension}";
            var filePath = Path.Combine(imagesOutputPath, filename);

            try
            {
                var bytes = Convert.FromBase64String(base64Data);
                File.WriteAllBytes(filePath, bytes);
            }
            catch (FormatException ex)
            {
                // Log but don't fail the whole process for one bad image
                // The src will still be replaced with the filename
                _logger.LogWarning("Failed to decode base64 image {Filename}: {Message}", filename, ex.Message);
            }

            return $"{prefix}src=\"{filename}\"";
        }, RegexOptions.Singleline);

        return (result, imageCounter);
    }

    private static string WrapInXhtml(string fragment)
    {
        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <html>
            <head><meta charset="UTF-8"/><title>Word Plugin Export</title></head>
            <body>
            {fragment}
            </body>
            </html>
            """;
    }

    private static string FixSelfClosingTags(string html)
    {
        var selfClosingTags = new[] { "br", "hr", "img" };
        foreach (var tag in selfClosingTags)
        {
            // Match tags not already self-closing (negative lookbehind for / before >)
            html = Regex.Replace(html, $@"<{tag}(\s[^>]*)?(?<!/)>(?!</{tag}>)", $"<{tag}$1/>", RegexOptions.IgnoreCase);
        }
        return html;
    }

    private static string ReplaceHtmlEntities(string html)
    {
        html = html.Replace("&nbsp;", "&#160;");
        html = html.Replace("&ndash;", "&#8211;");
        html = html.Replace("&mdash;", "&#8212;");
        html = html.Replace("&lsquo;", "&#8216;");
        html = html.Replace("&rsquo;", "&#8217;");
        html = html.Replace("&ldquo;", "&#8220;");
        html = html.Replace("&rdquo;", "&#8221;");
        html = html.Replace("&bull;", "&#8226;");
        html = html.Replace("&hellip;", "&#8230;");
        html = html.Replace("&trade;", "&#8482;");
        html = html.Replace("&copy;", "&#169;");
        html = html.Replace("&reg;", "&#174;");
        html = html.Replace("&euro;", "&#8364;");
        html = html.Replace("&pound;", "&#163;");
        return html;
    }

}

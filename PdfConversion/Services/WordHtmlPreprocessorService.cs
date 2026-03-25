using HtmlAgilityPack;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PdfConversion.Services;

public class WordHtmlPreprocessorService : IWordHtmlPreprocessorService
{
    private readonly ILogger<WordHtmlPreprocessorService> _logger;
    private const string DebugDir = "/app/data/_work/word-html-debug";

    public WordHtmlPreprocessorService(ILogger<WordHtmlPreprocessorService> logger)
    {
        _logger = logger;
    }

    public async Task<PreprocessorResult> PreprocessAsync(
        string inputPath,
        string outputPath,
        bool writeDebugFiles = true,
        IProgress<PreprocessorProgress>? progress = null)
    {
        var stepLogs = new List<PreprocessorStepLog>();
        const int totalSteps = 7;

        try
        {
            if (writeDebugFiles)
                Directory.CreateDirectory(DebugDir);

            var html = await File.ReadAllTextAsync(inputPath);
            if (writeDebugFiles)
                await File.WriteAllTextAsync(Path.Combine(DebugDir, "0-original.html"), html);

            // Step 1: Remove conditional comments
            var sw = Stopwatch.StartNew();
            ReportProgress(progress, 1, totalSteps, "RemoveConditionalComments", "running");
            html = RemoveConditionalComments(html);
            var summary1 = $"Cleaned HTML: {html.Split('\n').Length} lines remaining";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("RemoveConditionalComments", sw.Elapsed, summary1));
            _logger.LogInformation("Step 1: {Summary} ({Elapsed}ms)", summary1, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "1-no-conditionals.html"), html);
            ReportProgress(progress, 1, totalSteps, "RemoveConditionalComments", "completed");

            // Step 2: Parse to DOM
            sw.Restart();
            ReportProgress(progress, 2, totalSteps, "ParseToDOM", "running");
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var summary2 = $"Parsed {html.Split('\n').Length} lines";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("ParseToDOM", sw.Elapsed, summary2));
            _logger.LogInformation("Step 2: {Summary} ({Elapsed}ms)", summary2, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "2-parsed.html"), SerializeDoc(doc));
            ReportProgress(progress, 2, totalSteps, "ParseToDOM", "completed");

            // Step 3: Strip Office namespaces
            sw.Restart();
            ReportProgress(progress, 3, totalSteps, "StripOfficeNamespaces", "running");
            var summary3 = StripOfficeNamespaces(doc);
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("StripOfficeNamespaces", sw.Elapsed, summary3));
            _logger.LogInformation("Step 3: {Summary} ({Elapsed}ms)", summary3, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "3-no-namespaces.html"), SerializeDoc(doc));
            ReportProgress(progress, 3, totalSteps, "StripOfficeNamespaces", "completed");

            // Step 4: Remove hidden elements + style blocks
            sw.Restart();
            ReportProgress(progress, 4, totalSteps, "ResolveAndRemoveHiddenElements", "running");
            var summary4 = ResolveAndRemoveHiddenElements(doc);
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("ResolveAndRemoveHiddenElements", sw.Elapsed, summary4));
            _logger.LogInformation("Step 4: {Summary} ({Elapsed}ms)", summary4, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "4-no-hidden.html"), SerializeDoc(doc));
            ReportProgress(progress, 4, totalSteps, "ResolveAndRemoveHiddenElements", "completed");

            // Step 5: Clean MSO styles
            sw.Restart();
            ReportProgress(progress, 5, totalSteps, "CleanMsoStyles", "running");
            var summary5 = CleanMsoStyles(doc);
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("CleanMsoStyles", sw.Elapsed, summary5));
            _logger.LogInformation("Step 5: {Summary} ({Elapsed}ms)", summary5, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "5-no-mso.html"), SerializeDoc(doc));
            ReportProgress(progress, 5, totalSteps, "CleanMsoStyles", "completed");

            // Step 6: Rewrite image paths
            sw.Restart();
            ReportProgress(progress, 6, totalSteps, "RewriteImagePaths", "running");
            var summary6 = RewriteImagePaths(doc);
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("RewriteImagePaths", sw.Elapsed, summary6));
            _logger.LogInformation("Step 6: {Summary} ({Elapsed}ms)", summary6, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "6-images-rewritten.html"), SerializeDoc(doc));
            ReportProgress(progress, 6, totalSteps, "RewriteImagePaths", "completed");

            // Step 7: Serialize to XHTML
            sw.Restart();
            ReportProgress(progress, 7, totalSteps, "SerializeToXhtml", "running");
            var xhtml = SerializeToXhtml(doc);
            var summary7 = $"Output: {xhtml.Length} characters";
            sw.Stop();
            stepLogs.Add(new PreprocessorStepLog("SerializeToXhtml", sw.Elapsed, summary7));
            _logger.LogInformation("Step 7: {Summary} ({Elapsed}ms)", summary7, sw.ElapsedMilliseconds);
            if (writeDebugFiles) await File.WriteAllTextAsync(Path.Combine(DebugDir, "7-final.xhtml"), xhtml);
            ReportProgress(progress, 7, totalSteps, "SerializeToXhtml", "completed");

            var outputDir = Path.GetDirectoryName(outputPath);
            if (outputDir != null) Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(outputPath, xhtml);

            return new PreprocessorResult(true, outputPath, null, stepLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preprocessing failed");
            return new PreprocessorResult(false, null, ex.Message, stepLogs);
        }
    }

    private static void ReportProgress(IProgress<PreprocessorProgress>? progress, int step, int total, string name, string status)
    {
        progress?.Report(new PreprocessorProgress(step, total, name, status));
    }

    private static string SerializeDoc(HtmlDocument doc)
    {
        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    private string RemoveConditionalComments(string html)
    {
        // Remove <!--[if ...]>...<![endif]--> comment blocks (VML, Office XML, etc.)
        html = Regex.Replace(html, @"<!--\[if[^\]]*\]>.*?<!\[endif\]-->", "", RegexOptions.Singleline);

        // Remove <![if ...]> and <![endif]> wrapper tags but KEEP content between them.
        // These wrap fallback content like <img> tags that we need to preserve.
        html = Regex.Replace(html, @"<!\[if[^\]]*\]>", "");
        html = Regex.Replace(html, @"<!\[endif\]>", "");

        return html;
    }

    private string StripOfficeNamespaces(HtmlDocument doc)
    {
        var removedElements = 0;
        var removedNamespaces = 0;

        var namespacePrefixes = new[] { "o:", "v:", "w:", "m:", "dt:" };
        var nodesToRemove = doc.DocumentNode.SelectNodes("//*")
            ?.Where(n => namespacePrefixes.Any(p => n.Name.StartsWith(p)))
            .ToList() ?? new List<HtmlNode>();

        foreach (var node in nodesToRemove)
        {
            node.Remove();
            removedElements++;
        }

        var htmlNode = doc.DocumentNode.SelectSingleNode("//html");
        if (htmlNode != null)
        {
            // Remove all namespace attributes: both prefixed (xmlns:v, xmlns:o, etc.)
            // and the default namespace (xmlns="http://www.w3.org/TR/REC-html40")
            var nsAttrs = htmlNode.Attributes
                .Where(a => a.Name == "xmlns" || a.Name.StartsWith("xmlns:"))
                .Select(a => a.Name)
                .ToList();
            foreach (var attr in nsAttrs)
            {
                htmlNode.Attributes.Remove(attr);
                removedNamespaces++;
            }
        }

        return $"Removed {removedElements} namespace-prefixed elements, {removedNamespaces} namespace declarations";
    }

    private string ResolveAndRemoveHiddenElements(HtmlDocument doc)
    {
        var removedCount = 0;

        var hiddenClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var styleNodes = doc.DocumentNode.SelectNodes("//style") ?? Enumerable.Empty<HtmlNode>();
        foreach (var styleNode in styleNodes.ToList())
        {
            var css = styleNode.InnerText;
            var matches = Regex.Matches(css, @"(?:p|li|div|span)?\.(\w+)[^{]*\{[^}]*display\s*:\s*none[^}]*\}", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                hiddenClasses.Add(match.Groups[1].Value);
            }
        }

        var allElements = doc.DocumentNode.SelectNodes("//*[@style]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var element in allElements.ToList())
        {
            var style = element.GetAttributeValue("style", "");
            if (Regex.IsMatch(style, @"display\s*:\s*none", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(style, @"mso-hide\s*:\s*(all|screen)", RegexOptions.IgnoreCase))
            {
                element.Remove();
                removedCount++;
            }
        }

        if (hiddenClasses.Count > 0)
        {
            var classElements = doc.DocumentNode.SelectNodes("//*[@class]") ?? Enumerable.Empty<HtmlNode>();
            foreach (var element in classElements.ToList())
            {
                var classes = element.GetAttributeValue("class", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (classes.Any(c => hiddenClasses.Contains(c)))
                {
                    element.Remove();
                    removedCount++;
                }
            }
        }

        var remainingStyles = doc.DocumentNode.SelectNodes("//style")?.ToList() ?? new List<HtmlNode>();
        foreach (var styleNode in remainingStyles)
        {
            styleNode.Remove();
        }

        return $"Removed {removedCount} hidden elements, {hiddenClasses.Count} hidden CSS classes found, {remainingStyles.Count} style blocks removed";
    }

    private string CleanMsoStyles(HtmlDocument doc)
    {
        var cleanedCount = 0;
        var removedAttrs = 0;

        var styledElements = doc.DocumentNode.SelectNodes("//*[@style]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var element in styledElements.ToList())
        {
            var style = element.GetAttributeValue("style", "");
            var cleaned = Regex.Replace(style, @"mso-[^;""']+;?\s*", "", RegexOptions.IgnoreCase);
            cleaned = cleaned.Trim().TrimEnd(';').Trim();

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                element.Attributes.Remove("style");
                removedAttrs++;
            }
            else if (cleaned != style)
            {
                element.SetAttributeValue("style", cleaned);
                cleanedCount++;
            }
        }

        return $"Cleaned {cleanedCount} style attributes, removed {removedAttrs} empty style attributes";
    }

    private string RewriteImagePaths(HtmlDocument doc)
    {
        var rewrittenCount = 0;

        var imgElements = doc.DocumentNode.SelectNodes("//img[@src]") ?? Enumerable.Empty<HtmlNode>();
        foreach (var img in imgElements)
        {
            var src = img.GetAttributeValue("src", "");
            if (string.IsNullOrEmpty(src)) continue;

            var decoded = Uri.UnescapeDataString(src);
            var filename = Path.GetFileName(decoded);
            if (!string.IsNullOrEmpty(filename))
            {
                img.SetAttributeValue("src", $"from-conversion/{filename}");
                rewrittenCount++;
            }
        }

        return $"Rewrote {rewrittenCount} image paths";
    }

    private string SerializeToXhtml(HtmlDocument doc)
    {
        using var sw = new StringWriter();
        doc.Save(sw);
        var html = sw.ToString();

        // Replace HTML named entities with numeric equivalents (XML only supports &amp; &lt; &gt; &quot; &apos;)
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

        if (!html.StartsWith("<?xml"))
        {
            html = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + html;
        }

        var selfClosingTags = new[] { "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed", "source", "track", "wbr" };
        foreach (var tag in selfClosingTags)
        {
            html = Regex.Replace(html, $@"<{tag}(\s[^>]*)?>(?!</{tag}>)", $"<{tag}$1/>", RegexOptions.IgnoreCase);
        }

        return html;
    }
}


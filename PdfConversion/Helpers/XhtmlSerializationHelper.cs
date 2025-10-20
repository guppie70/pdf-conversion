using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PdfConversion.Helpers;

/// <summary>
/// Utility class for serializing XML documents as proper XHTML
/// with correct void element handling.
/// </summary>
public static class XhtmlSerializationHelper
{
    /// <summary>
    /// HTML void elements that should be self-closing in XHTML.
    /// These elements never have content and are always empty.
    /// </summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "source", "track", "wbr"
    };

    /// <summary>
    /// Serializes an XDocument to XHTML format with proper void element handling.
    /// </summary>
    /// <param name="document">The XML document to serialize</param>
    /// <param name="omitXmlDeclaration">Whether to omit the XML declaration (default: false)</param>
    /// <returns>XHTML string with proper element closing</returns>
    public static string SerializeXhtmlDocument(XDocument document, bool omitXmlDeclaration = false)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = omitXmlDeclaration,
            Encoding = new UTF8Encoding(false), // UTF-8 without BOM
            ConformanceLevel = ConformanceLevel.Document
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);

        // Write the document
        document.Save(xmlWriter);
        xmlWriter.Flush();

        var xml = stringWriter.ToString();

        // Post-process to fix non-void empty elements
        return FixNonVoidElements(xml);
    }

    /// <summary>
    /// Serializes an XElement to XHTML format with proper void element handling.
    /// </summary>
    /// <param name="element">The XML element to serialize</param>
    /// <returns>XHTML string with proper element closing</returns>
    public static string SerializeXhtmlElement(XElement element)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = true,
            Encoding = new UTF8Encoding(false),
            ConformanceLevel = ConformanceLevel.Fragment
        };

        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, settings);

        element.Save(xmlWriter);
        xmlWriter.Flush();

        var xml = stringWriter.ToString();

        return FixNonVoidElements(xml);
    }

    /// <summary>
    /// Fixes non-void empty elements to use explicit closing tags instead of self-closing syntax.
    /// Only HTML void elements should remain self-closing.
    /// </summary>
    /// <param name="xml">The XML string to fix</param>
    /// <returns>Fixed XHTML string</returns>
    private static string FixNonVoidElements(string xml)
    {
        var sb = new StringBuilder(xml.Length + 100);
        var i = 0;

        while (i < xml.Length)
        {
            // Find next self-closing tag
            var tagStart = xml.IndexOf('<', i);
            if (tagStart == -1)
            {
                // No more tags, append rest
                sb.Append(xml.AsSpan(i));
                break;
            }

            // Append everything before the tag
            sb.Append(xml.AsSpan(i, tagStart - i));

            // Check if it's a self-closing tag
            var tagEnd = xml.IndexOf('>', tagStart);
            if (tagEnd == -1)
            {
                // Malformed, append rest and break
                sb.Append(xml.AsSpan(tagStart));
                break;
            }

            // Check if tag ends with "/>"
            if (tagEnd > tagStart + 1 && xml[tagEnd - 1] == '/')
            {
                // Extract tag name
                var tagContent = xml.Substring(tagStart + 1, tagEnd - tagStart - 2).TrimEnd();
                var spaceIndex = tagContent.IndexOfAny(new[] { ' ', '\t', '\n', '\r' });
                var tagName = spaceIndex > 0 ? tagContent.Substring(0, spaceIndex) : tagContent;

                // Skip processing instructions and comments
                if (tagName.StartsWith("?") || tagName.StartsWith("!"))
                {
                    sb.Append(xml.AsSpan(tagStart, tagEnd - tagStart + 1));
                    i = tagEnd + 1;
                    continue;
                }

                // Check if this is a void element
                if (VoidElements.Contains(tagName))
                {
                    // Keep self-closing for void elements
                    sb.Append(xml.AsSpan(tagStart, tagEnd - tagStart + 1));
                }
                else
                {
                    // Convert to explicit closing tag for non-void elements
                    sb.Append('<');
                    sb.Append(tagContent);
                    sb.Append("></");
                    sb.Append(tagName);
                    sb.Append('>');
                }

                i = tagEnd + 1;
            }
            else
            {
                // Not self-closing, append as-is
                sb.Append(xml.AsSpan(tagStart, tagEnd - tagStart + 1));
                i = tagEnd + 1;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates XmlWriterSettings configured for XHTML output.
    /// </summary>
    /// <param name="omitXmlDeclaration">Whether to omit the XML declaration</param>
    /// <param name="conformanceLevel">The conformance level (Document or Fragment)</param>
    /// <returns>Configured XmlWriterSettings</returns>
    public static XmlWriterSettings CreateXhtmlSettings(
        bool omitXmlDeclaration = false,
        ConformanceLevel conformanceLevel = ConformanceLevel.Document)
    {
        return new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = omitXmlDeclaration,
            Encoding = new UTF8Encoding(false),
            ConformanceLevel = conformanceLevel
        };
    }
}

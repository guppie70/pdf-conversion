using HtmlAgilityPack;
using System.Text;
using System.Xml;

namespace PdfConversion.Services;

/// <summary>
/// Service for converting HTML to XHTML format suitable for XSLT transformation.
/// Uses HtmlAgilityPack to parse HTML and convert to valid XHTML.
/// </summary>
public class HtmlToXhtmlConversionService : IHtmlToXhtmlConversionService
{
    private readonly ILogger<HtmlToXhtmlConversionService> _logger;

    public HtmlToXhtmlConversionService(ILogger<HtmlToXhtmlConversionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert HTML string to XHTML with standardized wrapper.
    /// </summary>
    /// <param name="htmlContent">Raw HTML content from docling</param>
    /// <returns>XHTML wrapped content with proper XML declaration and structure</returns>
    public async Task<string> ConvertHtmlToXhtmlAsync(string htmlContent)
    {
        try
        {
            _logger.LogInformation("Starting HTML to XHTML conversion");

            // Load HTML using HtmlAgilityPack
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Convert HTML to XHTML
            var xhtmlContent = await ConvertToXhtmlAsync(htmlDoc);

            _logger.LogInformation("HTML to XHTML conversion completed successfully");
            return xhtmlContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert HTML to XHTML");
            throw;
        }
    }

    private async Task<string> ConvertToXhtmlAsync(HtmlDocument htmlDoc)
    {
        return await Task.Run(() =>
        {
            // Create XML document
            var xmlDoc = new XmlDocument();

            // Add XML declaration
            var xmlDeclaration = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
            xmlDoc.AppendChild(xmlDeclaration);

            // Create root html element
            var htmlElement = xmlDoc.CreateElement("html");
            xmlDoc.AppendChild(htmlElement);

            // Create head element
            var headElement = xmlDoc.CreateElement("head");
            htmlElement.AppendChild(headElement);

            // Add title to head
            var titleElement = xmlDoc.CreateElement("title");
            titleElement.InnerText = "Docling Converted Document";
            headElement.AppendChild(titleElement);

            // Create body element
            var bodyElement = xmlDoc.CreateElement("body");
            htmlElement.AppendChild(bodyElement);

            // Create div wrapper with class 'page'
            var divElement = xmlDoc.CreateElement("div");
            var classAttr = xmlDoc.CreateAttribute("class");
            classAttr.Value = "page";
            divElement.Attributes.Append(classAttr);
            bodyElement.AppendChild(divElement);

            // Convert HtmlAgilityPack nodes to XML nodes
            ConvertHtmlNodeToXmlNode(htmlDoc.DocumentNode, divElement, xmlDoc);

            // Convert to string with proper formatting
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                Encoding = Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                xmlDoc.Save(writer);
            }

            return sb.ToString();
        });
    }

    private void ConvertHtmlNodeToXmlNode(HtmlNode htmlNode, XmlElement parentXmlElement, XmlDocument xmlDoc)
    {
        foreach (var childNode in htmlNode.ChildNodes)
        {
            switch (childNode.NodeType)
            {
                case HtmlNodeType.Element:
                    // Create XML element
                    var xmlElement = xmlDoc.CreateElement(childNode.Name.ToLowerInvariant());

                    // Copy attributes
                    foreach (var attr in childNode.Attributes)
                    {
                        var xmlAttr = xmlDoc.CreateAttribute(attr.Name.ToLowerInvariant());
                        xmlAttr.Value = attr.Value;
                        xmlElement.Attributes.Append(xmlAttr);
                    }

                    // Handle self-closing tags
                    if (IsSelfClosingTag(childNode.Name))
                    {
                        // Self-closing tags in XHTML
                        parentXmlElement.AppendChild(xmlElement);
                    }
                    else
                    {
                        // Recursively process child nodes
                        ConvertHtmlNodeToXmlNode(childNode, xmlElement, xmlDoc);
                        parentXmlElement.AppendChild(xmlElement);
                    }
                    break;

                case HtmlNodeType.Text:
                    var text = HtmlEntity.DeEntitize(childNode.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var textNode = xmlDoc.CreateTextNode(text);
                        parentXmlElement.AppendChild(textNode);
                    }
                    break;

                case HtmlNodeType.Comment:
                    // Optionally preserve comments
                    var comment = xmlDoc.CreateComment(childNode.InnerText);
                    parentXmlElement.AppendChild(comment);
                    break;
            }
        }
    }

    private bool IsSelfClosingTag(string tagName)
    {
        var selfClosingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "br", "hr", "img", "input", "meta", "link", "area", "base", "col", "embed", "param", "source", "track", "wbr"
        };

        return selfClosingTags.Contains(tagName);
    }
}

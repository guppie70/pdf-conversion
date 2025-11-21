namespace PdfConversion.Services;

/// <summary>
/// Service for converting HTML to XHTML format suitable for XSLT transformation.
/// </summary>
public interface IHtmlToXhtmlConversionService
{
    /// <summary>
    /// Convert HTML string to XHTML with standardized wrapper.
    /// </summary>
    /// <param name="htmlContent">Raw HTML content from docling</param>
    /// <returns>XHTML wrapped content with proper XML declaration and structure</returns>
    Task<string> ConvertHtmlToXhtmlAsync(string htmlContent);
}

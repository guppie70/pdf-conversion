using System.Text.RegularExpressions;

namespace PdfConversion.Utils;

/// <summary>
/// Utility methods for filename operations.
/// </summary>
public static class FilenameUtils
{
    /// <summary>
    /// Normalizes a string for use as a filename.
    /// Removes/replaces invalid characters, converts to lowercase, replaces spaces with hyphens.
    /// </summary>
    /// <param name="input">The input string to normalize</param>
    /// <returns>A normalized filename-safe string</returns>
    public static string NormalizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unnamed";

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var normalized = new string(input
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Replace spaces and multiple hyphens with single hyphen
        normalized = Regex.Replace(normalized, @"\s+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");

        // Convert to lowercase
        normalized = normalized.ToLower();

        // Trim hyphens from ends
        normalized = normalized.Trim('-');

        // Limit length to reasonable filename size
        if (normalized.Length > 50)
            normalized = normalized.Substring(0, 50).TrimEnd('-');

        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized;
    }

    /// <summary>
    /// Converts a customer folder name to a display-friendly name.
    /// Examples: "optiver" -> "Optiver", "antea-group" -> "Antea Group", "test" -> "Test"
    /// </summary>
    /// <param name="customerFolder">The customer folder name (e.g., "optiver", "antea-group")</param>
    /// <returns>Display-friendly customer name</returns>
    public static string FormatCustomerDisplayName(string customerFolder)
    {
        if (string.IsNullOrWhiteSpace(customerFolder))
            return "Unknown";

        // Replace hyphens with spaces
        var displayName = customerFolder.Replace("-", " ");

        // Title case each word
        var words = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleCasedWords = words.Select(word =>
            char.ToUpper(word[0]) + word.Substring(1).ToLower());

        return string.Join(" ", titleCasedWords);
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace PdfConversion.Utils;

/// <summary>
/// Utility methods for filename operations.
/// </summary>
public static class FilenameUtils
{
    /// <summary>
    /// Normalizes text to ASCII characters only, replacing special Unicode characters
    /// with their ASCII equivalents.
    /// </summary>
    /// <param name="input">The input text to normalize</param>
    /// <returns>ASCII-only text</returns>
    public static string NormalizeToAscii(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder();

        foreach (char c in input)
        {
            // Convert common Unicode characters to ASCII equivalents
            var asciiChar = c switch
            {
                // Apostrophes and quotes
                '\u2018' or '\u2019' or '\u201A' or '\u201B' => '\'',  // Curly apostrophes
                '\u201C' or '\u201D' or '\u201E' or '\u201F' => '"',   // Curly quotes
                '\u00B4' or '\u0060' => '\'',                          // Acute accent, grave accent
                '\u2032' => '\'',                                       // Prime
                '\u2033' => '"',                                        // Double prime

                // Dashes and hyphens
                '\u2013' or '\u2014' or '\u2015' => '-',               // En dash, em dash, horizontal bar
                '\u2010' or '\u2011' or '\u2012' => '-',               // Hyphen, non-breaking hyphen, figure dash

                // Spaces
                '\u00A0' or '\u2002' or '\u2003' or '\u2004' => ' ',   // Non-breaking space, en space, em space, three-per-em space
                '\u2005' or '\u2006' or '\u2007' or '\u2008' => ' ',   // Four-per-em space, six-per-em space, figure space, punctuation space
                '\u2009' or '\u200A' or '\u202F' or '\u205F' => ' ',   // Thin space, hair space, narrow no-break space, medium mathematical space

                // Other punctuation
                '\u2026' => '.',                                        // Ellipsis
                '\u00B7' => '.',                                        // Middle dot
                '\u2022' => '*',                                        // Bullet
                _ when c == '\u00A9' => 'c',                           // Copyright (simplified to single char)
                _ when c == '\u00AE' => 'r',                           // Registered (simplified to single char)
                _ when c == '\u2122' => ' ',                           // Trademark (simplified to space)

                // Keep the character if it's already ASCII (32-126)
                _ when c >= 32 && c <= 126 => c,

                // Replace other non-ASCII with space
                _ => ' '
            };

            result.Append(asciiChar);
        }

        // Clean up multiple spaces
        var normalized = Regex.Replace(result.ToString(), @"\s+", " ");
        return normalized.Trim();
    }
    /// <summary>
    /// Normalizes a string for use as a filename.
    /// Removes/replaces invalid characters, converts to lowercase, replaces spaces with hyphens.
    /// </summary>
    /// <param name="input">The input string to normalize</param>
    /// <param name="postfix">Optional postfix to append (e.g., timestamp like "20250107-143025")</param>
    /// <returns>A normalized filename-safe string</returns>
    public static string NormalizeFileName(string input, string? postfix = null)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "unnamed";

        // First convert to ASCII to handle all Unicode characters
        var normalized = NormalizeToAscii(input);

        // Remove invalid filename characters
        var invalidChars = Path.GetInvalidFileNameChars();
        normalized = new string(normalized
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Remove apostrophes and other punctuation we don't want in filenames
        // Keep: letters, numbers, spaces, hyphens
        // Remove: apostrophes, quotes, commas, periods, parentheses, brackets, ampersands, asterisks, etc.
        normalized = Regex.Replace(normalized, @"['`,:;!?()[\]{}\""*&]", "");
        normalized = normalized.Replace(".", ""); // Remove periods separately

        // Replace spaces and multiple hyphens with single hyphen
        normalized = Regex.Replace(normalized, @"\s+", "-");
        normalized = Regex.Replace(normalized, @"-+", "-");

        // Convert to lowercase
        normalized = normalized.ToLower();

        // Trim hyphens from ends
        normalized = normalized.Trim('-');

        // Limit length to 40 characters (Taxxor system limitation)
        if (normalized.Length > 40)
            normalized = normalized.Substring(0, 40).TrimEnd('-');

        // Apply postfix BEFORE returning (if provided and not just whitespace)
        if (!string.IsNullOrWhiteSpace(postfix))
        {
            normalized = $"{normalized}-{postfix}";
        }

        return string.IsNullOrWhiteSpace(normalized) ? "unnamed" : normalized;
    }

    /// <summary>
    /// Ensures the given ID is unique by appending a sequential counter if needed.
    /// Uses the provided usedIds set to track already-used IDs.
    /// </summary>
    /// <param name="baseId">The base ID (already normalized)</param>
    /// <param name="usedIds">Set of already-used IDs (will be updated with the final unique ID)</param>
    /// <returns>Unique ID (e.g., "lorem-ipsum-143025" or "lorem-ipsum-2-143025")</returns>
    public static string EnsureUniqueId(string baseId, HashSet<string> usedIds)
    {
        if (string.IsNullOrEmpty(baseId))
            throw new ArgumentException("Base ID cannot be null or empty", nameof(baseId));

        if (usedIds == null)
            throw new ArgumentNullException(nameof(usedIds));

        // If ID is already unique, use it as-is
        if (!usedIds.Contains(baseId))
        {
            usedIds.Add(baseId);
            return baseId;
        }

        // ID is duplicate - append counter
        int counter = 2;
        string uniqueId;

        do
        {
            uniqueId = $"{baseId}-{counter}";
            counter++;
        }
        while (usedIds.Contains(uniqueId));

        usedIds.Add(uniqueId);
        return uniqueId;
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

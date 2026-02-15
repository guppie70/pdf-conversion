namespace PdfConversion.Services;

public static class HeaderCapsHelper
{
    private static readonly HashSet<string> SmallWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "the", "of", "in", "on", "at", "to", "for",
        "a", "an", "by", "with", "from", "as", "but", "nor", "so", "yet"
    };

    public static bool IsAllCaps(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var alphaChars = text.Where(char.IsLetter).ToList();
        if (alphaChars.Count == 0)
            return false;

        var upperCount = alphaChars.Count(char.IsUpper);
        return (double)upperCount / alphaChars.Count > 0.8;
    }

    public static string ToSmartTitleCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return text;

        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var isFirstOrLast = i == 0 || i == words.Length - 1;

            if (isFirstOrLast || !SmallWords.Contains(word))
            {
                words[i] = CapitalizeWord(word);
            }
            else
            {
                words[i] = word.ToLowerInvariant();
            }
        }

        return string.Join(' ', words);
    }

    private static string CapitalizeWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return word;

        if (word.Length == 1)
            return word.ToUpperInvariant();

        return char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    /// <summary>
    /// Capitalizes the first alphabetic character if it is lowercase.
    /// Handles text that starts with numbers/symbols (e.g., "4.2 financial..." -> "4.2 Financial...").
    /// </summary>
    public static string CapitalizeFirstLetter(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var index = 0;
        while (index < text.Length && !char.IsLetter(text[index]))
            index++;

        if (index >= text.Length || !char.IsLower(text[index]))
            return text;

        return text[..index] + char.ToUpperInvariant(text[index]) + text[(index + 1)..];
    }

    /// <summary>
    /// Returns true if the first alphabetic character in the text is lowercase.
    /// </summary>
    public static bool StartsWithLowercase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (char.IsLetter(c))
                return char.IsLower(c);
        }

        return false;
    }
}

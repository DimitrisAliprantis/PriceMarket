using System.Globalization;
using System.Text;

namespace PriceMarket;

public static class TextNormalizer
{
    /* Turns any text into a simple searchable form: lowercase, no accents,
       final sigma replaced by a normal sigma, and punctuation replaced by spaces. */
    public static string ForSearch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Split letters from their accents (ά becomes α plus an accent mark)
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            // Skip the accent marks themselves
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;

            // Keep letters and digits, turn everything else (hyphens, brackets, %...) into a space
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        // Normal sigma and final sigma should count as the same letter
        var cleaned = sb.ToString().Replace('ς', 'σ');

        // Collapse repeated spaces so the text splits into clean words
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
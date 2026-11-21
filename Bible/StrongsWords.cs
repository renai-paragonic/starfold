using System.Text;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Bible;

// Finds lexicon entries by transliteration, so "agape" offers G26 and
// "elohim" offers H430. Both sides are folded the same way: accents and
// breathing marks dropped, and the Hebrew file's long-vowel spellings
// shortened ("ʼĕlôhîym" -> "elohim", "shâlôwm" -> "shalom", "chêçêd" -> "chesed").
public sealed class StrongsWords(IEnumerable<StrongsEntry> entries)
{
    public const int Max = 5;

    private readonly List<(StrongsEntry Entry, string Key)> index =
        [.. entries.Where(e => e.Transliteration is not null).Select(e => (e, Fold(e.Transliteration!)))];

    public int Count => index.Count;

    // Exact matches from 3 letters, then (from 4 letters) transliterations
    // that start with the word, shortest first. Short words are noisy:
    // "in", "on" and "he" are all Hebrew transliterations, and "the" starts 46.
    public List<StrongsEntry> Match(string word)
    {
        if (word.Any(char.IsDigit)) return [];
        var key = Fold(word);
        if (key.Length < 3) return [];
        return [.. index
            .Where(x => x.Key == key || key.Length >= 4 && x.Key.StartsWith(key, StringComparison.Ordinal))
            .OrderBy(x => x.Key == key ? 0 : 1)
            .ThenBy(x => x.Key.Length)
            .ThenBy(x => x.Entry.Id[0] == 'G' ? 0 : 1)
            .ThenBy(x => int.Parse(x.Entry.Id[1..]))
            .Take(Max)
            .Select(x => x.Entry)];
    }

    // Lowercase ASCII letters only. ç is samekh (s), ᵉ a reduced vowel (e).
    public static string Fold(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Replace('ç', 's').Replace('Ç', 's').Replace('ᵉ', 'e').Replace('ˢ', 's').Normalize(NormalizationForm.FormD))
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') sb.Append(char.ToLowerInvariant(c));
        return sb.Replace("iy", "i").Replace("ow", "o").Replace("uw", "u").ToString();
    }
}

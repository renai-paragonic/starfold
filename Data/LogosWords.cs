using System.Globalization;
using System.Text;

namespace starfold_mobile.Data;

// Matches Logos word anchors ("lemma.lbs.he.קום", "root.lbs.el.δουλος") to
// Strong's entries. Logos leaves out vowel points and accents, so both sides
// are compared without them, which can match more than one entry (קום is
// H6965, Hebrew, and H6966, Aramaic). The lowest number wins.
public sealed class LogosWords
{
    public sealed record Match(string? Id, List<string> Candidates, string? Problem);

    private readonly Dictionary<string, List<string>> _byKey = [];

    public LogosWords(IEnumerable<(string Id, string Lemma)> lexicon)
    {
        foreach (var (id, lemma) in lexicon)
        {
            var key = id[0] + Bare(lemma);
            if (!_byKey.TryGetValue(key, out var ids)) _byKey[key] = ids = [];
            ids.Add(id);
        }
        foreach (var ids in _byKey.Values) ids.Sort((a, b) => int.Parse(a[1..]).CompareTo(int.Parse(b[1..])));
    }

    public Match Find(string raw)
    {
        var parts = raw.Split('.', 4);
        if (parts.Length < 4 || parts[1] != "lbs") return new(null, [], $"couldn't read the word anchor {raw}.");
        var prefix = parts[2] switch { "he" or "arc" => 'H', "el" or "grc" => 'G', _ => (char?)null };
        if (prefix is null) return new(null, [], $"the word anchor {raw} isn't Hebrew or Greek.");
        if (!_byKey.TryGetValue(prefix + Bare(parts[3]), out var ids)) return new(null, [], $"no Strong's entry for {parts[3]}.");
        return new(ids[0], ids, null);
    }

    // Without vowel points, cantillation, accents or breathings, and with a
    // final sigma read as a sigma.
    public static string Bare(string word)
    {
        var sb = new StringBuilder();
        foreach (var c in word.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('ς', 'σ').Trim();
    }
}

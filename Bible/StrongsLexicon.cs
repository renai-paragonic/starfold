using System.Text.Json;
using System.Text.RegularExpressions;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Bible;

// Reads Open Scriptures' strongs data (https://github.com/openscriptures/strongs,
// CC BY-SA): greek/strongs-greek-dictionary.js and
// hebrew/strongs-hebrew-dictionary.js. Each is a comment header, then
// `var x = { "G26": {...}, ... }; module.exports = x;`. The Greek entries
// spell the transliteration "translit" and the Hebrew ones "xlit".
public static partial class StrongsLexicon
{
    public static readonly string[] FileNames = ["strongs-greek-dictionary.js", "strongs-hebrew-dictionary.js"];

    public static List<StrongsEntry> Read(string js)
    {
        var m = Start().Match(js);
        var end = js.LastIndexOf('}');
        if (!m.Success || end < m.Index) throw new FormatException("Expected `var name = { ... };`.");
        var start = m.Index + m.Length - 1;

        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(js[start..(end + 1)])!;
        var entries = new List<StrongsEntry>();
        foreach (var (key, v) in raw)
        {
            var id = StrongsParser.Normalize(key) ?? throw new FormatException($"Bad Strong's number \"{key}\".");
            entries.Add(new StrongsEntry
            {
                Id = id,
                Lemma = Field(v, "lemma") ?? throw new FormatException($"{id} has no lemma."),
                Transliteration = Field(v, "translit") ?? Field(v, "xlit"),
                Definition = Field(v, "strongs_def"),
                Derivation = Field(v, "derivation"),
                KjvDef = Field(v, "kjv_def"),
            });
        }
        return entries;
    }

    // Trimmed (definitions start with a space), and empty as null.
    private static string? Field(Dictionary<string, string> v, string name) =>
        v.TryGetValue(name, out var s) && s.Trim() is { Length: > 0 } t ? t : null;

    [GeneratedRegex(@"\bvar\s+\w+\s*=\s*\{")]
    private static partial Regex Start(); // `var name = {`
}

using starfold_mobile.Bible;

namespace starfold_mobile.Models.Ezra;

// Search: free text plus chips (user tags, scripture refs and Strong's
// numbers). A note must match every text word (in its title, body or a tag
// name) and every chip. SQLite's LIKE ignores case for ASCII letters only.
public static class NoteSearch
{
    public const string Escape = @"\"; // pass to EF.Functions.Like
    public const int MaxWords = 10;

    // Chip kinds double as the query string names: ?tag=…&ref=…&strongs=…
    public const string TagKind = "tag", RefKind = "ref", StrongsKind = "strongs";

    // A chip in the search box. Value is what's posted; Link is its page.
    public sealed record Chip(string Kind, string Value, string Label, string Link)
    {
        public string CssClass => Kind switch
        {
            RefKind => "ezra-chip ezra-chip-ref",
            StrongsKind => "ezra-chip ezra-chip-strongs",
            _ => "ezra-chip",
        };
    }

    // A flyout option. Picking it removes the last `Words` words of the text.
    public sealed record Suggestion(string Kind, string Value, string Label, int Words);

    public static List<string> Patterns(string? query) =>
        [.. Words(query)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxWords)
            .Select(w => "%" + EscapeLike(w) + "%")];

    // Suggestions for the text typed so far, checked against its trailing
    // words: scripture (the longest tail that parses, e.g. "love john 3:16"
    // -> John 3:16), then the Strong's entry for the last word (looked up by
    // the caller), then tags containing a tail of up to 3 words.
    // Only the text after the last ";" counts, so in "John 3:16; Rom 8" the
    // suggestion covers "Rom 8" and picking it leaves "John 3:16;" alone.
    public static List<Suggestion> Suggest(string? text, IEnumerable<string> tags, StrongsEntry? strongs)
    {
        var words = Words((text ?? "").Split(';')[^1]);
        var result = new List<Suggestion>();
        if (words.Length == 0) return result;
        string Tail(int k) => string.Join(" ", words[^k..]);

        for (var k = Math.Min(5, words.Length); k >= 1; k--)
        {
            var tail = Tail(k);
            if (!tail.Any(char.IsLetter)) continue;
            var parsed = ReferenceParser.Parse(tail);
            if (parsed.Errors.Count > 0 || parsed.Refs.Count == 0) continue;
            // A bare book needs 3 letters, so "is" doesn't offer Isaiah.
            if (tail.Any(char.IsDigit) || tail.Count(char.IsLetter) >= 3)
                result.AddRange(parsed.Refs.Take(5).Select(r => new Suggestion(RefKind, r.Format(), r.Format(), k)));
            break;
        }

        if (strongs is not null)
            result.Add(new(StrongsKind, strongs.Id, $"{strongs.Id} {strongs.Lemma}", 1));

        var tagMatches = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var k = 1; k <= Math.Min(3, words.Length); k++)
            foreach (var tag in tags.Where(t => t.Contains(Tail(k), StringComparison.OrdinalIgnoreCase)))
                tagMatches[tag] = k;
        result.AddRange(tagMatches
            .OrderBy(m => m.Key.StartsWith(Tail(m.Value), StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(m => m.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(m => new Suggestion(TagKind, m.Key, m.Key, m.Value)));
        return result;
    }

    private static string[] Words(string? text) =>
        (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    // "100%_done" -> "100\%\_done", so % and _ match themselves.
    private static string EscapeLike(string word) =>
        word.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

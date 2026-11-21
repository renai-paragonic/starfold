using System.Text.RegularExpressions;

namespace starfold_mobile.Bible;

// One book of the 66-book Protestant canon. Verses[i] is the verse count of
// chapter i + 1 (LSB versification; the data is in BibleBooks.Data.cs).
public sealed record BibleBook(int Number, string Name, string[] Aliases, int[] Verses)
{
    public int Chapters => Verses.Length;
    public int VersesIn(int chapter) => Verses[chapter - 1];

    // URL form, e.g. "1-john", "song-of-solomon".
    public string Slug => Name.ToLowerInvariant().Replace(' ', '-');
}

public static partial class BibleBooks
{
    public static BibleBook Get(int number) => All[number - 1];

    // Full names and aliases, normalized by Key. Add throws on a collision,
    // so a clashing alias fails loudly on first use (and in the tests). Lazy
    // because static fields in the other partial file (All) may not be set yet.
    private static readonly Lazy<Dictionary<string, BibleBook>> ByKey = new(BuildKeys);

    // Finds a book by full name, alias, or any unambiguous prefix of its name
    // (at least two characters). Case, spaces, periods and hyphens are
    // ignored, and "I", "II", "III", "First", "2nd" etc. work as numbers.
    // Returns null for unknown or ambiguous names ("Jo" could be five books).
    public static BibleBook? Find(string name)
    {
        var key = Key(name);
        if (key.Length < 2) return null;
        if (ByKey.Value.TryGetValue(key, out var book)) return book;
        var matches = All.Where(b => Key(b.Name).StartsWith(key, StringComparison.Ordinal)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static Dictionary<string, BibleBook> BuildKeys()
    {
        var keys = new Dictionary<string, BibleBook>();
        foreach (var book in All)
            foreach (var name in book.Aliases.Prepend(book.Name))
                keys.Add(Key(name), book);
        return keys;
    }

    private static string Key(string name)
    {
        var s = Ordinal().Replace(name.Trim().ToLowerInvariant(), m =>
            m.Groups["three"].Success ? "3" : m.Groups["two"].Success ? "2" : "1");
        return NonAlphanumeric().Replace(s, "");
    }

    [GeneratedRegex(@"^(?:(?<three>iii|third|3rd)|(?<two>ii|second|2nd)|(?<one>i|first|1st))[\s.-]+")]
    private static partial Regex Ordinal();

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NonAlphanumeric();
}

using System.Text.RegularExpressions;

namespace starfold_mobile.Bible;

// Parses the editor's references field: semicolon-separated items such as
//   Genesis; Rom 8; Rom 8-9; John 3:16; John 3:16-18; John 3:16-4:2; Jude 5
// Book names go through BibleBooks.Find. Chapters and verses are checked
// against the LSB counts. A range that crosses chapters becomes one row per
// chapter (John 3:16-4:2 -> John 3:16-36 and John 4:1-2). In a one-chapter
// book (Obadiah, Philemon, 2-3 John, Jude) a bare number is a verse.
//
// Within an item, commas continue the same book and chapter:
//   John 3:16, 18      -> John 3:16 and John 3:18
//   John 3:16-18, 21   -> John 3:16-18 and John 3:21
//   Rom 8:28, 9:1      -> Romans 8:28 and Romans 9:1
//   Rom 8, 9           -> Romans 8 and Romans 9
// A comma piece that names a book ("John 3:16, Rom 8") stands on its own.
public static partial class ReferenceParser
{
    public sealed record Result(List<ScriptureRef> Refs, List<string> Errors);

    // What a comma piece continues: the last book, its last chapter (null for
    // a whole book), and whether the last piece was at verse level.
    private sealed record Context(BibleBook Book, int? Chapter, bool Verses)
    {
        public string Continue(string piece) =>
            Chapter is null || piece.IndexOfAny([':', '.']) >= 0 ? $"{Book.Name} {piece}"
            : Verses ? $"{Book.Name} {Chapter}:{piece}"
            : $"{Book.Name} {piece}";
    }

    public static Result Parse(string? input)
    {
        var refs = new List<ScriptureRef>();
        var errors = new List<string>();
        foreach (var part in (input ?? "").Split(';'))
        {
            var item = part.Trim();
            if (item == "") continue;
            Context? context = null;
            foreach (var piece in item.Split(','))
            {
                var text = piece.Trim();
                if (text == "") continue;
                if (context is not null && !text.Any(char.IsLetter)) text = context.Continue(text);
                var error = ParseOne(text, refs, out context);
                if (error is null) continue;
                errors.Add($"\"{item}\": {error}");
                break;
            }
        }
        return new([.. refs.Distinct()], errors);
    }

    private static string? ParseOne(string text, List<ScriptureRef> refs, out Context? context)
    {
        context = null;
        var m = Reference().Match(text);
        if (!m.Success) return "couldn't read this reference.";

        var bookText = m.Groups["book"].Value.Trim();
        var book = BibleBooks.Find(bookText);
        if (book is null) return $"unknown book \"{bookText}\".";
        if (!m.Groups["c1"].Success)
        {
            refs.Add(new(book.Number));
            context = new(book, null, false);
            return null;
        }

        int c1 = Num(m, "c1"), c2;
        int? v1 = OptNum(m, "v1"), v2;
        if (!m.Groups["c2"].Success) { c2 = c1; v2 = v1; }
        else if (m.Groups["v2"].Success) { c2 = Num(m, "c2"); v2 = Num(m, "v2"); }
        else if (v1 is not null) { c2 = c1; v2 = Num(m, "c2"); } // 3:16-18: the end is a verse
        else { c2 = Num(m, "c2"); v2 = null; }                    // 8-9: the end is a chapter

        // One-chapter books: "Jude 5" and "Jude 3-5" are verses.
        if (book.Chapters == 1 && v1 is null && v2 is null)
        {
            (v1, v2) = (c1, c2);
            c1 = c2 = 1;
        }

        if (c1 < 1 || c1 > book.Chapters || c2 < 1 || c2 > book.Chapters)
            return $"{book.Name} has {book.Chapters} chapter{(book.Chapters == 1 ? "" : "s")}.";
        if (v1 is < 1 || v1 > book.VersesIn(c1)) return $"{book.Name} {c1} has {book.VersesIn(c1)} verses.";
        if (v2 is < 1 || v2 > book.VersesIn(c2)) return $"{book.Name} {c2} has {book.VersesIn(c2)} verses.";
        if (c2 < c1 || (c2 == c1 && v1 is not null && v2 < v1)) return "the range ends before it starts.";

        context = new(book, c2, v2 is not null);
        if (c1 == c2)
        {
            // "8-8:5" starts at verse 1.
            refs.Add(v1 is null && v2 is null ? new(book.Number, c1) : new(book.Number, c1, v1 ?? 1, v2));
            return null;
        }
        refs.Add(v1 is null ? new(book.Number, c1) : new(book.Number, c1, v1, book.VersesIn(c1)));
        for (var c = c1 + 1; c < c2; c++) refs.Add(new(book.Number, c));
        refs.Add(v2 is null ? new(book.Number, c2) : new(book.Number, c2, 1, v2));
        return null;
    }

    private static int Num(Match m, string group) => int.Parse(m.Groups[group].Value);
    private static int? OptNum(Match m, string group) => m.Groups[group].Success ? Num(m, group) : null;

    // The book is everything before the first chapter number (lazy, so
    // "1 John 3:16" reads as "1 John" + "3:16"); it can't end in a digit, so
    // "John 9999" doesn't become "John 9" + 999. Numbers are capped at three
    // digits; the range checks do the rest. Dashes: - – —.
    [GeneratedRegex(@"^(?<book>.*?[^\d\s].*?)(?<!\d)(?:\s*(?<c1>\d{1,3})(?:\s*[:.]\s*(?<v1>\d{1,3}))?(?:\s*[-–—]\s*(?<c2>\d{1,3})(?:\s*[:.]\s*(?<v2>\d{1,3}))?)?)?$")]
    private static partial Regex Reference();
}

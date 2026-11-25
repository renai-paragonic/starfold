using System.Text.Json;
using System.Text.RegularExpressions;

namespace starfold_mobile.Data;

// Places Logos notes anchored to a text selection in a Bible. Logos stores only
// a character offset into its (licensed, encrypted) Bible resource, so the
// verse is worked out, not read. For one resource and book, with the notes in
// offset order:
//   1. Fixed points: "Chapter N" heading notes (a chapter starts right after
//      them), and long quotes whose wording clearly matches a run of 1–4
//      verses in the public-domain ASV (an F1 score over content words).
//   2. Every note's offset is interpolated between the fixed points, against
//      the ASV's verse lengths.
//   3. The quote's wording then picks the verse near that estimate.
// The feasibility test and its numbers are in ProjectPlans/ezra.md.
public static partial class LogosAlignment
{
    public sealed record AsvVerse(int Chapter, int Verse, string Text);

    // A note to place: its anchor's offset and length, and the anchored
    // quote from the HTML export (null when the export didn't have it).
    public sealed record Anchored(long Offset, int Length, string? Quote);

    public enum Confidence { High, Medium, Low, Heading }

    // Inclusive. A whole-chapter placement has verses 0 (a "Chapter N" heading).
    public sealed record Placement(int Chapter, int Verse, int EndChapter, int EndVerse, Confidence Confidence)
    {
        public bool WholeChapter => Verse == 0;

        // In the reference parser's text form, for book name `book`.
        public string Format(string book) =>
            WholeChapter ? $"{book} {Chapter}"
            : (Chapter, Verse) == (EndChapter, EndVerse) ? $"{book} {Chapter}:{Verse}"
            : Chapter == EndChapter ? $"{book} {Chapter}:{Verse}-{EndVerse}"
            : $"{book} {Chapter}:{Verse}-{EndChapter}:{EndVerse}";
    }

    // getbible.net's v2 JSON for a whole translation: books[].nr (1–66, the
    // same order as Ezra), chapters[].verses[] with chapter, verse and text.
    public static Dictionary<int, List<AsvVerse>> ReadAsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var books = new Dictionary<int, List<AsvVerse>>();
        foreach (var book in doc.RootElement.GetProperty("books").EnumerateArray())
            books[book.GetProperty("nr").GetInt32()] = [.. book.GetProperty("chapters").EnumerateArray()
                .SelectMany(c => c.GetProperty("verses").EnumerateArray())
                .Select(v => new AsvVerse(v.GetProperty("chapter").GetInt32(), v.GetProperty("verse").GetInt32(), v.GetProperty("text").GetString()!.Trim()))];
        return books;
    }

    // Placements for `notes` (one resource, one book), in the same order.
    // Null when a note can't be placed at all.
    public static List<Placement?> Place(IReadOnlyList<AsvVerse> asv, IReadOnlyList<Anchored> notes)
    {
        var verses = new List<(int Start, int End, HashSet<string> Words)>();
        var pos = 0;
        foreach (var v in asv)
        {
            verses.Add((pos, pos + v.Text.Length + 1, [.. Words(v.Text)]));
            pos += v.Text.Length + 1;
        }

        var order = Enumerable.Range(0, notes.Count).OrderBy(i => notes[i].Offset).ThenBy(i => notes[i].Length).ToList();
        var n = order.Select(i => notes[i]).ToList();
        var quotes = n.Select(x => x.Quote is null ? null : Normalize(x.Quote)).ToList();

        // Headings: "Chapter N" (or "Psalm N"), and section headings, which are
        // short title-case lines with no full stop.
        var chapterOf = new int?[n.Count];
        var heading = new bool[n.Count];
        for (var i = 0; i < n.Count; i++)
        {
            if (quotes[i] is not string q) continue;
            var m = ChapterHeading().Match(q);
            if (m.Success && asv.Any(v => v.Chapter == int.Parse(m.Groups[1].Value)))
                (chapterOf[i], heading[i]) = (int.Parse(m.Groups[1].Value), true);
            else if (q.Length < 40 && q.Contains(' ') && !q.Contains('.') && q.Split(' ').Where(w => w.Length > 3).All(w => char.IsUpper(w[0])))
                heading[i] = true;
        }

        var best = n.Select((_, i) => quotes[i] is string q && !heading[i] ? BestWindow(q, verses) : null).ToList();

        // Fixed points: (resource offset, ASV position, note).
        var pins = new List<(long Off, double Asv, int Note)>();
        for (var i = 0; i < n.Count; i++)
            if (chapterOf[i] is int c) pins.Add((n[i].Offset + n[i].Length, verses[asv.Select((v, k) => (v, k)).First(x => x.v.Chapter == c).k].Start, i));
        var chapterPins = pins.ToList();
        for (var i = 0; i < n.Count; i++)
        {
            if (best[i] is not { } b || Words(quotes[i]!).Count() < 8 || b.Score < 0.35 || b.Margin < 0.12) continue;
            if (chapterPins.Count > 0 && Math.Abs(VerseAt(Estimate(n[i].Offset, chapterPins)!.Value) - b.Start) > 4) continue;
            pins.Add((n[i].Offset, verses[b.Start].Start, i));
        }
        // Keep the fixed points in order: drop a text pin that runs backwards.
        pins = [.. pins.OrderBy(p => p.Off)];
        for (var k = 1; k < pins.Count;)
        {
            if (pins[k].Asv >= pins[k - 1].Asv) { k++; continue; }
            pins.RemoveAt(heading[pins[k].Note] ? k - 1 : k);
            k = Math.Max(1, k - 1);
        }
        var pinned = pins.Where(p => !heading[p.Note]).Select(p => p.Note).ToHashSet();

        var placed = new Placement?[n.Count];
        for (var i = 0; i < n.Count; i++)
        {
            if (heading[i]) continue;
            var start = Estimate(n[i].Offset, pins);
            if (start is null)
            {
                // Nothing to measure from: the wording alone, if it's clear.
                if (best[i] is { } b && b.Score > 0)
                    placed[i] = Make(b.Start, b.End, b.Score >= 0.35 && b.Margin >= 0.12 ? Confidence.Medium : Confidence.Low);
                continue;
            }
            int ps = VerseAt(start.Value + 1), pe = Math.Max(ps, VerseAt(Estimate(n[i].Offset + n[i].Length - 1, pins)!.Value));
            if (pinned.Contains(i))
                placed[i] = Make(best[i]!.Start, Math.Max(best[i]!.Start, Math.Min(best[i]!.End, pe)), Confidence.High);
            // A long quote: the best run of verses starting within one of the
            // estimate. The ASV's next verse often shares words, so the end
            // is capped by where the anchor's end lands.
            else if (quotes[i] is string lq && Words(lq).Count() >= 8 && BestWindow(lq, verses, ps - 1, ps + 1) is { Score: > 0 } w)
                placed[i] = Make(w.Start, Math.Max(w.Start, Math.Min(w.End, pe)), w.Score >= 0.35 ? Confidence.High : Confidence.Medium);
            // A short one: the verse near the estimate with most of its words.
            else if (quotes[i] is string q && Local(q, ps) is var (verse, recall) && recall > 0)
                placed[i] = Make(verse, verse, recall >= 0.6 ? Confidence.High : Confidence.Medium);
            else
                placed[i] = Make(ps, quotes[i] is string sq && Words(sq).Count() < 8 ? ps : pe, Confidence.Low);
        }

        // A "Chapter N" heading is that chapter; a section heading starts
        // where the next note starts.
        for (var i = 0; i < n.Count; i++)
        {
            if (!heading[i]) continue;
            if (chapterOf[i] is int c) placed[i] = new(c, 0, c, 0, Confidence.Heading);
            else
            {
                var next = Enumerable.Range(i + 1, n.Count - i - 1).Select(k => placed[k]).FirstOrDefault(p => p is { WholeChapter: false });
                var at = next is not null ? (next.Chapter, next.Verse)
                    : Estimate(n[i].Offset + n[i].Length, pins) is double e ? (asv[VerseAt(e + 1)].Chapter, asv[VerseAt(e + 1)].Verse) : ((int, int)?)null;
                if (at is var (ch, v)) placed[i] = new(ch, v, ch, v, Confidence.Heading);
            }
        }

        var result = new Placement?[notes.Count];
        for (var k = 0; k < order.Count; k++) result[order[k]] = placed[k];
        return [.. result];

        Placement Make(int s, int e, Confidence conf) => new(asv[s].Chapter, asv[s].Verse, asv[e].Chapter, asv[e].Verse, conf);

        int VerseAt(double at)
        {
            var k = verses.FindIndex(v => at < v.End);
            return k < 0 ? verses.Count - 1 : k;
        }

        // The ASV verse within one of `around` that has the most of the quote's words.
        (int, double) Local(string quote, int around)
        {
            var qw = Words(quote).ToList();
            if (qw.Count == 0) return (around, 0);
            return new[] { around, around + 1, around - 1 }.Where(k => k >= 0 && k < verses.Count)
                .Select(k => (k, qw.Count(verses[k].Words.Contains) / (double)qw.Count))
                .OrderByDescending(x => x.Item2).First();
        }

        double? Estimate(long off, List<(long Off, double Asv, int Note)> ps)
        {
            if (ps.Count == 0) return null;
            var p = ps.OrderBy(x => x.Off).ToList();
            if (off <= p[0].Off) return Math.Max(0, p[0].Asv - (p[0].Off - off));
            if (off >= p[^1].Off) return Math.Min(verses[^1].End - 1, p[^1].Asv + (off - p[^1].Off));
            var k = p.FindLastIndex(x => x.Off <= off);
            var (a, b) = (p[k], p[k + 1]);
            return b.Off == a.Off ? a.Asv : a.Asv + (b.Asv - a.Asv) * (off - a.Off) / (b.Off - a.Off);
        }
    }

    private sealed record Window(int Start, int End, double Score, double Margin);

    // The run of 1–4 verses (starting from `from` to `to`) whose words best match the quote, and how far
    // ahead it is of the best run that doesn't overlap it.
    private static Window? BestWindow(string quote, List<(int Start, int End, HashSet<string> Words)> verses, int from = 0, int to = int.MaxValue)
    {
        var qw = Words(quote).ToList();
        if (qw.Count == 0) return null;
        var distinct = qw.Distinct().ToList();
        var scores = new List<(int S, int E, double F)>();
        for (var s = Math.Max(0, from); s < verses.Count && s <= to; s++)
        {
            var w = new HashSet<string>();
            for (var e = s; e < Math.Min(verses.Count, s + 4); e++)
            {
                w.UnionWith(verses[e].Words);
                var hit = qw.Count(w.Contains);
                if (hit == 0) continue;
                double recall = hit / (double)qw.Count, precision = distinct.Count(w.Contains) / (double)w.Count;
                scores.Add((s, e, 2 * recall * precision / (recall + precision)));
            }
        }
        if (scores.Count == 0) return null;
        var top = scores.MaxBy(x => x.F);
        var rival = scores.Where(x => x.S > top.E || x.E < top.S).Select(x => x.F).DefaultIfEmpty(0).Max();
        return new(top.S, top.E, top.F, top.F - rival);
    }

    // The export's quotes carry Logos's spacing around formatting (" , ") and
    // its "•" marks for words the translators supplied.
    private static string Normalize(string quote) => Spaces().Replace(quote.Replace("•", ""), " ").Trim();

    private static readonly HashSet<string> Stop =
    [
        "the", "and", "of", "to", "in", "a", "that", "is", "he", "his", "him", "for", "by", "which", "who", "with", "be",
        "it", "as", "we", "us", "you", "your", "are", "was", "not", "but", "have", "has", "from", "on", "this", "they",
        "them", "all", "an", "so", "at", "or", "were", "had", "their", "also", "unto", "ye", "hath", "shall", "will",
        "even", "our", "my", "me", "i",
    ];

    // Content words, lightly stemmed so the ASV's "maketh" meets "makes".
    private static IEnumerable<string> Words(string text)
    {
        foreach (Match m in Word().Matches(text.ToLowerInvariant()))
        {
            var w = m.Value;
            if (Stop.Contains(w)) continue;
            yield return w.Length > 4 && (w.EndsWith("eth") || w.EndsWith("est")) ? w[..^3]
                : w.Length > 3 && w.EndsWith('s') ? w[..^1] : w;
        }
    }

    [GeneratedRegex("[a-z]+")]
    private static partial Regex Word();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"^(?:Chapter|Psalm) (\d+)$")]
    private static partial Regex ChapterHeading();
}

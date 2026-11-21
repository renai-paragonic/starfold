using starfold_mobile.Bible;

namespace starfold_mobile.Models.Ezra;

// A scripture tag. Book is 1–66 in canonical order. A null Chapter means the
// whole book; a null VerseStart means the whole chapter. Ranges stay within
// one chapter.
public class NoteRef
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public int Book { get; set; }
    public int? Chapter { get; set; }
    public int? VerseStart { get; set; }
    public int? VerseEnd { get; set; }

    public Note Note { get; set; } = null!;

    public ScriptureRef ToScriptureRef() => new(Book, Chapter, VerseStart, VerseEnd);

    // The refs that a browse page or a search chip for `q` finds: a book finds
    // every ref in it; a chapter also finds the whole book; a verse or range
    // also finds its whole chapter or book, and overlapping verse ranges.
    public static IQueryable<NoteRef> Matching(IQueryable<NoteRef> refs, ScriptureRef q)
    {
        refs = refs.Where(r => r.Book == q.Book);
        if (q.Chapter is int c) refs = refs.Where(r => r.Chapter == null || r.Chapter == c);
        if (q.VerseStart is int v1)
        {
            var v2 = q.VerseEnd ?? v1;
            refs = refs.Where(r => r.Chapter == null || r.VerseStart == null || (r.VerseStart <= v2 && r.VerseEnd >= v1));
        }
        return refs;
    }

    // Canonical order: book, then whole book before chapters, then verses.
    public static IEnumerable<NoteRef> Sort(IEnumerable<NoteRef> refs) =>
        refs.OrderBy(r => r.Book).ThenBy(r => r.Chapter ?? 0).ThenBy(r => r.VerseStart ?? 0).ThenBy(r => r.VerseEnd ?? 0);
}

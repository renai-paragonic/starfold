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
}

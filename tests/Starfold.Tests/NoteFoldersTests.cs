using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class NoteFoldersTests
{
    private static Note Make(DateTime? created = null, string[]? tags = null, string[]? strongs = null, params (int Book, int? Chapter, int? Verse)[] refs) => new()
    {
        CreatedAt = created ?? new DateTime(2026, 11, 23, 18, 0, 0),
        Tags = [.. (tags ?? []).Select(t => new Tag { Name = t })],
        Strongs = [.. (strongs ?? []).Select(id => new StrongsEntry { Id = id })],
        Refs = [.. refs.Select(r => new NoteRef { Book = r.Book, Chapter = r.Chapter, VerseStart = r.Verse, VerseEnd = r.Verse })],
    };

    [Fact]
    public void Book_folders_are_in_canonical_order_then_unfiled()
    {
        Note[] notes = [Make(refs: (45, 8, null)), Make(refs: [(1, null, null), (45, null, null)]), Make()];
        Assert.Equal(
            [new Folder("Genesis", "/ezra/bible/genesis"), new Folder("Romans", "/ezra/bible/romans"), new Folder("Unfiled", "/ezra/unfiled/books")],
            NoteFolders.Root(notes, NoteFolders.Books));
    }

    [Fact]
    public void No_unfiled_folder_when_every_note_is_filed() =>
        Assert.DoesNotContain(NoteFolders.Root([Make(tags: ["Grace"])], NoteFolders.Tags), f => f.Label == "Unfiled");

    [Fact]
    public void Tag_folders_are_alphabetical_and_escaped()
    {
        Note[] notes = [Make(tags: ["sermons", "Grace notes"]), Make(tags: ["Grace notes"]), Make(refs: (43, 3, 16))];
        Assert.Equal(
            [new Folder("Grace notes", "/ezra/tags/Grace%20notes"), new Folder("sermons", "/ezra/tags/sermons"), new Folder("Unfiled", "/ezra/unfiled/tags")],
            NoteFolders.Root(notes, NoteFolders.Tags));
    }

    [Fact]
    public void Strongs_folders_are_hebrew_then_greek_with_no_unfiled()
    {
        Note[] notes = [Make(strongs: ["G26"]), Make(strongs: ["H430", "G25"]), Make()];
        Assert.Equal(
            [new Folder("Hebrew", "/ezra/strongs/hebrew"), new Folder("Greek", "/ezra/strongs/greek")],
            NoteFolders.Root(notes, NoteFolders.Strongs));
    }

    [Fact]
    public void An_empty_language_has_no_folder() =>
        Assert.Equal([new Folder("Greek", "/ezra/strongs/greek")], NoteFolders.Root([Make(strongs: ["G26"])], NoteFolders.Strongs));

    [Fact]
    public void Date_folders_are_newest_first_with_no_unfiled()
    {
        Note[] notes = [Make(new DateTime(2026, 1, 5, 15, 0, 0)), Make(new DateTime(2026, 11, 23, 15, 0, 0)), Make(new DateTime(2026, 1, 5, 16, 0, 0))];
        Assert.Equal(
            [new Folder("11/23/2026", "/ezra/dates/2026-11-23"), new Folder("01/05/2026", "/ezra/dates/2026-01-05")],
            NoteFolders.Root(notes, NoteFolders.Dates));
    }

    // Stored times are UTC; the folders follow US Central, daylight saving included.
    [Theory]
    [InlineData("2026-11-24T02:00:00", "2026-11-23")] // 8pm CST
    [InlineData("2026-11-24T06:00:00", "2026-11-24")] // midnight CST
    [InlineData("2026-07-04T04:30:00", "2026-07-03")] // 11:30pm CDT
    [InlineData("2026-07-04T05:00:00", "2026-07-04")] // midnight CDT
    public void Local_date_is_us_central(string utc, string day) =>
        Assert.Equal(DateOnly.Parse(day), NoteFolders.LocalDate(DateTime.Parse(utc)));

    [Theory]
    [InlineData("2026-11-23", true)]
    [InlineData("2026-02-30", false)]
    [InlineData("11-23-2026", false)]
    [InlineData("2026-1-5", false)]
    public void Date_slugs_are_iso(string slug, bool ok) => Assert.Equal(ok, NoteFolders.ParseSlug(slug) is not null);

    [Fact]
    public void Chapter_folders_come_from_chapter_and_verse_refs_in_the_book()
    {
        Note[] notes = [Make(refs: [(45, 8, 28), (45, null, null)]), Make(refs: [(45, 3, null), (45, 8, null)]), Make(refs: (46, 1, null))];
        Assert.Equal(["3", "8"], NoteFolders.Chapters(notes, 45).Select(f => f.Label));
        Assert.Equal("/ezra/bible/romans/8", NoteFolders.Chapters(notes, 45)[1].Url);
    }

    [Fact]
    public void Chip_text_is_scripture_then_strongs_then_tags_in_chip_order()
    {
        var note = Make(tags: ["sermons", "Grace notes"], refs: [(45, 8, 28), (43, 3, 16), (1, null, null)]);
        note.Strongs = [new StrongsEntry { Id = "H430", Lemma = "אֱלֹהִים" }, new StrongsEntry { Id = "G26", Lemma = "ἀγάπη" }];
        Assert.Equal("Genesis, John 3:16, Romans 8:28, G26 ἀγάπη, H430 אֱלֹהִים, Grace notes, sermons", NoteFolders.ChipText(note));
    }

    [Fact]
    public void A_note_with_no_chips_has_empty_chip_text() => Assert.Equal("", NoteFolders.ChipText(Make()));

    [Fact]
    public void Whole_book_notes_stay_in_the_book_folder()
    {
        var wholeBook = Make(refs: (45, null, null));
        var verse = Make(refs: (45, 8, 28));
        Assert.True(NoteFolders.InBook(wholeBook, 45));
        Assert.False(NoteFolders.InBook(verse, 45));
        Assert.False(NoteFolders.InChapter(wholeBook, 45, 8));
        Assert.True(NoteFolders.InChapter(verse, 45, 8));
        Assert.False(NoteFolders.InChapter(verse, 45, 9));
    }
}

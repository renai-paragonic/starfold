using System.Globalization;
using starfold_mobile.Bible;

namespace starfold_mobile.Models.Ezra;

// The folder view of notes: filed by Bible book, user tag, or the date they
// were written. These group notes the caller has already loaded (and filtered
// for the visitor), so the rules live here in one place.
public static class NoteFolders
{
    public const string Books = "books", Strongs = "strongs", Tags = "tags", Dates = "dates";
    public static readonly string[] Views = [Books, Strongs, Tags, Dates];

    // Notes show as file icons, or as the plain list when this cookie is
    // "list" (the Icons | List toggle, remembered for every page).
    public const string LayoutCookie = "ezra_layout";

    // Date folders follow US Central time, so an evening note files under
    // the day it was written, not the next UTC day.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    public static DateOnly LocalDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    // "11/23/2026" for the label, "2026-11-23" for the URL (no slashes in a path segment).
    public static string Label(DateOnly day) => day.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
    public static string Slug(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly? ParseSlug(string slug) =>
        DateOnly.TryParseExact(slug, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    // The root folders for a view, plus "Unfiled" when some notes have no
    // scripture refs (books) or no user tags (tags). Every note has a date.
    // Strong's holds Hebrew and Greek folders of entries, not notes, so it
    // has no Unfiled.
    public static List<Folder> Root(IReadOnlyCollection<Note> notes, string view)
    {
        var folders = view switch
        {
            Books => notes.SelectMany(n => n.Refs).Select(r => r.Book).Distinct().Order()
                .Select(b => new Folder(BibleBooks.Get(b).Name, new ScriptureRef(b).BrowsePath)).ToList(),
            Strongs => new[] { true, false }
                .Where(hebrew => notes.SelectMany(n => n.Strongs).Any(s => StrongsParser.IsHebrew(s.Id) == hebrew))
                .Select(Language).ToList(),
            Tags => notes.SelectMany(n => n.Tags).Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(name => new Folder(name, "/ezra/tags/" + Uri.EscapeDataString(name))).ToList(),
            _ => notes.Select(n => LocalDate(n.CreatedAt)).Distinct().OrderDescending()
                .Select(d => new Folder(Label(d), "/ezra/dates/" + Slug(d))).ToList(),
        };
        if (view is Books or Tags && notes.Any(n => IsUnfiled(n, view)))
            folders.Add(new Folder("Unfiled", "/ezra/unfiled/" + view));
        return folders;
    }

    public static Folder Language(bool hebrew) =>
        hebrew ? new("Hebrew", "/ezra/strongs/hebrew") : new("Greek", "/ezra/strongs/greek");

    public static bool IsUnfiled(Note note, string view) =>
        view == Books ? note.Refs.Count == 0 : note.Tags.Count == 0;

    // A book folder's chapter folders: chapters with a chapter or verse ref.
    public static List<Folder> Chapters(IEnumerable<Note> notes, int book) =>
        notes.SelectMany(n => n.Refs).Where(r => r.Book == book && r.Chapter != null)
            .Select(r => r.Chapter!.Value).Distinct().Order()
            .Select(c => new Folder(c.ToString(CultureInfo.InvariantCulture), new ScriptureRef(book, c).BrowsePath)).ToList();

    // A note icon's hover text: its chips as _NoteChips shows them, scripture
    // then Strong's then user tags, joined with commas.
    public static string ChipText(Note note) => string.Join(", ",
        NoteRef.Sort(note.Refs).Select(r => r.ToScriptureRef().Format())
            .Concat(StrongsParser.Sort(note.Strongs.Select(s => s.Id)).Select(id => $"{id} {note.Strongs.First(s => s.Id == id).Lemma}"))
            .Concat(note.Tags.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase)));

    // Notes filed in the book folder itself: those tagged with the whole book.
    public static bool InBook(Note note, int book) => note.Refs.Any(r => r.Book == book && r.Chapter == null);

    // Notes filed in a chapter folder: a ref to that chapter or its verses.
    // Whole-book notes stay in the book folder (Renai, 2026-11-23).
    public static bool InChapter(Note note, int book, int chapter) =>
        note.Refs.Any(r => r.Book == book && r.Chapter == chapter);
}

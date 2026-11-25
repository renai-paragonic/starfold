using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace starfold_mobile.Data;

// Reads notes from Logos Bible Software's local notes database,
// %LOCALAPPDATA%\Logos\Documents\<id>\NotesToolManager\notestool.db. The file
// belongs to Logos, so it's copied (with its -wal and -shm files) to a temp
// folder and read there, read-only. The schema notes are in ProjectPlans/ezra.md.
public static partial class LogosNotes
{
    // In priority order: the first kind a note has decides where it belongs.
    public enum AnchorKind { Reference, Word, BibleText, Other, None }

    // Reference is the raw Logos reference ("bible+nasb95.64.5.27",
    // "lemma.lbs.he.קום"). A text selection has a resource and a character
    // range in it instead.
    public sealed record Anchor(AnchorKind Kind, string? Reference, string? ResourceId, long Offset, int Length);

    public sealed record Note(
        string Id, DateTime CreatedUtc, DateTime? ModifiedUtc, string? Notebook,
        string? RichText, int? LogosBook, IReadOnlyList<Anchor> Anchors)
    {
        // Logos keeps empty paragraphs around, so look for actual text or links.
        public bool HasBody => RichText is not null && Visible().IsMatch(RichText);

        public AnchorKind Kind => Anchors.Select(a => a.Kind).DefaultIfEmpty(AnchorKind.None).Min();
    }

    // Logos numbers Genesis–Malachi 1–39, the Apocrypha 40–60 and
    // Matthew–Revelation 61–87. Returns Ezra's 1–66, or null for the Apocrypha.
    public static int? EzraBook(int logosBook) => logosBook switch
    {
        >= 1 and <= 39 => logosBook,
        >= 61 and <= 87 => logosBook - 21,
        _ => null,
    };

    // Live notes (not deleted or in the trash), oldest first.
    public static List<Note> Read(string path)
    {
        var copy = Directory.CreateTempSubdirectory("logos-notes-");
        try
        {
            var db = Path.Combine(copy.FullName, "notestool.db");
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                if (File.Exists(path + suffix)) File.Copy(path + suffix, db + suffix);

            // No pooling, so the copy can be deleted afterwards.
            var cs = new SqliteConnectionStringBuilder { DataSource = db, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
            using var c = new SqliteConnection(cs.ToString());
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = """
                select n.ExternalId, n.CreatedDate, n.ModifiedDate, b.Title, n.ContentRichText, n.AnchorBibleBook, n.AnchorsJson
                from Notes n
                left join Notebooks b on b.ExternalId = n.NotebookExternalId and b.IsDeleted = 0 and b.IsTrashed = 0
                where n.IsDeleted = 0 and n.IsTrashed = 0
                order by n.CreatedDate, n.NoteId
                """;
            using var r = cmd.ExecuteReader();
            var notes = new List<Note>();
            while (r.Read())
            {
                int? book = r.IsDBNull(5) ? null : r.GetInt32(5);
                notes.Add(new Note(
                    r.GetString(0),
                    Utc(r.GetString(1)),
                    r.IsDBNull(2) ? null : Utc(r.GetString(2)),
                    r.IsDBNull(3) || string.IsNullOrWhiteSpace(r.GetString(3)) ? null : r.GetString(3).Trim(),
                    r.IsDBNull(4) ? null : r.GetString(4),
                    book,
                    r.IsDBNull(6) ? [] : Anchors(r.GetString(6), book is not null)));
            }
            return notes;
        }
        finally
        {
            copy.Delete(recursive: true);
        }
    }

    // A text selection counts as Bible text when Logos filed the note under a
    // Bible book (AnchorBibleBook), which it does only for Bible resources.
    private static List<Anchor> Anchors(string json, bool inBible)
    {
        var anchors = new List<Anchor>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return anchors;
        foreach (var a in doc.RootElement.EnumerateArray())
        {
            if (a.TryGetProperty("reference", out var reference) && reference.TryGetProperty("raw", out var raw))
            {
                var s = raw.GetString() ?? "";
                var kind = s.StartsWith("bible", StringComparison.Ordinal) ? AnchorKind.Reference
                    : s.StartsWith("lemma.", StringComparison.Ordinal) || s.StartsWith("root.", StringComparison.Ordinal) ? AnchorKind.Word
                    : AnchorKind.Other;
                anchors.Add(new Anchor(kind, s, null, 0, 0));
            }
            else if (a.TryGetProperty("textRange", out var range))
                anchors.Add(new Anchor(
                    inBible ? AnchorKind.BibleText : AnchorKind.Other, null,
                    range.GetProperty("resourceId").GetString(),
                    range.GetProperty("offset").GetInt64(),
                    range.GetProperty("length").GetInt32()));
            else
                anchors.Add(new Anchor(AnchorKind.Other, null, null, 0, 0));
        }
        return anchors;
    }

    private static DateTime Utc(string s) =>
        DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    [GeneratedRegex("""Text="[^"]*[^\s"]|<Reference |<UriLink """)]
    private static partial Regex Visible();
}

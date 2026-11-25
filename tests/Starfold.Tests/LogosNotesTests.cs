using Microsoft.Data.Sqlite;
using starfold_mobile.Data;
using Kind = starfold_mobile.Data.LogosNotes.AnchorKind;

namespace Starfold.Tests;

// LogosNotes.Read against a small database with the Logos columns it uses.
public sealed class LogosNotesTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("logos-test-").FullName;
    private readonly string _path;

    public LogosNotesTests()
    {
        _path = Path.Combine(_dir, "notestool.db");
        Exec("""
            create table Notes (NoteId integer primary key autoincrement, ExternalId text not null, CreatedDate text not null,
                ModifiedDate text, IsDeleted bool not null default 0, IsTrashed bool not null default 0, Kind int not null,
                ContentRichText text, AnchorsJson text, AnchorBibleBook int, NotebookExternalId text not null);
            create table Notebooks (NotebookId integer primary key autoincrement, ExternalId text not null, CreatedDate text not null,
                IsDeleted bool not null default 0, IsTrashed bool not null default 0, Title text);
            insert into Notebooks (ExternalId, CreatedDate, Title) values ('nb1', '2026-06-01T00:00:00Z', ' TULIP '), ('nb2', '2026-06-01T00:00:00Z', 'Gone');
            update Notebooks set IsTrashed = 1 where ExternalId = 'nb2';
            """);
        Note("heb", "2026-01-24T13:35:49Z", """<Paragraph><Run Text="Hebrews intro"/></Paragraph>""",
            """[{"textRange":{"resourceId":"LLS:NASB95PARA","version":"x","offset":5351298,"length":9}}]""", book: 79);
        Note("john", "2026-06-04T23:03:32Z", """<Paragraph><Run Text="Son of Man"/></Paragraph>""",
            """[{"reference":{"raw":"bible+nasb95.64.5.27"}}]""", book: 64, notebook: "nb1");
        Note("word", "2026-09-19T14:08:36Z", """<Paragraph><Run Text="arise"/></Paragraph>""",
            """[{"reference":{"raw":"root.lbs.el.δουλος"}},{"textRange":{"resourceId":"LLS:1.0.710","version":"x","offset":5,"length":12}},{"reference":{"raw":"bible+esv.66.1.1"}}]""");
        Note("book", "2026-07-01T00:00:00Z", """<Paragraph><Run Text="from a book"/></Paragraph>""",
            """[{"textRange":{"resourceId":"LLS:MTPHYSCSVLBK","version":"x","offset":5,"length":12}}]""", notebook: "nb2");
        Note("empty", "2026-07-02T00:00:00Z", """<Paragraph FontSize="11"/><Paragraph><Run Text="  "/></Paragraph>""", null);
        Note("link", "2026-07-03T00:00:00Z", """<Paragraph><Reference Reference="bible+nasb95.1.12.1" IsLink="True"/></Paragraph>""", null);
        Note("deleted", "2026-07-04T00:00:00Z", """<Paragraph><Run Text="x"/></Paragraph>""", null, deleted: true);
        Note("trashed", "2026-07-05T00:00:00Z", """<Paragraph><Run Text="x"/></Paragraph>""", null, trashed: true);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private List<LogosNotes.Note> Read() => LogosNotes.Read(_path);
    private LogosNotes.Note Get(string id) => Read().Single(n => n.Id == id);

    [Fact]
    public void Leaves_out_deleted_and_trashed_notes_and_sorts_by_date() =>
        Assert.Equal(["heb", "john", "book", "empty", "link", "word"], Read().Select(n => n.Id));

    [Fact]
    public void Reads_dates_as_utc()
    {
        var n = Get("heb");
        Assert.Equal(new DateTime(2026, 1, 24, 13, 35, 49, DateTimeKind.Utc), n.CreatedUtc);
        Assert.Equal(DateTimeKind.Utc, n.CreatedUtc.Kind);
        Assert.Null(n.ModifiedUtc);
    }

    [Fact]
    public void Names_the_notebook_unless_it_is_trashed()
    {
        Assert.Equal("TULIP", Get("john").Notebook);
        Assert.Null(Get("book").Notebook);
        Assert.Null(Get("heb").Notebook);
    }

    [Theory]
    [InlineData("heb", true)]
    [InlineData("empty", false)]
    [InlineData("link", true)]
    public void Knows_which_notes_have_a_body(string id, bool hasBody) => Assert.Equal(hasBody, Get(id).HasBody);

    [Theory]
    [InlineData("heb", Kind.BibleText)]
    [InlineData("john", Kind.Reference)]
    [InlineData("word", Kind.Reference)]
    [InlineData("book", Kind.Other)]
    [InlineData("empty", Kind.None)]
    public void Picks_the_anchor_kind_by_priority(string id, Kind kind) => Assert.Equal(kind, Get(id).Kind);

    [Fact]
    public void Reads_each_anchor()
    {
        var a = Get("word").Anchors;
        Assert.Equal([Kind.Word, Kind.Other, Kind.Reference], a.Select(x => x.Kind));
        Assert.Equal("root.lbs.el.δουλος", a[0].Reference);
        var heb = Assert.Single(Get("heb").Anchors);
        Assert.Equal(("LLS:NASB95PARA", 5351298L, 9), (heb.ResourceId, heb.Offset, heb.Length));
    }

    [Fact]
    public void Leaves_the_original_file_alone()
    {
        var before = File.ReadAllBytes(_path);
        Read();
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(39, 39)]
    [InlineData(61, 40)]
    [InlineData(79, 58)]
    [InlineData(87, 66)]
    public void Maps_logos_books_to_ezra_books(int logos, int ezra) => Assert.Equal(ezra, LogosNotes.EzraBook(logos));

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(60)]
    [InlineData(88)]
    public void Has_no_ezra_book_for_the_apocrypha_or_out_of_range(int logos) => Assert.Null(LogosNotes.EzraBook(logos));

    private void Note(string id, string created, string body, string? anchors, int? book = null, string notebook = "", bool deleted = false, bool trashed = false) =>
        Exec("insert into Notes (ExternalId, CreatedDate, Kind, ContentRichText, AnchorsJson, AnchorBibleBook, NotebookExternalId, IsDeleted, IsTrashed) values ($id, $c, 0, $body, $a, $b, $nb, $d, $t)",
            ("$id", id), ("$c", created), ("$body", body), ("$a", anchors), ("$b", book), ("$nb", notebook), ("$d", deleted), ("$t", trashed));

    private void Exec(string sql, params (string Name, object? Value)[] ps)
    {
        using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path, Pooling = false }.ToString());
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}

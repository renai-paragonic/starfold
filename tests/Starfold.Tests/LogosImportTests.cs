using Microsoft.EntityFrameworkCore;
using starfold_mobile.Bible;
using starfold_mobile.Data;
using static starfold_mobile.Data.LogosNotes;

namespace Starfold.Tests;

// LogosImport.Plan: how each kind of anchor becomes refs and Strong's.
public class LogosImportTests
{
    private static readonly LogosWords Words = new([("H6965", "קוּם"), ("H6966", "קוּם")]);

    private static readonly Dictionary<int, List<LogosAlignment.AsvVerse>> Asv = new()
    {
        [58] = [new(1, 1, "God, having of old time spoken unto the fathers in the prophets by divers portions and in divers manners,"),
                new(1, 2, "hath at the end of these days spoken unto us in his Son, whom he appointed heir of all things, through whom also he made the worlds;")],
    };

    private static Note N(string id, string body, int? book, params Anchor[] anchors) =>
        new(id, new DateTime(2026, 1, 24, 13, 0, 0, DateTimeKind.Utc), null, null, $"<Paragraph><Run Text=\"{body}\"/></Paragraph>{(id == "john" ? "<Paragraph><Reference Reference=\"bible+nasb95.64.5.27\" IsLink=\"True\"><Run Text=\"John 5:27\"/></Reference><Reference Reference=\"bible+nasb95.1.1.1\" IsLink=\"True\"><Run Text=\"Gen 1:1\"/></Reference></Paragraph>" : "")}", book, anchors);

    private static Anchor Ref(string raw) => new(raw.StartsWith("bible") ? AnchorKind.Reference : AnchorKind.Word, raw, null, 0, 0);
    private static Anchor Text(long offset, int length) => new(AnchorKind.BibleText, null, "LLS:NASB95PARA", offset, length);

    private static List<LogosImport.Planned> Plan(List<Note> notes, List<LogosExport.ExportNote>? export = null) =>
        LogosImport.Plan(notes, export ?? [], Asv, Words);

    [Fact]
    public void A_reference_anchor_is_the_only_ref_and_body_links_stay_links()
    {
        var p = Plan([N("john", "Son of Man", 64, Ref("bible+nasb95.64.5.27"))]).Single();
        Assert.Equal("John 5:27", p.AnchorLabel);
        Assert.Null(p.Confidence);
        Assert.Equal([new ScriptureRef(43, 5, 27, 27)], p.Refs);
        Assert.Contains("[Gen 1:1](https://www.biblegateway.com/passage/?search=Genesis%201%3A1&version=LSB)", p.Markdown);
    }

    [Fact]
    public void A_note_with_only_body_links_has_no_refs() =>
        Assert.Empty(Plan([N("john", "Son of Man", null)]).Single().Refs);

    [Fact]
    public void A_word_anchor_becomes_a_strongs_tag_and_reports_a_tie()
    {
        var p = Plan([N("arise", "arise", null, Ref("lemma.lbs.he.קום"))]).Single();
        Assert.Equal(["H6965"], p.Strongs);
        Assert.Null(p.AnchorLabel);
        Assert.Equal(["lemma.lbs.he.קום matches H6965, H6966; took H6965."], p.Problems);
    }

    [Fact]
    public void A_text_anchor_is_placed_with_its_quote_from_the_export()
    {
        var notes = new List<Note>
        {
            N("one", "The letter opens", 79, Text(100, 200)),
            N("two", "His voice", 79, Text(320, 150)),
        };
        var export = new List<LogosExport.ExportNote>
        {
            new("God, after He spoke long ago to the fathers in the prophets in many portions and in many ways", "theletteropens"),
            new("in these last days has spoken to us in His Son, whom He appointed heir of all things, through whom also He made the world", "hisvoice"),
        };
        var plan = Plan(notes, export);
        Assert.Equal(["Hebrews 1:1", "Hebrews 1:2"], plan.Select(p => p.AnchorLabel));
        Assert.Equal([new ScriptureRef(58, 1, 2, 2)], plan[1].Refs);
        Assert.All(plan, p => Assert.Empty(p.Problems));
    }

    [Fact]
    public void A_text_anchor_with_no_quote_is_reported()
    {
        var p = Plan([N("one", "The letter opens", 79, Text(100, 0))]).Single();
        Assert.Null(p.AnchorLabel);
        Assert.Equal(["no anchored text in the export, so it's placed by position only.", "couldn't be placed, so it has no anchor ref."], p.Problems);
    }

    [Fact]
    public void A_text_anchor_in_the_apocrypha_has_no_ref() =>
        Assert.Equal(["anchored in the Apocrypha, so it has no anchor ref."], Plan([N("tobit", "Tobit", 41, Text(5, 5))]).Single().Problems);

    [Fact]
    public void A_bad_reference_anchor_is_reported() =>
        Assert.Equal(["\"Romans 17:1\": Romans has 16 chapters."], Plan([N("bad", "x", 66, Ref("bible+nasb95.66.17.1"))]).Single().Problems);

    [Theory]
    [InlineData("Hebrews 1:1", "The letter opens with an absolutely packed theological statement. God has spoken",
        "Hebrews 1:1 — The letter opens with an absolutely packed theological…")]
    [InlineData("Ecclesiastes 2:3", "Limit testing", "Ecclesiastes 2:3 — Limit testing")]
    [InlineData(null, "Grace is not the only thing that is true", "Grace is not the only thing that is true")]
    [InlineData("John 3", "", "John 3")]
    [InlineData(null, "", "Logos note")]
    public void Titles_are_the_anchor_and_first_words(string? anchor, string body, string title) =>
        Assert.Equal(title, LogosImport.Title(anchor, $"<Paragraph><Run Text=\"{body}\"/></Paragraph>"));

    [Fact]
    public void Titles_keep_paragraphs_apart() =>
        Assert.Equal("That’s us! He’s talking about us!", LogosImport.Title(null,
            "<Paragraph><Run Text=\"That’s us!\"/></Paragraph><Paragraph/><Paragraph><Run Text=\"He’s talking about us!\"/></Paragraph>"));

    [Theory]
    [InlineData("Gideon & Baal", "Gideon and Baal")]
    [InlineData("What's In A Name", "Whats In A Name")]
    [InlineData("The Feeding of the 5000", "The Feeding of the 5000")]
    [InlineData("!!!", null)]
    [InlineData(null, null)]
    public void Notebooks_become_valid_tag_names(string? notebook, string? tag) => Assert.Equal(tag, LogosImport.TagFor(notebook));
}

// LogosImport.WriteAsync against the real schema, in an in-memory database.
public sealed class LogosImportWriteTests : IAsyncLifetime
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("Data Source=:memory:");
    private EzraDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new EzraDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<EzraDbContext>()
            .UseSqlite(_connection, s => s.MigrationsHistoryTable("ezra_ef_migrations")).Options);
        await _db.Database.MigrateAsync();
        _db.Strongs.Add(new() { Id = "H6965", Lemma = "קוּם" });
        _db.Tags.Add(new() { Name = "tulip" });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static LogosImport.Planned P(string id, string? notebook, string? tag, params string[] strongs) => new(
        new LogosNotes.Note(id, new DateTime(2026, 1, 24, 13, 35, 49, DateTimeKind.Utc), id == "a" ? new DateTime(2026, 1, 25, 1, 0, 0, DateTimeKind.Utc) : null,
            notebook, "<Paragraph/>", 79, []),
        $"Hebrews 1:1 — note {id}", "The **letter** opens", "Hebrews 1:1", LogosAlignment.Confidence.High,
        [new ScriptureRef(58, 1, 1, 1)], [.. strongs], tag, []);

    [Fact]
    public async Task Writes_public_notes_with_their_logos_dates_refs_tags_and_strongs()
    {
        var (notes, newTags) = await LogosImport.WriteAsync(_db, [P("a", "TULIP", "TULIP", "H6965"), P("b", "Gideon & Baal", "Gideon and Baal"), P("c", null, null)]);
        Assert.Equal((3, 1), (notes, newTags));

        _db.ChangeTracker.Clear();
        var saved = await _db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs).OrderBy(n => n.LogosId).ToListAsync();
        var a = saved[0];
        Assert.Equal(("a", "Hebrews 1:1 — note a", "hebrews-1-1-note-a", "The **letter** opens", true), (a.LogosId, a.Title, a.Slug, a.BodyMd, a.IsPublic));
        Assert.Equal(new DateTime(2026, 1, 24, 13, 35, 49), a.CreatedAt);
        Assert.Equal(new DateTime(2026, 1, 25, 1, 0, 0), a.UpdatedAt);
        Assert.Equal(a.CreatedAt, saved[1].UpdatedAt);
        Assert.Equal(["tulip"], a.Tags.Select(t => t.Name));
        Assert.Equal(["H6965"], a.Strongs.Select(s => s.Id));
        Assert.Equal([(58, 1)], a.Refs.Select(r => (r.Book, r.Chapter!.Value)));
        Assert.Equal(["Gideon and Baal"], saved[1].Tags.Select(t => t.Name));
        Assert.Empty(saved[2].Tags);
        Assert.Equal(2, await _db.Tags.CountAsync());
    }

    [Fact]
    public async Task A_logos_note_can_only_be_imported_once() =>
        await Assert.ThrowsAsync<Microsoft.EntityFrameworkCore.DbUpdateException>(() => LogosImport.WriteAsync(_db, [P("a", null, null), P("a", null, null)]));
}

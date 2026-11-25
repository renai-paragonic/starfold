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
    public void A_reference_anchor_is_exact_and_not_repeated_in_the_body_refs()
    {
        var p = Plan([N("john", "Son of Man", 64, Ref("bible+nasb95.64.5.27"))]).Single();
        Assert.Equal("John 5:27", p.AnchorLabel);
        Assert.Null(p.Confidence);
        Assert.Equal([new ScriptureRef(43, 5, 27, 27)], p.AnchorRefs);
        Assert.Equal([new ScriptureRef(1, 1, 1, 1)], p.BodyRefs);
        Assert.Equal(2, p.Refs.Count);
    }

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
        Assert.Equal([new ScriptureRef(58, 1, 2, 2)], plan[1].AnchorRefs);
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
}

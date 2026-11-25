using starfold_mobile.Data;
using static starfold_mobile.Data.LogosAlignment;

namespace Starfold.Tests;

// A made-up two-chapter book in which every verse has its own words, laid out
// in a "resource" the way Logos does: a heading line, then the verses.
public class LogosAlignmentTests
{
    private static readonly string[] Letters = ["alpha", "bravo", "charlie", "delta", "echo", "foxtrot", "golf", "hotel", "india"];
    private static readonly List<AsvVerse> Asv = [.. from c in new[] { 1, 2 } from v in Enumerable.Range(1, 6) select new AsvVerse(c, v, VerseText(c, v))];

    private static string VerseText(int c, int v) => string.Join(' ', Letters.Select(l => $"{l}x{c}x{v}".Replace("x", NumberWord(c) + NumberWord(v))));
    private static string NumberWord(int n) => n switch { 1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", _ => "six" };

    // The resource: "Chapter 1" and its verses, then "Chapter 2" and its verses.
    private static readonly string Resource = string.Concat(new[] { 1, 2 }.Select(c =>
        $"Chapter {c} " + string.Concat(Asv.Where(v => v.Chapter == c).Select(v => v.Text + " "))));

    private static Anchored At(string text, string? quote = null, int nth = 0)
    {
        var at = -1;
        for (var k = 0; k <= nth; k++) at = Resource.IndexOf(text, at + 1, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{text} isn't in the resource");
        return new(1000 + at, text.Length, quote ?? text);
    }

    private static Anchored Verse(int c, int v) => At(VerseText(c, v));

    private static string F(Placement? p) => p is null ? "null" : $"{p.Format("Book")} {p.Confidence}";

    [Fact]
    public void Places_headings_long_quotes_and_short_quotes()
    {
        var notes = new List<Anchored>
        {
            At("Chapter 1"),
            Verse(1, 2),
            At(VerseText(1, 3) + " " + VerseText(1, 4)),
            At("golfonefive"),                                  // one word of 1:5
            At("Chapter 2"),
            Verse(2, 1), Verse(2, 2), Verse(2, 5), Verse(2, 6),
        };
        var placed = Place(Asv, notes);
        Assert.Equal(
            ["Book 1 Heading", "Book 1:2 High", "Book 1:3-4 High", "Book 1:5 High", "Book 2 Heading",
             "Book 2:1 High", "Book 2:2 High", "Book 2:5 High", "Book 2:6 High"],
            placed.Select(F));
    }

    [Fact]
    public void Keeps_the_callers_order() =>
        Assert.Equal(["Book 2:2 High", "Book 1:2 High"], Place(Asv, [Verse(2, 2), Verse(1, 2), At("Chapter 1"), At("Chapter 2")]).Take(2).Select(F));

    [Fact]
    public void A_note_without_a_quote_is_placed_by_position_with_low_confidence()
    {
        var mid = Resource.IndexOf(VerseText(2, 3), StringComparison.Ordinal) + 20;
        var placed = Place(Asv, [At("Chapter 1"), At("Chapter 2"), Verse(2, 2), Verse(2, 4), new(1000 + mid, 10, null)]);
        Assert.Equal("Book 2:3 Low", F(placed[4]));
    }

    [Fact]
    public void A_section_heading_starts_where_the_next_note_starts()
    {
        var placed = Place(Asv, [At("Chapter 1"), Verse(1, 1), new(At(VerseText(1, 4)).Offset - 1, 1, "The New Way"), Verse(1, 4), Verse(1, 6)]);
        Assert.Equal("Book 1:4 Heading", F(placed[2]));
    }

    [Fact]
    public void Without_fixed_points_the_wording_alone_places_a_note() =>
        Assert.Equal("Book 2:3 Medium", F(Place(Asv, [At(string.Join(" ", VerseText(2, 3).Split(" ").Take(3)))]).Single()));

    [Fact]
    public void A_note_with_nothing_to_go_on_is_not_placed() =>
        Assert.Null(Place(Asv, [new(5, 3, null)]).Single());

    [Theory]
    [InlineData(3, 0, 3, 0, "Hebrews 3")]
    [InlineData(1, 1, 1, 1, "Hebrews 1:1")]
    [InlineData(1, 1, 1, 2, "Hebrews 1:1-2")]
    [InlineData(2, 17, 3, 2, "Hebrews 2:17-3:2")]
    public void Formats_placements(int c, int v, int c2, int v2, string text) =>
        Assert.Equal(text, new Placement(c, v, c2, v2, Confidence.High).Format("Hebrews"));

    [Fact]
    public void Reads_getbible_json()
    {
        var asv = ReadAsv("""{"books":[{"nr":58,"chapters":[{"chapter":1,"verses":[{"chapter":1,"verse":1,"text":"God, having of old time spoken "}]}]}]}""");
        Assert.Equal(new AsvVerse(1, 1, "God, having of old time spoken"), Assert.Single(asv[58]));
    }
}

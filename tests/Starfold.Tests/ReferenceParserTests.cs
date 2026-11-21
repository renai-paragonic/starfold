using starfold_mobile.Bible;

namespace Starfold.Tests;

public class ReferenceParserTests
{
    private static int B(string name) => BibleBooks.Find(name)!.Number;

    private static List<ScriptureRef> Ok(string input)
    {
        var result = ReferenceParser.Parse(input);
        Assert.Empty(result.Errors);
        return result.Refs;
    }

    private static string Error(string input) => Assert.Single(ReferenceParser.Parse(input).Errors);

    [Fact]
    public void Whole_book() => Assert.Equal([new ScriptureRef(B("Genesis"))], Ok("Genesis"));

    [Fact]
    public void Chapter() => Assert.Equal([new ScriptureRef(B("Romans"), 8)], Ok("Rom 8"));

    [Fact]
    public void Single_verse() => Assert.Equal([new ScriptureRef(B("John"), 3, 16, 16)], Ok("John 3:16"));

    [Theory]
    [InlineData("John 3:16-18")]
    [InlineData("John 3:16–18")]   // en dash
    [InlineData("Jn 3.16 - 18")]   // period separator, spaces
    [InlineData("jn3:16-18")]
    public void Verse_range(string input) =>
        Assert.Equal([new ScriptureRef(B("John"), 3, 16, 18)], Ok(input));

    [Fact]
    public void Numbered_book() => Assert.Equal([new ScriptureRef(B("1 John"), 4, 8, 8)], Ok("1 John 4:8"));

    [Fact]
    public void Chapter_range_becomes_one_row_per_chapter() =>
        Assert.Equal([new ScriptureRef(B("Romans"), 8), new ScriptureRef(B("Romans"), 9)], Ok("Romans 8-9"));

    [Fact]
    public void Cross_chapter_range_splits_at_chapter_ends() =>
        Assert.Equal(
            [new ScriptureRef(B("John"), 3, 16, 36), new ScriptureRef(B("John"), 4), new ScriptureRef(B("John"), 5, 1, 2)],
            Ok("John 3:16-5:2"));

    [Fact]
    public void Chapter_to_verse_range_starts_at_verse_one() =>
        Assert.Equal([new ScriptureRef(B("Romans"), 8, 1, 5)], Ok("Rom 8-8:5"));

    [Theory]
    [InlineData("Jude 5", 5, 5)]
    [InlineData("Jude 3-5", 3, 5)]
    [InlineData("Jude 1:5", 5, 5)]
    public void One_chapter_books_read_bare_numbers_as_verses(string input, int start, int end) =>
        Assert.Equal([new ScriptureRef(B("Jude"), 1, start, end)], Ok(input));

    [Fact]
    public void Several_items_and_duplicates()
    {
        var refs = Ok(" John 3:16 ; Rom 8;; Genesis; jn 3:16 ");
        Assert.Equal([new ScriptureRef(B("John"), 3, 16, 16), new ScriptureRef(B("Romans"), 8), new ScriptureRef(B("Genesis"))], refs);
    }

    [Fact]
    public void Comma_continues_the_chapter() =>
        Assert.Equal([new ScriptureRef(B("John"), 3, 16, 16), new ScriptureRef(B("John"), 3, 18, 18)], Ok("John 3:16, 18"));

    [Fact]
    public void Comma_after_a_range() =>
        Assert.Equal([new ScriptureRef(B("John"), 3, 16, 18), new ScriptureRef(B("John"), 3, 21, 21)], Ok("John 3:16-18, 21"));

    [Fact]
    public void Comma_with_a_range() =>
        Assert.Equal([new ScriptureRef(B("John"), 3, 1, 1), new ScriptureRef(B("John"), 3, 16, 18)], Ok("John 3:1,16-18"));

    [Fact]
    public void Comma_with_a_new_chapter() =>
        Assert.Equal([new ScriptureRef(B("Romans"), 8, 28, 28), new ScriptureRef(B("Romans"), 9, 1, 1), new ScriptureRef(B("Romans"), 9, 5, 5)],
            Ok("Rom 8:28, 9:1, 5"));

    [Fact]
    public void Comma_between_chapters() =>
        Assert.Equal([new ScriptureRef(B("Romans"), 8), new ScriptureRef(B("Romans"), 9), new ScriptureRef(B("Romans"), 11), new ScriptureRef(B("Romans"), 12)],
            Ok("Rom 8, 9, 11-12"));

    [Fact]
    public void Comma_after_a_cross_chapter_range_continues_the_last_chapter() =>
        Assert.Contains(new ScriptureRef(B("John"), 4, 5, 5), Ok("John 3:16-4:2, 5"));

    [Fact]
    public void Comma_in_a_one_chapter_book() =>
        Assert.Equal([new ScriptureRef(B("Jude"), 1, 5, 5), new ScriptureRef(B("Jude"), 1, 7, 7)], Ok("Jude 5, 7"));

    [Fact]
    public void Comma_after_a_whole_book_reads_a_chapter() =>
        Assert.Equal([new ScriptureRef(B("Genesis")), new ScriptureRef(B("Genesis"), 3)], Ok("Genesis, 3"));

    [Theory]
    [InlineData("John 3:16, Rom 8")]
    [InlineData("John 3:16, 1 Jn 4:8")]  // a numbered book isn't mistaken for a verse
    public void Comma_piece_with_a_book_stands_alone(string input) => Assert.Equal(2, Ok(input).Count);

    [Fact]
    public void Comma_errors_name_the_whole_item() =>
        Assert.Equal("\"John 3:16, 37\": John 3 has 36 verses.", Error("John 3:16, 37"));

    [Fact]
    public void Commas_and_semicolons_together() =>
        Assert.Equal(3, Ok("John 3:16, 18; Rom 8").Count);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Empty_input(string? input) => Assert.Empty(Ok(input!));

    [Theory]
    [InlineData("3 John 1:15")]      // the LSB has 3 John 15 (the KJV ends at 14)
    [InlineData("John 5:4")]         // bracketed in the LSB, but numbered
    [InlineData("Acts 8:37")]
    [InlineData("Mark 16:9-20")]
    [InlineData("Psalm 119:176")]
    public void Lsb_verses_that_exist(string input) => Assert.Single(Ok(input));

    [Theory]
    [InlineData("Romans 17", "Romans has 16 chapters.")]
    [InlineData("Jude 2:1", "Jude has 1 chapter.")]
    [InlineData("John 3:37", "John 3 has 36 verses.")]
    [InlineData("Revelation 12:18", "Revelation 12 has 17 verses.")]
    [InlineData("Jude 26", "Jude 1 has 25 verses.")]
    [InlineData("John 3:0", "John 3 has 36 verses.")]
    [InlineData("John 3:18-16", "the range ends before it starts.")]
    [InlineData("Rom 9-8", "the range ends before it starts.")]
    [InlineData("John 9999", "couldn't read this reference.")]
    public void Out_of_range(string input, string message) =>
        Assert.Equal($"\"{input}\": {message}", Error(input));

    [Theory]
    [InlineData("Hezekiah 3")]
    [InlineData("Jo 3:16")]
    [InlineData("3:16")]
    public void Unknown_books(string input) => Assert.Contains("unknown book", Error(input));

    [Fact]
    public void Errors_dont_hide_the_valid_items()
    {
        var result = ReferenceParser.Parse("John 3:16; Rom 99; Genesis");
        Assert.Equal(2, result.Refs.Count);
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("Genesis")]
    [InlineData("Romans 8")]
    [InlineData("John 3:16")]
    [InlineData("John 3:16-18")]
    [InlineData("Song of Solomon 2:1")]
    [InlineData("1 John 4:8")]
    public void Format_round_trips(string text) => Assert.Equal(text, Assert.Single(Ok(text)).Format());

    [Fact]
    public void Links()
    {
        var r = Assert.Single(Ok("1 John 4:8"));
        Assert.Equal("https://www.biblegateway.com/passage/?search=1%20John%204%3A8&version=LSB", r.GatewayUrl);
        Assert.Equal("/ezra/bible/1-john/4/8", r.BrowsePath);
    }
}

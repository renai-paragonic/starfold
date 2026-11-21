using starfold_mobile.Bible;

namespace Starfold.Tests;

public class BibleBooksTests
{
    [Fact]
    public void Totals_match_the_lsb()
    {
        Assert.Equal(66, BibleBooks.All.Count);
        Assert.Equal(1189, BibleBooks.All.Sum(b => b.Chapters));
        Assert.Equal(31103, BibleBooks.All.Sum(b => b.Verses.Sum()));
        Assert.Equal(Enumerable.Range(1, 66), BibleBooks.All.Select(b => b.Number));
    }

    // Spots where English Bibles differ, checked against the LSB on Bible Gateway.
    [Theory]
    [InlineData("3 John", 1, 15)]
    [InlineData("Revelation", 12, 17)]
    [InlineData("Revelation", 13, 18)]
    [InlineData("2 Corinthians", 13, 14)]
    [InlineData("Malachi", 4, 6)]
    [InlineData("Joel", 2, 32)]
    [InlineData("Psalms", 3, 8)]
    [InlineData("Psalms", 119, 176)]
    [InlineData("Acts", 8, 40)]
    [InlineData("John", 5, 47)]
    public void Verse_counts(string book, int chapter, int verses) =>
        Assert.Equal(verses, BibleBooks.Find(book)!.VersesIn(chapter));

    [Theory]
    [InlineData("Genesis", "Genesis")]
    [InlineData("gen", "Genesis")]
    [InlineData("Gen.", "Genesis")]
    [InlineData("Gn", "Genesis")]
    [InlineData("1 Cor", "1 Corinthians")]
    [InlineData("1Cor", "1 Corinthians")]
    [InlineData("I Corinthians", "1 Corinthians")]
    [InlineData("First Corinthians", "1 Corinthians")]
    [InlineData("2nd Kings", "2 Kings")]
    [InlineData("III John", "3 John")]
    [InlineData("1 Jn", "1 John")]
    [InlineData("Song of Songs", "Song of Solomon")]
    [InlineData("song-of-solomon", "Song of Solomon")]
    [InlineData("Psalm", "Psalms")]
    [InlineData("Ps", "Psalms")]
    [InlineData("Phil", "Philippians")]
    [InlineData("Phlm", "Philemon")]
    [InlineData("Jn", "John")]
    [InlineData("Mk", "Mark")]
    [InlineData("Isa", "Isaiah")]
    [InlineData("Rev", "Revelation")]
    [InlineData("Revelations", "Revelation")]
    public void Find_accepts_names_aliases_and_prefixes(string input, string expected) =>
        Assert.Equal(expected, BibleBooks.Find(input)?.Name);

    [Theory]
    [InlineData("Jo")]      // Joshua, Job, Joel, John, Jonah
    [InlineData("Jud")]     // Judges, Jude
    [InlineData("Ma")]      // Malachi, Matthew, Mark
    [InlineData("J")]       // too short
    [InlineData("Tobit")]   // not in the canon
    [InlineData("")]
    public void Find_rejects_unknown_or_ambiguous(string input) =>
        Assert.Null(BibleBooks.Find(input));

    [Fact]
    public void Slugs_round_trip()
    {
        foreach (var book in BibleBooks.All)
            Assert.Same(book, BibleBooks.Find(book.Slug));
    }
}

using starfold_mobile.Bible;
using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class StrongsWordsTests
{
    // Transliterations as the Open Scriptures files spell them.
    private static readonly StrongsWords Words = new(new (string Id, string Translit)[]
    {
        ("G25", "agapáō"), ("G26", "agápē"), ("G27", "agapētós"),
        ("G2316", "theós"), ("G2318", "theosebḗs"), ("G5485", "cháris"),
        ("H410", "ʼêl"), ("H430", "ʼĕlôhîym"), ("H2617", "chêçêd"), ("H2618", "Cheçed"),
        ("H3068", "Yᵉhôvâh"), ("H3070", "Yᵉhôvâh yirʼeh"), ("H7965", "shâlôwm"), ("H5512", "Çîyn"),
    }.Select(e => new StrongsEntry { Id = e.Id, Lemma = "x", Transliteration = e.Translit }));

    private static List<string> Ids(string word) => [.. Words.Match(word).Select(e => e.Id)];

    [Theory]
    [InlineData("agápē", "agape")]
    [InlineData("ʼĕlôhîym", "elohim")]
    [InlineData("shâlôwm", "shalom")]
    [InlineData("chêçêd", "chesed")]
    [InlineData("Yᵉhôvâh yirʼeh", "yehovahyireh")]
    [InlineData("praÿs", "prays")]
    public void Folds_transliterations(string translit, string folded) =>
        Assert.Equal(folded, StrongsWords.Fold(translit));

    [Theory]
    [InlineData("agape", "G26")]
    [InlineData("Agápē", "G26")]
    [InlineData("elohim", "H430")]
    [InlineData("elohiym", "H430")]
    [InlineData("shalom", "H7965")]
    public void Finds_by_transliteration(string word, string id) => Assert.Equal(id, Ids(word)[0]);

    [Fact]
    public void Exact_matches_come_before_longer_ones() =>
        Assert.Equal(["G26", "G27"], Ids("agape"));

    [Fact]
    public void Prefixes_match_from_four_letters_shortest_first()
    {
        Assert.Equal(["G26", "G25", "G27"], Ids("agap"));
        Assert.Equal(["H3068", "H3070"], Ids("yehovah"));
        Assert.Empty(Ids("aga"));
    }

    [Fact]
    public void Equal_matches_go_by_number() =>
        Assert.Equal(["H2617", "H2618"], Ids("chesed"));

    [Theory]
    [InlineData("el")]  // H410, but two-letter words are too noisy
    [InlineData("the")] // no exact match, and too short for a prefix
    [InlineData("g26")]
    [InlineData("3:16")]
    [InlineData("")]
    public void Nothing_for_short_words_or_numbers(string word) => Assert.Empty(Ids(word));

    [Fact]
    public void Caps_the_matches()
    {
        var many = new StrongsWords(Enumerable.Range(1, 9).Select(i => new StrongsEntry { Id = $"G{i}", Lemma = "x", Transliteration = "logos" + i }));
        Assert.Equal(StrongsWords.Max, many.Match("logos").Count);
    }

    [Fact]
    public void Skips_entries_without_a_transliteration() =>
        Assert.Equal(0, new StrongsWords([new StrongsEntry { Id = "G1", Lemma = "x" }]).Count);
}

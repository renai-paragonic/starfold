using starfold_mobile.Data;

namespace Starfold.Tests;

public class LogosWordsTests
{
    private static readonly LogosWords Words = new([("H6966", "קוּם"), ("H6965", "קוּם"), ("G1401", "δοῦλος"), ("H2199", "זָעַק"), ("G26", "ἀγάπη")]);

    [Fact]
    public void Matches_an_unpointed_hebrew_lemma_and_takes_the_lowest_number()
    {
        var m = Words.Find("lemma.lbs.he.קום");
        Assert.Equal("H6965", m.Id);
        Assert.Equal(["H6965", "H6966"], m.Candidates);
        Assert.Null(m.Problem);
    }

    [Theory]
    [InlineData("root.lbs.el.δουλος", "G1401")]
    [InlineData("root.lbs.he.זעק", "H2199")]
    [InlineData("lemma.lbs.el.αγαπη", "G26")]
    public void Matches_roots_and_greek_without_accents(string raw, string id) => Assert.Equal(id, Words.Find(raw).Id);

    [Fact]
    public void Keeps_hebrew_and_greek_apart() => Assert.Null(Words.Find("lemma.lbs.el.קום").Id);

    [Theory]
    [InlineData("lemma.lbs.he.שלום", "no Strong's entry for שלום.")]
    [InlineData("lemma.lbs.la.amor", "the word anchor lemma.lbs.la.amor isn't Hebrew or Greek.")]
    [InlineData("lemma.he", "couldn't read the word anchor lemma.he.")]
    public void Reports_what_it_cannot_match(string raw, string problem)
    {
        var m = Words.Find(raw);
        Assert.Null(m.Id);
        Assert.Equal(problem, m.Problem);
    }

    [Theory]
    [InlineData("δοῦλος", "δουλοσ")]
    [InlineData("ἀγάπη", "αγαπη")]
    [InlineData("קוּם", "קום")]
    [InlineData("בְּרֵאשִׁית", "בראשית")]
    public void Strips_points_and_accents(string word, string bare) => Assert.Equal(bare, LogosWords.Bare(word));
}

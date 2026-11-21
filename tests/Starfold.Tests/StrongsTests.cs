using starfold_mobile.Bible;

namespace Starfold.Tests;

public class StrongsTests
{
    [Theory]
    [InlineData("G26", "G26")]
    [InlineData("g26", "G26")]
    [InlineData("G0026", "G26")]
    [InlineData(" H430 ", "H430")]
    [InlineData("h08674", "H8674")]
    public void Normalizes(string input, string expected) => Assert.Equal(expected, StrongsParser.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("G0")]
    [InlineData("G 26")]
    [InlineData("A26")]
    [InlineData("G12345")]
    [InlineData("Gen 1")]
    [InlineData("Ga 2")]
    public void Rejects(string input) => Assert.Null(StrongsParser.Normalize(input));

    [Fact]
    public void Extract_leaves_scripture_for_the_reference_parser()
    {
        var (ids, rest) = StrongsParser.Extract("John 3:16-18, 21; G26; Rom 8, h430; g0026");
        Assert.Equal(["G26", "H430"], ids);
        var refs = ReferenceParser.Parse(rest);
        Assert.Empty(refs.Errors);
        Assert.Equal(3, refs.Refs.Count); // John 3:16-18, John 3:21, Romans 8
    }

    [Fact]
    public void Extract_with_only_strongs_leaves_nothing() =>
        Assert.Empty(ReferenceParser.Parse(StrongsParser.Extract("G26, H430").Scripture).Refs);

    [Fact]
    public void Sorts_greek_first_then_by_number() =>
        Assert.Equal(["G3", "G26", "H1", "H430"], StrongsParser.Sort(["H430", "G26", "H1", "G3"]));

    [Fact]
    public void Blb_url_uses_lsb() =>
        Assert.Equal("https://www.blueletterbible.org/lexicon/h430/lsb/", StrongsParser.BlbUrl("H430"));

    [Fact]
    public void Mentions_drop_zero_padding() =>
        Assert.Equal(["G1537", "H8012"], StrongsParser.Mentions("from G1537 (ἐκ); of Hebrew origin (H08012);"));

    [Fact]
    public void Links_only_known_mentions_and_encodes_text() =>
        Assert.Equal("from <a href=\"/ezra/strongs/G1537\">G1537</a> &lt;b&gt; and G9999",
            StrongsParser.LinkMentions("from G1537 <b> and G9999", new HashSet<string> { "G1537" }));

    private const string Greek = """
        /**
         * Copyright 2009, Open Scriptures. CC-BY-SA. Derived from XML. G1 = not a key
         */

        var strongsGreekDictionary = {"G26":{"strongs_def":" love, i.e. affection","derivation":"from G25 (ἀγαπάω);","translit":"agápē","lemma":"ἀγάπη","kjv_def":"(feast of) charity"},
        "G4533":{"lemma":"Σαλμών","translit":"Salmṓn","derivation":"of Hebrew origin (H08012);"}}; module.exports = strongsGreekDictionary;
        """;

    private const string Hebrew = """
        var strongsHebrewDictionary = {"H1":{"lemma":"אָב","xlit":"ʼâb","pron":"awb","derivation":"a primitive word;","strongs_def":"father","kjv_def":"chief, father."}
        };

        module.exports = strongsHebrewDictionary;
        """;

    [Fact]
    public void Reads_greek_file()
    {
        var entries = StrongsLexicon.Read(Greek);
        Assert.Equal(2, entries.Count);
        var e = entries.Single(e => e.Id == "G26");
        Assert.Equal("ἀγάπη", e.Lemma);
        Assert.Equal("agápē", e.Transliteration);
        Assert.Equal("love, i.e. affection", e.Definition); // trimmed
        Assert.Equal("from G25 (ἀγαπάω);", e.Derivation);
        Assert.Equal("(feast of) charity", e.KjvDef);
        Assert.Null(entries.Single(e => e.Id == "G4533").Definition);
    }

    [Fact]
    public void Reads_hebrew_file_with_xlit()
    {
        var e = Assert.Single(StrongsLexicon.Read(Hebrew));
        Assert.Equal("H1", e.Id);
        Assert.Equal("אָב", e.Lemma);
        Assert.Equal("ʼâb", e.Transliteration);
    }

    [Fact]
    public void Rejects_unexpected_file() => Assert.Throws<FormatException>(() => StrongsLexicon.Read("not a lexicon"));
}

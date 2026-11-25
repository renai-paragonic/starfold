using starfold_mobile.Bible;
using starfold_mobile.Data;
using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class LogosRichTextTests
{
    private static string Md(string richText) => LogosRichText.Convert(richText).Markdown;
    private static string P(params string[] runs) => $"<Paragraph>{string.Concat(runs)}</Paragraph>";
    private static string Run(string text, string attrs = "") => $"<Run {attrs} Text=\"{text}\"/>";
    private static string Ref(string logos, string shown) => $"<Reference Reference=\"{logos}\" IsLink=\"True\">{Run(shown)}</Reference>";

    [Fact]
    public void Paragraphs_become_blocks_and_empty_ones_go() =>
        Assert.Equal("One\n\nTwo", Md(P(Run("One")) + "<Paragraph FontSize=\"11\"/>" + P(Run(" ")) + P(Run("Two"))));

    [Theory]
    [InlineData("FontBold=\"True\"", "**packed**")]
    [InlineData("FontItalic=\"True\"", "*packed*")]
    [InlineData("HasUnderline=\"True\"", "++packed++")]
    [InlineData("FontBold=\"True\" FontItalic=\"True\"", "***packed***")]
    public void Formats_runs(string attrs, string expected) => Assert.Equal($"An {expected} start", Md(P(Run("An "), Run("packed", attrs), Run(" start"))));

    [Fact]
    public void Neighbours_with_the_same_format_share_markers() =>
        Assert.Equal("**ab**", Md(P(Run("a", "FontBold=\"True\""), Run("b", "FontBold=\"True\""))));

    [Fact]
    public void Spaces_and_punctuation_move_outside_the_markers() =>
        Assert.Equal("the **absolute kicker**, in", Md(P(Run("the "), Run("absolute kicker, ", "FontBold=\"True\""), Run("in"))));

    [Fact]
    public void Formatting_on_only_punctuation_is_dropped() =>
        Assert.Equal("being **Imago Dei**, to", Md(P(Run("being "), Run("Imago Dei", "FontBold=\"True\""), Run(",", "FontBold=\"True\" FontItalic=\"True\""), Run(" to"))));

    [Fact]
    public void Shared_formatting_nests()
    {
        var md = Md(P(Run("defined by the Bible", "FontBold=\"True\" FontItalic=\"True\""), Run(", not the other way around!", "FontItalic=\"True\"")));
        Assert.Equal("***defined by the Bible**, not the other way around*!", md);
        Assert.Equal("<p><em><strong>defined by the Bible</strong>, not the other way around</em>!</p>\n", NoteMarkdown.ToHtml(md));
    }

    [Fact]
    public void Emphasis_next_to_a_footnote_number_still_renders() =>
        Assert.Equal("<p><em>out of death</em>.1</p>\n", NoteMarkdown.ToHtml(Md(P(Run("out of death.", "FontItalic=\"True\""), Run("1")))));

    [Theory]
    [InlineData("2 * 3 = 6", @"2 \* 3 = 6")]
    [InlineData("a [note] here", @"a \[note\] here")]
    [InlineData("x ++ y == z ~ q ^", @"x \+\+ y \=\= z \~ q \^")]
    [InlineData("&amp;copy; and &amp; stay", @"\&copy; and & stay")]
    [InlineData("#1 point", @"\#1 point")]
    [InlineData("&lt;b&gt; | c_d", @"\<b\> \| c\_d")]
    public void Escapes_markdown_syntax(string text, string expected) => Assert.Equal(expected, Md(P(Run(text))));

    [Fact]
    public void A_typed_dash_stays_a_bullet() => Assert.Equal("- Faith is fruit", Md(P(Run("- Faith is fruit"))));

    [Fact]
    public void Leading_tabs_go_so_it_is_not_code() => Assert.Equal("Point 1: We hear", Md(P(Run("&#9;Point 1: We hear"))));

    [Fact]
    public void A_line_break_in_a_run_is_a_hard_break() =>
        Assert.Equal("one\\\ntwo", Md(P(Run("one&#10;two&#10;"))));

    [Fact]
    public void Lists_nest_and_skip_empty_items()
    {
        var rich = "<List Kind=\"Disc\"><ListItem>" + P(Run("masters")) +
            "<List Kind=\"Disc\"><ListItem>" + P(Run("nails")) + "</ListItem></List></ListItem>" +
            "<ListItem><Paragraph/></ListItem><ListItem>" + P(Run("Shepherd")) + "</ListItem></List>";
        Assert.Equal("Intro\n\n- masters\n  - nails\n- Shepherd", Md(P(Run("Intro")) + rich));
    }

    [Fact]
    public void References_link_to_bible_gateway_and_become_refs()
    {
        var r = LogosRichText.Convert(P(Run("see "), Ref("bible+nasb95.1.12.1-1.12.3", "Genesis 12:1-3")));
        Assert.Equal("see [Genesis 12:1-3](https://www.biblegateway.com/passage/?search=Genesis%2012%3A1-3&version=LSB)", r.Markdown);
        Assert.Equal([new ScriptureRef(1, 12, 1, 3)], r.Refs);
        Assert.Empty(r.Problems);
    }

    [Fact]
    public void A_range_across_chapters_splits()
    {
        var r = LogosRichText.Convert(P(Ref("bible+esv.66.5.20-66.6.3", "Romans 5:20-6:3")));
        Assert.Equal([new ScriptureRef(45, 5, 20, 21), new ScriptureRef(45, 6, 1, 3)], r.Refs);
        Assert.Contains("search=Romans%205%3A20-6%3A3", r.Markdown);
    }

    [Fact]
    public void The_same_passage_twice_is_one_ref() =>
        Assert.Single(LogosRichText.Convert(P(Ref("bible.66.9", "Romans 9"), Run(" and "), Ref("bible+nasb95.66.9", "Rom 9"))).Refs);

    [Fact]
    public void A_bad_reference_stays_as_text_and_is_reported()
    {
        var r = LogosRichText.Convert(P(Ref("bible+nasb95.66.17.1", "Romans 17:1"), Ref("bible+nrsv.41.1.1", "Tobit 1:1")));
        Assert.Equal("Romans 17:1Tobit 1:1", r.Markdown);
        Assert.Empty(r.Refs);
        Assert.Equal(["\"Romans 17:1\": Romans has 16 chapters.", "couldn't read the reference bible+nrsv.41.1.1."], r.Problems);
    }

    [Fact]
    public void Web_links_keep_their_text()
    {
        var md = Md(P($"<UriLink Uri=\"https://www.twitch.tv/fedorablewill\">{Run("Will's stream")}</UriLink>"));
        Assert.Equal("[Will's stream](<https://www.twitch.tv/fedorablewill>)", md);
        Assert.Contains("href=\"https://www.twitch.tv/fedorablewill\"", NoteMarkdown.ToHtml(md));
    }

    [Fact]
    public void Images_are_dropped_and_reported()
    {
        var r = LogosRichText.Convert(P(Run("Look:"), "<UriMedia Uri=\"https://files.logoscdn.com/x.png\" Width=\"10\" Height=\"10\"/>"));
        Assert.Equal("Look:", r.Markdown);
        Assert.Equal(["dropped an image."], r.Problems);
    }

    [Fact]
    public void Unreadable_markup_is_reported() =>
        Assert.StartsWith("couldn't read the note's markup", Assert.Single(LogosRichText.Convert("<Paragraph>").Problems));

    [Theory]
    [InlineData("bible+nasb95.79.1.1", "Hebrews 1:1")]
    [InlineData("bible+nasb95.1.12.1-1.12.3", "Genesis 12:1-3")]
    [InlineData("bible+esv.66.5.20-66.6.3", "Romans 5:20-6:3")]
    [InlineData("bible.66.9", "Romans 9")]
    [InlineData("bible.66.9-66.11", "Romans 9-11")]
    [InlineData("bible+nasb95.66", "Romans")]
    [InlineData("bible+nasb95.64.5.27-64.5.27", "John 5:27")]
    public void Reads_logos_references(string logos, string text) => Assert.Equal(text, LogosRichText.ReferenceText(logos));

    [Theory]
    [InlineData("bible+nasb95.45.1.1")]
    [InlineData("bible.66.1.1-67.1.1")]
    [InlineData("bible.66.1.1-66.2")]
    [InlineData("lemma.lbs.he.קום")]
    public void Refuses_logos_references_it_cannot_read(string logos) => Assert.Null(LogosRichText.ReferenceText(logos));
}

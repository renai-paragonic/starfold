using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class NoteSearchTests
{
    [Fact]
    public void One_pattern_per_word() =>
        Assert.Equal(["%love%", "%feast%"], NoteSearch.Patterns("  love   feast "));

    [Fact]
    public void Repeated_words_count_once() =>
        Assert.Equal(["%Love%"], NoteSearch.Patterns("Love love LOVE"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_query_has_no_patterns(string? q) => Assert.Empty(NoteSearch.Patterns(q));

    [Fact]
    public void Wildcards_are_escaped() =>
        Assert.Equal([@"%100\%%", @"%a\_b%", @"%c\\d%"], NoteSearch.Patterns(@"100% a_b c\d"));

    private static readonly string[] Tags = ["Sermons", "Sermon on the Mount", "Greek", "Grace notes", "ἀγάπη"];

    private static List<NoteSearch.Suggestion> Suggest(string text, StrongsEntry? strongs = null) =>
        NoteSearch.Suggest(text, Tags, strongs is null ? [] : [strongs]);

    [Fact]
    public void Suggests_scripture_from_the_end_of_the_text()
    {
        var s = Assert.Single(Suggest("love john 3:16"));
        Assert.Equal(new NoteSearch.Suggestion("ref", "John 3:16", "John 3:16", 2), s);
    }

    [Fact]
    public void Suggests_multi_word_books() =>
        Assert.Equal(new NoteSearch.Suggestion("ref", "1 John 4:8", "1 John 4:8", 3), Suggest("grace 1 jn 4:8")[0]);

    [Fact]
    public void Suggestions_stop_at_the_last_semicolon() =>
        Assert.Equal(new NoteSearch.Suggestion("ref", "Romans 8", "Romans 8", 2), Assert.Single(Suggest("John 3:16; Rom 8")));

    [Fact]
    public void Suggests_each_ref_of_a_comma_list() =>
        Assert.Equal(["Romans 8", "Romans 9"], Suggest("Rom 8, 9").Select(s => s.Value));

    [Theory]
    [InlineData("this is")]   // "is" would be Isaiah
    [InlineData("am")]        // Amos
    [InlineData("love")]
    [InlineData("3:16")]
    public void No_scripture_for_short_or_unreadable_text(string text) =>
        Assert.DoesNotContain(Suggest(text), s => s.Kind == "ref");

    [Fact]
    public void Suggests_a_bare_book_from_three_letters() =>
        Assert.Contains(Suggest("Job"), s => s.Kind == "ref" && s.Value == "Job");

    [Fact]
    public void Suggests_the_strongs_entry_it_is_given()
    {
        var s = Suggest("love g26", new StrongsEntry { Id = "G26", Lemma = "ἀγάπη" });
        Assert.Contains(new NoteSearch.Suggestion("strongs", "G26", "G26 ἀγάπη", 1), s);
    }

    [Fact]
    public void Suggests_every_strongs_entry_it_is_given() =>
        Assert.Equal(["G26", "G27"], NoteSearch.Suggest("agape", [], [
            new StrongsEntry { Id = "G26", Lemma = "ἀγάπη" }, new StrongsEntry { Id = "G27", Lemma = "ἀγαπητός" }]).Select(s => s.Value));

    [Fact]
    public void Suggests_tags_containing_the_last_words_prefix_matches_first()
    {
        Assert.Equal(["Sermon on the Mount", "Sermons"], Suggest("serm").Select(s => s.Value));
        // "on the mount" covers three words, so all three are replaced.
        Assert.Equal(new NoteSearch.Suggestion("tag", "Sermon on the Mount", "Sermon on the Mount", 3), Assert.Single(Suggest("study on the mount")));
    }

    [Fact]
    public void Tag_matching_ignores_case_beyond_ascii() =>
        Assert.Contains(Suggest("ἈΓΆΠΗ"), s => s.Value == "ἀγάπη");

    [Fact]
    public void Nothing_for_empty_text() => Assert.Empty(Suggest("   "));

    [Fact]
    public void Caps_the_word_count() =>
        Assert.Equal(NoteSearch.MaxWords, NoteSearch.Patterns(string.Join(" ", Enumerable.Range(1, 20))).Count);
}

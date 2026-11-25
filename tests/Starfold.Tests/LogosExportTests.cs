using starfold_mobile.Data;

namespace Starfold.Tests;

public class LogosExportTests
{
    private const string Divider = "<div style=\"margin-top:4pt; margin-bottom:4pt; border-bottom:1px solid;\">\n\t</div>";

    private static string Note(string? quote, string body) =>
        (quote is null ? "" : $"<div style=\"margin-left:8pt;\">\n<p lang=\"en-US\"><span style=\"font-variant:small-caps;\">You</span> are My Son, {quote}</p>\n</div>") +
        $"<p style=\"margin:0pt;\"><span>{body}</span></p>\n" + Divider;

    private static readonly string Html =
        "<html><body><h1 style=\"border-bottom:1px solid;\"><span>Notes</span></h1>" +
        Note("Today I have begotten You&rdquo;", "If Christ has an inheritance &amp; more") +
        Note(null, "No anchor here.") +
        Note("&bull;a Father", "") +
        "<br/><div style='mso-element:footer'><p>Exported from <a href=\"https://www.logos.com/\">Logos Bible Study</a></p></div></body></html>";

    [Fact]
    public void Reads_quotes_and_body_keys()
    {
        var notes = LogosExport.Read(Html);
        Assert.Equal("You are My Son, Today I have begotten You”", notes[0].Quote);
        Assert.Equal("ifchristhasaninheritancemore", notes[0].BodyKey);
        Assert.Null(notes[1].Quote);
        Assert.Equal("noanchorhere", notes[1].BodyKey);
    }

    [Fact]
    public void Skips_notes_with_no_body() =>
        Assert.DoesNotContain(LogosExport.Read(Html), n => n.Quote?.Contains("Father") == true);

    [Fact]
    public void Body_text_from_the_database_has_the_same_key() =>
        Assert.Equal("ifchristhasaninheritancemore", LogosExport.Key(LogosExport.BodyText(
            """<Paragraph><Run Text="If Christ has an "/><Run FontBold="True" Text="inheritance"/><Run Text=" &amp; more"/></Paragraph>""")));

    [Fact]
    public void Keys_keep_hebrew_and_greek_letters_without_points() => Assert.Equal("קוםδοῦλος1", LogosExport.Key("קוּם, δοῦλος (1)"));

    [Fact]
    public void Pairs_by_body_in_order_and_leaves_misses_null()
    {
        var export = new List<LogosExport.ExportNote> { new("first", "same"), new("other", "different"), new("second", "same") };
        Assert.Equal(["first", "second", null, "other"], LogosExport.Pair(["same", "same", "same", "different"], export));
    }
}

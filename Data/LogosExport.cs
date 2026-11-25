using System.Net;
using System.Text.RegularExpressions;

namespace starfold_mobile.Data;

// Reads the anchored quotes out of a Logos notes export (Notes > Print/Export
// as HTML), which the notes database doesn't have. Each note in the export is
// an optional anchor quote, <div style="margin-left:8pt;">, then its body,
// and it ends with an empty border-bottom divider.
public static partial class LogosExport
{
    public sealed record ExportNote(string? Quote, string BodyKey);

    public static List<ExportNote> Read(string html)
    {
        var start = html.IndexOf("</h1>", StringComparison.Ordinal);
        var notes = new List<ExportNote>();
        foreach (var segment in Divider().Split(start < 0 ? html : html[(start + 5)..]))
        {
            var anchor = Anchor().Match(segment);
            var body = anchor.Success ? segment[(anchor.Index + anchor.Length)..] : segment;
            var key = Key(Text(body));
            if (key == "") continue;
            notes.Add(new(anchor.Success ? Text(anchor.Groups[1].Value) : null, key));
        }
        return notes;
    }

    // What a note's body is matched on: its letters and digits, lower case.
    // The export and the database differ in spacing, quotes and entities.
    public static string Key(string text) => NotKey().Replace(text.ToLowerInvariant(), "");

    // The body text of a note in the database, for Key.
    public static string BodyText(string richText) =>
        string.Concat(TextAttribute().Matches(richText).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)));

    // Matches database notes to export notes by body. Notes with the same
    // body are matched in order, so pass the database notes in the export's
    // order (by resource, then offset). Returns each note's quote, or null.
    public static List<string?> Pair(IEnumerable<string> bodyKeys, IEnumerable<ExportNote> export)
    {
        var byKey = export.GroupBy(e => e.BodyKey).ToDictionary(g => g.Key, g => new Queue<ExportNote>(g));
        return [.. bodyKeys.Select(k => byKey.TryGetValue(k, out var q) && q.Count > 0 ? q.Dequeue().Quote : null)];
    }

    private static string Text(string html) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();

    [GeneratedRegex("""<div style="margin-top:4pt; margin-bottom:4pt; border-bottom:1px solid;">\s*</div>""")]
    private static partial Regex Divider();

    [GeneratedRegex("""<div style="margin-left:8pt;">(.*?)</div>""", RegexOptions.Singleline)]
    private static partial Regex Anchor();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[^\p{L}\p{N}]")]
    private static partial Regex NotKey();

    [GeneratedRegex(@"Text=""([^""]*)""")]
    private static partial Regex TextAttribute();
}

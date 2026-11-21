using System.Net;
using System.Text.RegularExpressions;

namespace starfold_mobile.Bible;

// Strong's numbers: "G26" (Greek) and "H430" (Hebrew). Any casing and
// leading zeros are accepted ("g0026") and stored as "G26".
public static partial class StrongsParser
{
    public static string? Normalize(string? text)
    {
        var m = Number().Match(text ?? "");
        return m.Success ? Id(m) : null;
    }

    // Pulls the Strong's items out of the editor's references field and
    // returns the rest for ReferenceParser. Items are split on ";" and ",",
    // so "John 3:16; G26, H430" -> [G26, H430] and "John 3:16".
    public static (List<string> Ids, string Scripture) Extract(string? input)
    {
        var ids = new List<string>();
        var items = new List<string>();
        foreach (var item in (input ?? "").Split(';'))
        {
            var pieces = new List<string>();
            foreach (var piece in item.Split(','))
            {
                if (Normalize(piece) is string id) ids.Add(id);
                else pieces.Add(piece);
            }
            items.Add(string.Join(",", pieces));
        }
        return ([.. Sort(ids.Distinct())], string.Join(";", items));
    }

    // Greek before Hebrew, then by number.
    public static IEnumerable<string> Sort(IEnumerable<string> ids) =>
        ids.OrderBy(id => id[0]).ThenBy(id => int.Parse(id[1..]));

    public static bool IsHebrew(string id) => id[0] == 'H';

    public static string BlbUrl(string id) => $"https://www.blueletterbible.org/lexicon/{id.ToLowerInvariant()}/lsb/";

    public static string BrowsePath(string id) => "/ezra/strongs/" + id;

    // Every Strong's number mentioned in a lexicon text ("from G1537 (ἐκ)").
    public static IEnumerable<string> Mentions(string? text) =>
        Mention().Matches(text ?? "").Select(Id);

    // HTML-encodes a lexicon text and links the numbers in it that are in
    // `known` (a few mention numbers the lexicon doesn't have).
    public static string LinkMentions(string? text, IReadOnlySet<string> known) =>
        Mention().Replace(WebUtility.HtmlEncode(text ?? ""), m =>
        {
            var id = Id(m);
            return known.Contains(id) ? $"<a href=\"{BrowsePath(id)}\">{id}</a>" : id;
        });

    private static string Id(Match m) => char.ToUpperInvariant(m.Groups["l"].Value[0]) + m.Groups["n"].Value;

    // Up to four digits after the zeros: G1-G5624, H1-H8674.
    [GeneratedRegex(@"^\s*(?<l>[GgHh])0*(?<n>[1-9]\d{0,3})\s*$")]
    private static partial Regex Number();

    [GeneratedRegex(@"\b(?<l>[GH])0*(?<n>[1-9]\d{0,3})\b")]
    private static partial Regex Mention();
}

using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using starfold_mobile.Bible;

namespace starfold_mobile.Data;

// Turns a Logos note body (ContentRichText) into Ezra Markdown, with its
// scripture links pointed at Bible Gateway. They stay links only: a note's
// refs come from its anchor (Renai, 2026-11-24). The markup seen in Renai's notes: Paragraph, Run
// (Text, FontBold, FontItalic, HasUnderline), List/ListItem, Reference,
// UriLink and UriMedia. Font size, family, colour and language are dropped.
public static partial class LogosRichText
{
    public sealed record Result(string Markdown, List<string> Problems);

    public static Result Convert(string richText)
    {
        var problems = new List<string>();
        XElement root;
        try
        {
            root = XElement.Parse($"<Root>{richText}</Root>");
        }
        catch (XmlException e)
        {
            return new("", [$"couldn't read the note's markup: {e.Message}"]);
        }

        var blocks = new List<string>();
        foreach (var el in root.Elements())
        {
            var block = el.Name.LocalName == "List" ? List(el, 0, problems) : Paragraph(el, problems);
            if (block != "") blocks.Add(block);
        }
        return new(string.Join("\n\n", blocks), problems);
    }

    // "bible+nasb95.66.5.20-66.6.3" → "Romans 5:20-6:3", the form Ezra's
    // reference parser reads. Logos numbers books 1–39 and 61–87 (see
    // LogosNotes.EzraBook). Null if it isn't one we can read.
    public static string? ReferenceText(string logos)
    {
        var m = LogosReference().Match(logos);
        if (!m.Success) return null;
        var start = m.Groups["start"].Value.Split('.').Select(int.Parse).ToArray();
        var end = m.Groups["end"].Success ? m.Groups["end"].Value.Split('.').Select(int.Parse).ToArray() : null;
        if (end is not null && (end.Length != start.Length || end[0] != start[0])) return null;
        if (LogosNotes.EzraBook(start[0]) is not int book) return null;

        var name = BibleBooks.Get(book).Name;
        var text = start.Length switch
        {
            1 => name,
            2 => $"{name} {start[1]}",
            _ => $"{name} {start[1]}:{start[2]}",
        };
        if (end is null || end.SequenceEqual(start)) return text;
        return end.Length switch
        {
            2 => $"{text}-{end[1]}",
            _ when end[1] == start[1] => $"{text}-{end[2]}",
            _ => $"{text}-{end[1]}:{end[2]}",
        };
    }

    private static string List(XElement list, int depth, List<string> problems)
    {
        var lines = new List<string>();
        var indent = new string(' ', depth * 2);
        foreach (var item in list.Elements())
        {
            if (item.Name.LocalName != "ListItem") { problems.Add($"skipped a {item.Name.LocalName} inside a list."); continue; }
            // An item's paragraphs share one line (joined by line breaks), and
            // its nested lists follow, indented.
            var text = string.Join("\\\n" + indent + "  ", item.Elements().Where(e => e.Name.LocalName != "List")
                .Select(e => Paragraph(e, problems)).Where(t => t != ""));
            var nested = item.Elements().Where(e => e.Name.LocalName == "List").Select(e => List(e, depth + 1, problems)).Where(t => t != "").ToList();
            if (text == "" && nested.Count == 0) continue;
            lines.Add($"{indent}- {text}".TrimEnd());
            lines.AddRange(nested);
        }
        return string.Join("\n", lines);
    }

    // One block of inline text. Leading whitespace goes (four spaces or a tab
    // would make a code block), and a leading # is escaped so it isn't a
    // heading. A leading "- " or "1. " is left alone: Renai typed those as
    // bullets, and Markdown shows them as one.
    private static string Paragraph(XElement p, List<string> problems)
    {
        if (p.Name.LocalName != "Paragraph") problems.Add($"read a {p.Name.LocalName} as a paragraph.");
        var spans = new List<Span>();
        foreach (var node in p.Elements()) Inline(node, spans, problems);

        // Formatting on only punctuation or spaces is invisible, and Logos
        // often makes a comma bold-italic next to a bold word, which would
        // give **word*****,***. Dropping it keeps the Markdown readable.
        var plain = spans.Select(s => s.Md.Any(char.IsLetterOrDigit) ? s : s with { Fmt = Fmt.None }).ToList();
        var text = TrailingBreaks().Replace(Render(plain).TrimStart(), "").TrimEnd();
        return text.StartsWith('#') ? "\\" + text : text;
    }

    [Flags]
    private enum Fmt { None = 0, Underline = 1, Bold = 2, Italic = 4 }

    // A piece of inline Markdown (already escaped) and its formatting.
    private sealed record Span(string Md, Fmt Fmt);

    private static void Inline(XElement el, List<Span> spans, List<string> problems)
    {
        switch (el.Name.LocalName)
        {
            case "Run":
                spans.Add(new(Escape((string?)el.Attribute("Text") ?? ""), Format(el)));
                break;
            case "Reference":
            {
                var shown = Escape(string.Concat(el.Elements().Select(r => (string?)r.Attribute("Text") ?? ""))).Trim();
                var first = el.Elements().FirstOrDefault();
                var logos = (string?)el.Attribute("Reference") ?? "";
                var text = ReferenceText(logos);
                var parsed = ReferenceParser.Parse(text);
                if (text is null || parsed.Errors.Count > 0 || parsed.Refs.Count == 0)
                {
                    problems.Add(text is null ? $"couldn't read the reference {logos}." : string.Join(" ", parsed.Errors));
                    spans.Add(new(shown, Format(first)));
                    break;
                }
                var url = $"https://www.biblegateway.com/passage/?search={Uri.EscapeDataString(text)}&version=LSB";
                spans.Add(new($"[{(shown == "" ? Escape(text) : shown)}]({url})", Format(first)));
                break;
            }
            case "UriLink":
            {
                var uri = (string?)el.Attribute("Uri") ?? "";
                var shown = Escape(string.Concat(el.Elements().Select(r => (string?)r.Attribute("Text") ?? ""))).Trim();
                var first = el.Elements().FirstOrDefault();
                spans.Add(new($"[{(shown == "" ? Escape(uri) : shown)}](<{uri.Replace("<", "%3C").Replace(">", "%3E")}>)",
                    Format(first)));
                break;
            }
            case "UriMedia":
                problems.Add("dropped an image.");
                break;
            default:
                problems.Add($"read a {el.Name.LocalName} as plain text.");
                spans.Add(new(Escape(el.Value), Fmt.None));
                break;
        }
    }

    private static Fmt Format(XElement? el) =>
        (Flag(el, "HasUnderline") ? Fmt.Underline : 0) | (Flag(el, "FontBold") ? Fmt.Bold : 0) | (Flag(el, "FontItalic") ? Fmt.Italic : 0);

    private static bool Flag(XElement? el, string name) => (string?)el?.Attribute(name) == "True";

    // Nests the formatting, the way HTML would: a format shared by a run of
    // neighbouring spans wraps them all once, and the rest is done inside.
    // Bold-italic "defined by the Bible" then italic ", not the other way"
    // becomes ***defined by the Bible**, not the other way*, and neighbours
    // with the same formatting share one pair of markers.
    private static string Render(List<Span> spans)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < spans.Count;)
        {
            if (spans[i].Fmt == Fmt.None) { sb.Append(spans[i++].Md); continue; }
            // The format of this span that carries on furthest.
            var (fmt, end) = new[] { Fmt.Underline, Fmt.Bold, Fmt.Italic }
                .Where(f => spans[i].Fmt.HasFlag(f))
                .Select(f => (f, End: Enumerable.Range(i, spans.Count - i).TakeWhile(k => spans[k].Fmt.HasFlag(f)).Last() + 1))
                .MaxBy(x => x.End);
            sb.Append(Wrap(Render([.. spans.Skip(i).Take(end - i).Select(s => s with { Fmt = s.Fmt & ~fmt })]), fmt));
            i = end;
        }
        return sb.ToString();
    }

    // Markdown emphasis can't start or end with a space, and can't open or
    // close between punctuation and a letter (**gentleness*, or meekness***
    // or *death.*1 stay literal). So outer whitespace, line breaks and
    // punctuation move outside the markers. Brackets stay, for links.
    private static string Wrap(string md, Fmt fmt)
    {
        var lead = LeadingSpace().Match(md).Value;
        var trail = TrailingSpace().Match(md, lead.Length).Value;
        var core = md[lead.Length..(md.Length - trail.Length)];
        if (core == "") return md;
        var mark = fmt switch { Fmt.Underline => "++", Fmt.Bold => "**", _ => "*" };
        return lead + mark + core + mark + trail;
    }

    // Escapes what Ezra's Markdown would read as syntax, including the
    // emphasis extras (~~, ~, ^, ++, ==). Tabs become spaces, and a line break
    // inside a run becomes a Markdown hard break.
    private static string Escape(string text)
    {
        var s = Special().Replace(text.Replace('\t', ' ').Replace("\r", ""), @"\$0");
        s = Doubled().Replace(s, m => $"\\{m.Value[0]}\\{m.Value[1]}");
        s = Entity().Replace(s, m => "\\" + m.Value);
        return s.Replace("\n", "\\\n");
    }

    [GeneratedRegex(@"[\\*_`\[\]<>~^|]")]
    private static partial Regex Special();

    [GeneratedRegex(@"\+\+|==")]
    private static partial Regex Doubled();

    [GeneratedRegex("&(?=[A-Za-z0-9#]+;)")]
    private static partial Regex Entity();

    [GeneratedRegex(@"(\\\n\s*)+$")]
    private static partial Regex TrailingBreaks();

    [GeneratedRegex(@"^(?:[\s.,;:!?“”‘’""]|\\\n)*")]
    private static partial Regex LeadingSpace();

    [GeneratedRegex(@"(?:[\s.,;:!?“”‘’""]|\\\n)*$")]
    private static partial Regex TrailingSpace();

    [GeneratedRegex(@"^bible(?:\+[a-z0-9]+)?\.(?<start>\d+(?:\.\d+){0,2})(?:-(?<end>\d+(?:\.\d+){0,2}))?$")]
    private static partial Regex LogosReference();
}

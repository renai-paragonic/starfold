using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace starfold_mobile.Models.Ezra;

// Renders note bodies. Raw HTML is escaped, and links and images may only use
// http, https or mailto (relative URLs are fine). The live preview and the
// note page both call this, so they always match.
public static partial class NoteMarkdown
{
    // Emphasis extras: ~~strike~~, ~sub~, ^sup^ (Pandoc) and ++ins++, ==mark== (markdown-it).
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseEmphasisExtras()
        .UsePipeTables()
        .Build();

    public static string ToHtml(string? markdown)
    {
        var doc = Markdown.Parse(markdown ?? "", Pipeline);
        foreach (var link in doc.Descendants<LinkInline>())
            if (!IsSafeUrl(link.Url)) link.Url = "";
        foreach (var autolink in doc.Descendants<AutolinkInline>().ToList())
            if (!IsSafeUrl(autolink.Url)) autolink.ReplaceBy(new LiteralInline(autolink.Url));
        return doc.ToHtml(Pipeline);
    }

    private static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        // Browsers ignore whitespace and control characters inside a scheme.
        var scheme = Scheme().Match(new string(url.Where(c => c > ' ').ToArray()));
        return !scheme.Success || scheme.Groups[1].Value.ToLowerInvariant() is "http" or "https" or "mailto";
    }

    [GeneratedRegex("^([a-zA-Z][a-zA-Z0-9+.-]*):")]
    private static partial Regex Scheme();
}

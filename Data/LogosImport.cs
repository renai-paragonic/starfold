using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Bible;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Data;

// `dotnet run -- import-logos <notestool.db> <export.html>`: brings Renai's
// Logos notes into Ezra. For now it's a dry run that lists what would be
// imported; the rest is built in steps (see the Logos note importer in
// ProjectPlans/ezra.md). Notes with no body are skipped, and so are notes
// already imported (by Logos id). The export supplies the anchored quotes,
// and the ASV (App_Data/asv/asv.json) places text-selection anchors.
public static class LogosImport
{
    // Everything worked out for one note. AnchorLabel is where the note is
    // anchored ("Hebrews 1:1"), and Confidence says how sure the placement
    // is (null when the anchor is exact or there's none).
    public sealed record Planned(
        LogosNotes.Note Note, string Markdown, string? AnchorLabel, LogosAlignment.Confidence? Confidence,
        List<ScriptureRef> AnchorRefs, List<ScriptureRef> BodyRefs, List<string> Strongs, List<string> Problems)
    {
        public List<ScriptureRef> Refs => [.. AnchorRefs.Concat(BodyRefs).Distinct()];
    }

    public static async Task<int> RunAsync(EzraDbContext db, string path, string exportPath, string asvPath)
    {
        // Hebrew and Greek in the output; Windows consoles default to a code page.
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        foreach (var (file, help) in new[]
        {
            (path, "Logos keeps it at %LOCALAPPDATA%\\Logos\\Documents\\<id>\\NotesToolManager\\notestool.db."),
            (exportPath, "Export every note from Logos's Notes panel as HTML."),
            (asvPath, "Download the ASV: curl -o App_Data/asv/asv.json https://api.getbible.net/v2/asv.json"),
        })
            if (!File.Exists(file))
            {
                Console.Error.WriteLine($"Missing {file}. {help}");
                return 1;
            }

        List<LogosNotes.Note> notes;
        try
        {
            notes = LogosNotes.Read(path);
        }
        catch (SqliteException e)
        {
            Console.Error.WriteLine($"Couldn't read {path} as a Logos notes database: {e.Message}");
            return 1;
        }

        var imported = (await db.Notes.Where(n => n.LogosId != null).Select(n => n.LogosId!).ToListAsync()).ToHashSet();
        var withBody = notes.Where(n => n.HasBody).ToList();
        var toImport = withBody.Where(n => !imported.Contains(n.Id)).ToList();

        var export = LogosExport.Read(await File.ReadAllTextAsync(exportPath));
        var asv = LogosAlignment.ReadAsv(await File.ReadAllTextAsync(asvPath));
        var lexicon = await db.Strongs.Select(s => new { s.Id, s.Lemma }).ToListAsync();
        var plan = Plan(toImport, export, asv, new LogosWords(lexicon.Select(s => (s.Id, s.Lemma))));

        Console.WriteLine($"Logos notes: {notes.Count} live, {withBody.Count} with a body ({notes.Count - withBody.Count} skipped, no body), " +
            $"{withBody.Count - toImport.Count} already imported, {toImport.Count} to import.");
        Console.WriteLine("Anchors: " + string.Join(", ", toImport.GroupBy(n => n.Kind).OrderBy(g => g.Key).Select(g => $"{Label(g.Key)} {g.Count()}")));
        Console.WriteLine("Placed from Bible text: " + string.Join(", ", plan.Where(p => p.Confidence is not null)
            .GroupBy(p => p.Confidence!.Value).OrderBy(g => g.Key).Select(g => $"{g.Key.ToString().ToLowerInvariant()} {g.Count()}")) +
            $", not placed {plan.Count(p => p.Note.Kind == LogosNotes.AnchorKind.BibleText && p.AnchorLabel is null)}.");
        Console.WriteLine("Notebooks: " + string.Join(", ", toImport.GroupBy(n => n.Notebook ?? "(none)").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => $"{g.Key} {g.Count()}")));
        Console.WriteLine($"Refs: {plan.Sum(p => p.AnchorRefs.Count)} from anchors, {plan.Sum(p => p.BodyRefs.Count)} from bodies. " +
            $"Strong's: {plan.Sum(p => p.Strongs.Count)}. Problems: {plan.Sum(p => p.Problems.Count)}.");
        Console.WriteLine();
        foreach (var p in plan)
        {
            var conf = p.Confidence?.ToString().ToLowerInvariant() ?? (p.AnchorLabel is null ? "" : "exact");
            var extra = string.Join("  ", new[]
            {
                p.BodyRefs.Count > 0 ? "body: " + string.Join("; ", p.BodyRefs.Select(r => r.Format())) : "",
                p.Strongs.Count > 0 ? "Strong's: " + string.Join(", ", p.Strongs) : "",
                p.Note.Notebook is string nb ? $"notebook: {nb}" : "",
            }.Where(s => s != ""));
            Console.WriteLine($"{Date(p.Note)}  {conf,-7}  {p.AnchorLabel ?? "",-28}  {extra}".TrimEnd());
        }

        var problems = plan.SelectMany(p => p.Problems.Select(x => $"{Date(p.Note)}  {p.AnchorLabel ?? Book(p.Note),-28}  {x}")).ToList();
        if (problems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("To look at:");
            foreach (var x in problems) Console.WriteLine(x);
        }
        Console.WriteLine();
        Console.WriteLine("Dry run: nothing was written.");
        return 0;
    }

    // Works out each note's Markdown, refs and Strong's, in the notes' order.
    public static List<Planned> Plan(
        IReadOnlyList<LogosNotes.Note> notes, IReadOnlyList<LogosExport.ExportNote> export,
        IReadOnlyDictionary<int, List<LogosAlignment.AsvVerse>> asv, LogosWords words)
    {
        var bodies = notes.Select(n => LogosRichText.Convert(n.RichText!)).ToList();
        var problems = notes.Select((_, i) => new List<string>(bodies[i].Problems)).ToList();
        var anchorRefs = notes.Select(_ => new List<ScriptureRef>()).ToList();
        var labels = new string?[notes.Count];
        var confidence = new LogosAlignment.Confidence?[notes.Count];
        var strongs = notes.Select(_ => new List<string>()).ToList();

        // Text selections in a Bible: pair each with its quote from the
        // export (in the export's order: by resource, then offset), then
        // place each resource's notes book by book.
        var texts = notes.Select((n, i) => (i, Anchor: n.Kind == LogosNotes.AnchorKind.BibleText ? n.Anchors.First(a => a.Kind == LogosNotes.AnchorKind.BibleText) : null))
            .Where(x => x.Anchor is not null).OrderBy(x => x.Anchor!.ResourceId, StringComparer.Ordinal).ThenBy(x => x.Anchor!.Offset).ToList();
        var quotes = LogosExport.Pair(texts.Select(x => LogosExport.Key(LogosExport.BodyText(notes[x.i].RichText!))), export);
        var quoteOf = texts.Select((x, k) => (x.i, Quote: quotes[k])).ToDictionary(x => x.i, x => x.Quote);
        foreach (var group in texts.GroupBy(x => (x.Anchor!.ResourceId, Book: notes[x.i].LogosBook is int b ? LogosNotes.EzraBook(b) : null)))
        {
            var members = group.ToList();
            if (group.Key.Book is not int book || !asv.TryGetValue(book, out var verses))
            {
                foreach (var x in members) problems[x.i].Add(group.Key.Book is null ? "anchored in the Apocrypha, so it has no anchor ref." : "the ASV is missing this book.");
                continue;
            }
            var placed = LogosAlignment.Place(verses, [.. members.Select(x => new LogosAlignment.Anchored(x.Anchor!.Offset, x.Anchor.Length, quoteOf[x.i]))]);
            for (var k = 0; k < members.Count; k++)
            {
                var i = members[k].i;
                // The export has no quote for an anchor that selects no words.
                if (quoteOf[i] is null) problems[i].Add("no anchored text in the export, so it's placed by position only.");
                if (placed[k] is not { } p)
                {
                    problems[i].Add("couldn't be placed, so it has no anchor ref.");
                    continue;
                }
                var text = p.Format(BibleBooks.Get(book).Name);
                labels[i] = text;
                confidence[i] = p.Confidence;
                anchorRefs[i].AddRange(ReferenceParser.Parse(text).Refs);
                if (p.Confidence is LogosAlignment.Confidence.Medium or LogosAlignment.Confidence.Low)
                    problems[i].Add($"{p.Confidence.ToString().ToLowerInvariant()} confidence: \"{Snippet(quoteOf[i])}\"");
            }
        }

        // Reference and word anchors are exact.
        for (var i = 0; i < notes.Count; i++)
            foreach (var a in notes[i].Anchors)
            {
                if (a.Kind == LogosNotes.AnchorKind.Reference)
                {
                    var text = LogosRichText.ReferenceText(a.Reference!);
                    var parsed = ReferenceParser.Parse(text);
                    if (text is null || parsed.Errors.Count > 0 || parsed.Refs.Count == 0)
                    {
                        problems[i].Add(text is null ? $"couldn't read the anchor {a.Reference}." : string.Join(" ", parsed.Errors));
                        continue;
                    }
                    anchorRefs[i].AddRange(parsed.Refs);
                    labels[i] ??= text;
                }
                else if (a.Kind == LogosNotes.AnchorKind.Word)
                {
                    var m = words.Find(a.Reference!);
                    if (m.Id is null) { problems[i].Add(m.Problem!); continue; }
                    if (!strongs[i].Contains(m.Id)) strongs[i].Add(m.Id);
                    if (m.Candidates.Count > 1) problems[i].Add($"{a.Reference} matches {string.Join(", ", m.Candidates)}; took {m.Id}.");
                }
            }

        return [.. notes.Select((n, i) => new Planned(n, bodies[i].Markdown, labels[i], confidence[i],
            [.. anchorRefs[i].Distinct()], [.. bodies[i].Refs.Except(anchorRefs[i])], strongs[i], problems[i]))];
    }

    private static string Snippet(string? quote) =>
        quote is null ? "(no quote)" : quote.Length > 60 ? quote[..60] + "…" : quote;

    private static string Label(LogosNotes.AnchorKind kind) => kind switch
    {
        LogosNotes.AnchorKind.Reference => "reference",
        LogosNotes.AnchorKind.Word => "word",
        LogosNotes.AnchorKind.BibleText => "Bible text",
        LogosNotes.AnchorKind.Other => "other",
        _ => "none",
    };

    private static string Date(LogosNotes.Note n) => NoteFolders.Slug(NoteFolders.LocalDate(n.CreatedUtc));

    private static string Book(LogosNotes.Note n) =>
        n.LogosBook is int b && LogosNotes.EzraBook(b) is int ezra ? BibleBooks.Get(ezra).Name : "";
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Bible;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Data;

// `dotnet run -- import-logos <notestool.db>`: brings Renai's Logos notes into
// Ezra. For now it's a dry run that lists what would be imported; the rest is
// built in steps (see the Logos note importer in ProjectPlans/ezra.md). Notes
// with no body are skipped, and so are notes already imported (by Logos id).
public static class LogosImport
{
    public static async Task<int> RunAsync(EzraDbContext db, string path)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Missing {path}. Logos keeps it at %LOCALAPPDATA%\\Logos\\Documents\\<id>\\NotesToolManager\\notestool.db.");
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

        Console.WriteLine($"Logos notes: {notes.Count} live, {withBody.Count} with a body ({notes.Count - withBody.Count} skipped, no body), " +
            $"{withBody.Count - toImport.Count} already imported, {toImport.Count} to import.");
        Console.WriteLine("Anchors: " + string.Join(", ", toImport.GroupBy(n => n.Kind).OrderBy(g => g.Key).Select(g => $"{Label(g.Key)} {g.Count()}")));
        Console.WriteLine("Notebooks: " + string.Join(", ", toImport.GroupBy(n => n.Notebook ?? "(none)").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => $"{g.Key} {g.Count()}")));
        var bodies = toImport.ToDictionary(n => n.Id, n => LogosRichText.Convert(n.RichText!));
        var refCount = bodies.Values.Sum(b => b.Refs.Count);
        Console.WriteLine($"Body refs: {refCount} in {bodies.Values.Count(b => b.Refs.Count > 0)} notes. Problems: {bodies.Values.Sum(b => b.Problems.Count)}.");
        Console.WriteLine();
        foreach (var n in toImport)
        {
            var refs = string.Join("; ", bodies[n.Id].Refs.Select(r => r.Format()));
            Console.WriteLine($"{Date(n)}  {Label(n.Kind),-10}  {Book(n),-16}  {n.Notebook,-24}  {refs}".TrimEnd());
        }

        var problems = toImport.SelectMany(n => bodies[n.Id].Problems.Select(p => $"{Date(n)}  {Book(n),-16}  {p}")).ToList();
        if (problems.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("To look at:");
            foreach (var p in problems) Console.WriteLine(p);
        }
        Console.WriteLine();
        Console.WriteLine("Dry run: nothing was written.");
        return 0;
    }

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

using Microsoft.EntityFrameworkCore;
using starfold_mobile.Bible;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Data;

// `dotnet run -- import-strongs <folder>`: loads the two lexicon files from
// the folder into ezra_strongs. Entries are added or updated by id and never
// deleted, since notes link to them. Safe to run again.
public static class StrongsImport
{
    public static async Task<int> RunAsync(EzraDbContext db, string folder)
    {
        var entries = new List<StrongsEntry>();
        foreach (var name in StrongsLexicon.FileNames)
        {
            var path = Path.Combine(folder, name);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Missing {path}. Download it from https://github.com/openscriptures/strongs.");
                return 1;
            }
            entries.AddRange(StrongsLexicon.Read(await File.ReadAllTextAsync(path)));
        }

        var existing = await db.Strongs.ToDictionaryAsync(s => s.Id);
        int added = 0, updated = 0;
        foreach (var e in entries)
        {
            if (!existing.TryGetValue(e.Id, out var row))
            {
                db.Strongs.Add(e);
                added++;
                continue;
            }
            row.Lemma = e.Lemma;
            row.Transliteration = e.Transliteration;
            row.Definition = e.Definition;
            row.Derivation = e.Derivation;
            row.KjvDef = e.KjvDef;
            if (db.Entry(row).State == EntityState.Modified) updated++;
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Strong's: {entries.Count} entries read, {added} added, {updated} updated.");
        return 0;
    }
}

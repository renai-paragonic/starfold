using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Data;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Controllers.Ezra;

// The tag admin page: rename, merge and delete user tags. Signed in only.
// Changes go by tag id, so names never need escaping in these URLs. No URL
// here is /ezra/tags/<word>: that's a tag's folder (and "merge" is a valid tag).
[Route("ezra/tags"), Authorize, HitCounter]
public class TagsController(EzraDbContext db) : Controller
{
    public record Row(int Id, string Name, int Notes);

    // Every tag, A to Z, with how many notes (public or private) carry it.
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await Rows());

    private async Task<List<Row>> Rows() =>
        [.. (await db.Tags.Select(t => new Row(t.Id, t.Name, t.Notes.Count)).ToListAsync())
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)];

    private async Task<IActionResult> IndexWithError(string error)
    {
        ViewData["Error"] = error;
        return View("Index", await Rows());
    }

    // A rename to another tag's name (any casing) becomes a merge into that
    // tag, confirmed on the merge step. A casing-only change is a rename.
    [HttpPost("{id:int}/rename")]
    public async Task<IActionResult> Rename(int id, string? name)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is null) return NotFound();
        var newName = TagName.Normalize(name);
        if (!TagName.IsValid(newName)) return await IndexWithError($"\"{newName}\": {TagName.Rule}");
        if (newName == tag.Name) return RedirectToAction(nameof(Index));

        var other = await db.Tags.FirstOrDefaultAsync(t => t.Id != id && t.Name == newName); // NOCASE
        if (other is not null)
        {
            TempData["Message"] = $"\"{other.Name}\" already exists. Merge \"{tag.Name}\" into it?";
            return RedirectToAction(nameof(Merge), new { ids = new[] { id, other.Id }, keep = other.Id });
        }

        var old = tag.Name;
        tag.Name = newName;
        await db.SaveChangesAsync();
        TempData["Message"] = $"Renamed \"{old}\" to \"{newName}\".";
        return RedirectToAction(nameof(Index));
    }

    // The merge step: the ticked tags, and a choice of the name to keep.
    [HttpGet("merge/confirm")]
    public async Task<IActionResult> Merge(int[] ids, int? keep)
    {
        var rows = (await Rows()).Where(r => ids.Contains(r.Id)).ToList();
        if (rows.Count < 2) return await IndexWithError("Tick at least two tags to merge.");
        ViewData["Keep"] = keep ?? rows.MaxBy(r => r.Notes)!.Id;
        return View(rows);
    }

    // Moves every note onto the surviving tag (once each), deletes the
    // others, then gives the survivor its final name.
    [HttpPost("merge/confirm")]
    public async Task<IActionResult> Merge(int[] ids, int? keep, string? newName)
    {
        var ticked = await db.Tags.Include(t => t.Notes).Where(t => ids.Contains(t.Id)).ToListAsync();
        var others = await db.Tags.Where(t => !ids.Contains(t.Id)).Select(t => t.Name).ToListAsync();
        var (survivor, name, error) = TagMerge.Choose(ticked, others, keep, newName);
        if (survivor is null)
        {
            ViewData["Error"] = error;
            ViewData["Keep"] = keep;
            ViewData["NewName"] = newName;
            return View((await Rows()).Where(r => ids.Contains(r.Id)).ToList());
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var tag in ticked.Where(t => t != survivor))
        {
            foreach (var note in tag.Notes.Where(n => !survivor.Notes.Contains(n)))
                survivor.Notes.Add(note);
            db.Tags.Remove(tag);
        }
        // Deleted first, so a survivor renamed to a removed tag's name
        // doesn't trip the unique index.
        await db.SaveChangesAsync();
        survivor.Name = name;
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["Message"] = $"Merged {ticked.Count} tags into \"{name}\".";
        return RedirectToAction(nameof(Index));
    }

    // Removes the tag from every note; the notes stay. (The join rows go by
    // cascade, as when a note is deleted.)
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is null) return NotFound();
        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        TempData["Message"] = $"Deleted \"{tag.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}

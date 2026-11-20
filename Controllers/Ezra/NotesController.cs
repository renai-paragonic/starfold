using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Data;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Controllers.Ezra;

[Route("ezra")]
public class NotesController(EzraDbContext db) : Controller
{
    private bool SignedIn => User.Identity?.IsAuthenticated == true;

    // Public notes for visitors; every note when signed in.
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var all = SignedIn;
        var notes = await db.Notes
            .Where(n => all || n.IsPublic)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();
        return View(notes);
    }

    // Private notes are 404 when signed out. A missing or old slug redirects
    // to the current one, so links survive a title change.
    [HttpGet("notes/{id:int}/{slug?}")]
    public async Task<IActionResult> Show(int id, string? slug)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null || (!note.IsPublic && !SignedIn)) return NotFound();
        if (slug != note.Slug) return RedirectToAction(nameof(Show), new { id, slug = note.Slug });
        return View(note);
    }

    [Authorize, HttpGet("notes/new")]
    public IActionResult New() => View("Edit", new NoteForm());

    [Authorize, HttpPost("notes/new")]
    public async Task<IActionResult> New(NoteForm form)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        var now = DateTime.UtcNow;
        var note = new Note { CreatedAt = now };
        Apply(note, form, now);
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Show), new { id = note.Id, slug = note.Slug });
    }

    [Authorize, HttpGet("notes/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        ViewData["NoteId"] = id;
        return View(new NoteForm { Title = note.Title, BodyMd = note.BodyMd, IsPublic = note.IsPublic });
    }

    [Authorize, HttpPost("notes/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, NoteForm form)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewData["NoteId"] = id;
            return View(form);
        }
        Apply(note, form, DateTime.UtcNow);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Show), new { id, slug = note.Slug });
    }

    [Authorize, HttpPost("notes/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // The editor's live preview (debounced in ezra.js).
    [Authorize, HttpPost("preview")]
    public IActionResult Preview(string? bodyMd) => Content(NoteMarkdown.ToHtml(bodyMd), "text/html");

    private static void Apply(Note note, NoteForm form, DateTime now)
    {
        note.Title = form.Title.Trim();
        note.Slug = Slugify(note.Title);
        note.BodyMd = form.BodyMd ?? "";
        note.IsPublic = form.IsPublic;
        note.UpdatedAt = now;
    }

    // "Romans 8: Life in the Spirit" -> "romans-8-life-in-the-spirit".
    private static string Slugify(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }
        var slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        if (slug.Length > 80) slug = slug[..80].TrimEnd('-');
        // "edit" would collide with the notes/{id}/edit route.
        return slug switch { "" => "note", "edit" => "edit-note", _ => slug };
    }
}

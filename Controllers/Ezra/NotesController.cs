using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using starfold_mobile.Bible;
using starfold_mobile.Data;
using starfold_mobile.Models.Ezra;

namespace starfold_mobile.Controllers.Ezra;

[Route("ezra"), HitCounter]
public class NotesController(EzraDbContext db) : Controller
{
    private bool SignedIn => User.Identity?.IsAuthenticated == true;

    private const string ViewCookie = "ezra_view";

    // The notes as folders, by book, tag or date (?view=). Picking a view
    // remembers it in a cookie, so a bare /ezra reopens it; Books otherwise.
    // Public notes for visitors; every note when signed in.
    [HttpGet("")]
    public async Task<IActionResult> Index(string? view)
    {
        if (view is not null && NoteFolders.Views.Contains(view))
            Response.Cookies.Append(ViewCookie, view, Remembered);
        else
        {
            view = Request.Cookies[ViewCookie];
            if (view is null || !NoteFolders.Views.Contains(view)) view = NoteFolders.Books;
        }

        var all = SignedIn;
        var notes = await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
            .Where(n => all || n.IsPublic).ToListAsync();
        return View("Folder", new FolderPage
        {
            Title = "Notes",
            Path = [new("Notes", "/ezra"), ViewStep(view)],
            Toggles = [.. NoteFolders.Views.Select(ViewStep)],
            PressedToggle = ViewStep(view).Label,
            Folders = NoteFolders.Root(notes, view),
            Empty = "No notes yet.",
            NewNote = true,
            ManageTags = view == NoteFolders.Tags,
        });
    }

    // A year-long preference cookie for the Ezra pages.
    private CookieOptions Remembered => new()
    {
        MaxAge = TimeSpan.FromDays(365), Path = "/ezra", HttpOnly = true,
        SameSite = SameSiteMode.Lax, Secure = Request.IsHttps, IsEssential = true,
    };

    // The Icons | List toggle on pages that show notes: remembers the choice
    // and goes back to the page (local URLs only).
    [HttpGet("layout/{layout}")]
    public IActionResult Layout(string layout, string? returnUrl)
    {
        if (layout is not ("icons" or "list")) return NotFound();
        Response.Cookies.Append(NoteFolders.LayoutCookie, layout, Remembered);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    // The sort buttons beside Icons | List, remembered the same way.
    [HttpGet("sort/{sort}")]
    public IActionResult Sort(string sort, string? returnUrl)
    {
        if (!NoteFolders.Sorts.Contains(sort)) return NotFound();
        Response.Cookies.Append(NoteFolders.SortCookie, sort, Remembered);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    private static Folder ViewStep(string view) => view switch
    {
        NoteFolders.Strongs => new("Strong's", "/ezra?view=strongs"),
        NoteFolders.Tags => new("Tags", "/ezra?view=tags"),
        NoteFolders.Dates => new("Dates", "/ezra?view=dates"),
        _ => new("Books", "/ezra?view=books"),
    };

    // Notes created on a day (US Central), from the Dates view.
    [HttpGet("dates/{date}")]
    public async Task<IActionResult> Date(string date)
    {
        if (NoteFolders.ParseSlug(date) is not DateOnly day) return NotFound();
        var all = SignedIn;
        var notes = (await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
                .Where(n => all || n.IsPublic).OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync())
            .Where(n => NoteFolders.LocalDate(n.CreatedAt) == day).ToList();
        if (notes.Count == 0) return NotFound();
        var label = NoteFolders.Label(day);
        return View("Folder", new FolderPage
        {
            Title = label,
            Path = [new("Notes", "/ezra"), ViewStep(NoteFolders.Dates), new(label, "")],
            Files = notes,
        });
    }

    // The Strong's view's Hebrew and Greek folders: the entries tagged on
    // notes the visitor can see, by number. (Literal routes, so they win
    // over strongs/{id}.)
    [HttpGet("strongs/hebrew")]
    public Task<IActionResult> StrongsHebrew() => StrongsLanguage(true);

    [HttpGet("strongs/greek")]
    public Task<IActionResult> StrongsGreek() => StrongsLanguage(false);

    private async Task<IActionResult> StrongsLanguage(bool hebrew)
    {
        var all = SignedIn;
        var prefix = hebrew ? "H" : "G";
        var entries = await db.Strongs
            .Where(s => s.Id.StartsWith(prefix) && s.Notes.Any(n => all || n.IsPublic))
            .Select(s => new StrongsEntry { Id = s.Id, Lemma = s.Lemma, Definition = s.Definition })
            .ToListAsync();
        if (entries.Count == 0) return NotFound();
        var label = NoteFolders.Language(hebrew).Label;
        return View("Folder", new FolderPage
        {
            Title = label,
            Path = [new("Notes", "/ezra"), ViewStep(NoteFolders.Strongs), new(label, "")],
            Entries = [.. entries.OrderBy(e => int.Parse(e.Id[1..]))],
        });
    }

    // Notes with no scripture refs (books) or no user tags (tags).
    [HttpGet("unfiled/{view}")]
    public async Task<IActionResult> Unfiled(string view)
    {
        if (view is not (NoteFolders.Books or NoteFolders.Tags)) return NotFound();
        var all = SignedIn;
        var notes = (await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
                .Where(n => all || n.IsPublic).OrderBy(n => n.CreatedAt).ThenBy(n => n.Id).ToListAsync())
            .Where(n => NoteFolders.IsUnfiled(n, view)).ToList();
        return View("Folder", new FolderPage
        {
            Title = "Unfiled",
            Path = [new("Notes", "/ezra"), ViewStep(view), new("Unfiled", "")],
            Files = notes,
        });
    }

    // Private notes are 404 when signed out. A missing or old slug redirects
    // to the current one, so links survive a title change.
    [HttpGet("notes/{id:int}/{slug?}")]
    public async Task<IActionResult> Show(int id, string? slug)
    {
        var note = await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs).FirstOrDefaultAsync(n => n.Id == id);
        if (note is null || (!note.IsPublic && !SignedIn)) return NotFound();
        if (slug != note.Slug) return RedirectToAction(nameof(Show), new { id, slug = note.Slug });
        return View(note);
    }

    // Notes with a tag. A tag with no notes the visitor can see is 404, so
    // tags used only on private notes stay hidden. Other casings redirect.
    [HttpGet("tags/{name}")]
    public async Task<IActionResult> Tag(string name)
    {
        var all = SignedIn;
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name);
        if (tag is null) return NotFound();
        var notes = await db.Notes
            .Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
            .Where(n => (all || n.IsPublic) && n.Tags.Any(t => t.Id == tag.Id))
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync();
        if (notes.Count == 0) return NotFound();
        if (name != tag.Name) return RedirectToAction(nameof(Tag), new { name = tag.Name });
        return View("Folder", new FolderPage
        {
            Title = tag.Name,
            Path = [new("Notes", "/ezra"), ViewStep(NoteFolders.Tags), new(tag.Name, "")],
            Files = notes,
            NewNote = true,
            NewTag = tag.Name,
        });
    }

    // A book, chapter or verse folder. A book holds its chapter folders and
    // the whole-book notes, or with ?all=true every note in the book. A
    // chapter holds the notes for it or its verses. A verse also finds its
    // whole chapter or book (NoteRef.Matching, as search does).
    // Other spellings of the book ("Rom", "romans") redirect to the slug.
    [HttpGet("bible/{book}/{chapter:int?}/{verse:int?}")]
    public async Task<IActionResult> Bible(string book, int? chapter, int? verse, bool all = false)
    {
        var found = BibleBooks.Find(book);
        if (found is null || chapter < 1 || chapter > found.Chapters
            || (chapter is int c && (verse < 1 || verse > found.VersesIn(c)))) return NotFound();
        var target = new ScriptureRef(found.Number, chapter, verse, verse);
        if (book != found.Slug) return Redirect(target.BrowsePath + (all ? "?all=true" : ""));
        var refs = NoteRef.Matching(db.NoteRefs, target);

        var signedIn = SignedIn;
        var notes = await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
            .Where(n => (signedIn || n.IsPublic) && refs.Any(r => r.NoteId == n.Id))
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync();

        List<Folder> path = [new("Notes", "/ezra"), ViewStep(NoteFolders.Books), new(found.Name, new ScriptureRef(found.Number).BrowsePath)];
        if (chapter is int ch) path.Add(new(ch.ToString(CultureInfo.InvariantCulture), new ScriptureRef(found.Number, ch).BrowsePath));
        if (verse is int v) path.Add(new(v.ToString(CultureInfo.InvariantCulture), target.BrowsePath));
        path[^1] = path[^1] with { Url = "" };

        // A verse page, and a book's "All notes", list everything found.
        List<Folder> toggles = [], folders = [];
        var files = notes;
        if (chapter is null)
        {
            toggles = [new("Chapters", target.BrowsePath), new("All notes", target.BrowsePath + "?all=true")];
            if (!all)
            {
                folders = NoteFolders.Chapters(notes, found.Number);
                files = notes.Where(n => NoteFolders.InBook(n, found.Number)).ToList();
            }
        }
        else if (verse is null)
            files = notes.Where(n => NoteFolders.InChapter(n, found.Number, chapter.Value)).ToList();

        return View("Folder", new FolderPage
        {
            Title = target.Format(),
            Path = path,
            Toggles = toggles,
            PressedToggle = all ? "All notes" : "Chapters",
            Folders = folders,
            Files = files,
            LsbUrl = target.GatewayUrl,
            NewNote = true,
            NewReferences = target.Format(),
        });
    }

    // Search: every text word must match a title, body or tag name, and every
    // chip must match too. Chips come as ?tag=, ?ref= and ?strongs= (from the
    // header's search box). Unreadable ref and Strong's chips are dropped;
    // a chip that matches nothing just gives no results.
    [HttpGet("search")]
    public async Task<IActionResult> Search(string? q, string[] tag, [FromQuery(Name = "ref")] string[] refs, string[] strongs)
    {
        q = q?.Trim() ?? "";
        var all = SignedIn;
        var notes = db.Notes.Where(n => all || n.IsPublic);
        var chips = new List<NoteSearch.Chip>();

        foreach (var typed in tag.Select(TagName.Normalize).Where(n => n != "").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            notes = notes.Where(n => n.Tags.Any(t => t.Name == typed)); // NOCASE
            var name = await db.Tags.Where(t => t.Name == typed).Select(t => t.Name).FirstOrDefaultAsync() ?? typed;
            chips.Add(new(NoteSearch.TagKind, name, name, "/ezra/tags/" + Uri.EscapeDataString(name)));
        }
        foreach (var text in refs.Distinct())
        {
            var parsed = ReferenceParser.Parse(text);
            if (parsed.Errors.Count > 0 || parsed.Refs.Count == 0) continue;
            // A cross-chapter range parses to several rows; any of them counts.
            var noteIds = parsed.Refs.Select(r => NoteRef.Matching(db.NoteRefs, r).Select(x => x.NoteId)).Aggregate((a, b) => a.Concat(b));
            notes = notes.Where(n => noteIds.Contains(n.Id));
            var label = string.Join("; ", parsed.Refs.Select(r => r.Format()));
            chips.Add(new(NoteSearch.RefKind, label, label, parsed.Refs[0].BrowsePath));
        }
        foreach (var id in strongs.Select(StrongsParser.Normalize).OfType<string>().Distinct())
        {
            notes = notes.Where(n => n.Strongs.Any(s => s.Id == id));
            var entry = await db.Strongs.FindAsync(id);
            chips.Add(new(NoteSearch.StrongsKind, id, entry is null ? id : $"{id} {entry.Lemma}", StrongsParser.BrowsePath(id)));
        }

        var patterns = NoteSearch.Patterns(q);
        foreach (var p in patterns)
            notes = notes.Where(n => EF.Functions.Like(n.Title, p, NoteSearch.Escape)
                || EF.Functions.Like(n.BodyMd, p, NoteSearch.Escape)
                || n.Tags.Any(t => EF.Functions.Like(t.Name, p, NoteSearch.Escape)));

        ViewData["Query"] = q;
        ViewData["SearchChips"] = chips;
        if (patterns.Count == 0 && chips.Count == 0) return View(new List<Note>());
        var results = await notes
            .Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync();
        return View(results);
    }

    // The lexicon only changes on import, so it's read once. An empty one
    // isn't kept, so suggestions start working once the import has run.
    private static StrongsWords? strongsWords;

    // The search box's flyout options for the text typed so far (debounced in
    // ezra.js). Visitors only get tags that are on a public note.
    [HttpGet("search/suggest")]
    public async Task<IActionResult> Suggest(string? q)
    {
        var all = SignedIn;
        var tags = await db.Tags.Where(t => all || t.Notes.Any(n => n.IsPublic)).Select(t => t.Name).ToListAsync();
        var last = (q ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        List<StrongsEntry> strongs = [];
        if (StrongsParser.Normalize(last) is string id)
        {
            if (await db.Strongs.FindAsync(id) is StrongsEntry entry) strongs.Add(entry);
        }
        else if (last is not null)
        {
            if (strongsWords is null)
            {
                var words = new StrongsWords(await db.Strongs
                    .Select(s => new StrongsEntry { Id = s.Id, Lemma = s.Lemma, Transliteration = s.Transliteration })
                    .ToListAsync());
                if (words.Count > 0) strongsWords = words;
            }
            strongs = strongsWords?.Match(last) ?? [];
        }
        return Json(NoteSearch.Suggest(q, tags, strongs));
    }

    // A lexicon entry and the notes tagged with it. Other spellings ("g0026")
    // redirect to the stored id. Numbers the lexicon doesn't have are 404.
    [HttpGet("strongs/{id}")]
    public async Task<IActionResult> Strongs(string id)
    {
        var normal = StrongsParser.Normalize(id);
        var entry = normal is null ? null : await db.Strongs.FindAsync(normal);
        if (entry is null) return NotFound();
        if (id != entry.Id) return RedirectToAction(nameof(Strongs), new { id = entry.Id });

        var all = SignedIn;
        var notes = await db.Notes
            .Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs)
            .Where(n => (all || n.IsPublic) && n.Strongs.Any(s => s.Id == entry.Id))
            .OrderBy(n => n.CreatedAt).ThenBy(n => n.Id)
            .ToListAsync();

        // Numbers mentioned in the entry's text become links when they exist.
        var mentioned = StrongsParser.Mentions(entry.Definition).Concat(StrongsParser.Mentions(entry.Derivation))
            .Concat(StrongsParser.Mentions(entry.KjvDef)).Distinct().ToList();
        ViewData["Known"] = (await db.Strongs.Where(s => mentioned.Contains(s.Id)).Select(s => s.Id).ToListAsync()).ToHashSet();
        ViewData["Entry"] = entry;
        ViewData["Path"] = new List<Folder>
        {
            new("Notes", "/ezra"), ViewStep(NoteFolders.Strongs),
            NoteFolders.Language(StrongsParser.IsHebrew(entry.Id)),
            new($"{entry.Id} {entry.Lemma}", ""),
        };
        return View(notes);
    }

    // The "New note" button on the Bible browse, Strong's and tag pages passes
    // ?references= or ?tag= (pre-filled, and checked on save as usual) and
    // ?returnUrl= (where Save and Cancel go back to).
    [Authorize, HttpGet("notes/new")]
    public Task<IActionResult> New(string? references, string? tag, string? returnUrl) =>
        EditorView(new NoteForm
        {
            ReferenceChips = string.IsNullOrWhiteSpace(references) ? [] : [references.Trim()],
            Tags = string.IsNullOrWhiteSpace(tag) ? [] : [TagName.Normalize(tag)],
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null,
        }, null);

    [Authorize, HttpPost("notes/new")]
    public async Task<IActionResult> New(NoteForm form)
    {
        var tags = CleanTags(form);
        var (refs, strongs) = await ParseRefs(form);
        if (!ModelState.IsValid) return await EditorView(form, null);
        var now = DateTime.UtcNow;
        var note = new Note { CreatedAt = now };
        Apply(note, form, now);
        await SetTags(note, tags);
        SetRefs(note, refs, strongs);
        db.Notes.Add(note);
        await db.SaveChangesAsync();
        if (Url.IsLocalUrl(form.ReturnUrl)) return LocalRedirect(form.ReturnUrl);
        return RedirectToAction(nameof(Show), new { id = note.Id, slug = note.Slug });
    }

    [Authorize, HttpGet("notes/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var note = await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs).FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();
        return await EditorView(new NoteForm
        {
            Title = note.Title,
            BodyMd = note.BodyMd,
            IsPublic = note.IsPublic,
            Tags = [.. note.Tags.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase)],
            ReferenceChips = [.. NoteRef.Sort(note.Refs).Select(r => r.ToScriptureRef().Format())
                .Concat(StrongsParser.Sort(note.Strongs.Select(s => s.Id)))],
        }, id);
    }

    [Authorize, HttpPost("notes/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, NoteForm form)
    {
        var note = await db.Notes.Include(n => n.Tags).Include(n => n.Refs).Include(n => n.Strongs).FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return NotFound();
        var tags = CleanTags(form);
        var (refs, strongs) = await ParseRefs(form);
        if (!ModelState.IsValid) return await EditorView(form, id);
        Apply(note, form, DateTime.UtcNow);
        await SetTags(note, tags);
        SetRefs(note, refs, strongs);
        await db.SaveChangesAsync();
        await DeleteUnusedTags();
        return RedirectToAction(nameof(Show), new { id, slug = note.Slug });
    }

    [Authorize, HttpPost("notes/{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return NotFound();
        db.Notes.Remove(note);
        await db.SaveChangesAsync();
        await DeleteUnusedTags();
        return RedirectToAction(nameof(Index));
    }

    // The editor's live preview (debounced in ezra.js).
    [Authorize, HttpPost("preview")]
    public IActionResult Preview(string? bodyMd) => Content(NoteMarkdown.ToHtml(bodyMd), "text/html");

    // The editor's "Pop out preview" window. It starts empty: the editor
    // sends it the title and preview over a BroadcastChannel (ezra.js).
    [Authorize, HttpGet("preview/window")]
    public IActionResult PreviewWindow() => View();

    // The editor needs every tag name for the flyout's suggestions, and the
    // lemmas for its Strong's chips ("G26 ἀγάπη").
    private async Task<IActionResult> EditorView(NoteForm form, int? id)
    {
        ViewData["NoteId"] = id;
        ViewData["AllTags"] = (await db.Tags.Select(t => t.Name).ToListAsync())
            .Order(StringComparer.OrdinalIgnoreCase).ToList();
        var ids = form.ReferenceChips.Select(StrongsParser.Normalize).OfType<string>().ToList();
        ViewData["Lemmas"] = await db.Strongs.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Lemma);
        return View("Edit", form);
    }

    // Normalizes and de-duplicates the posted tag names, and reports any that
    // break the TagName rules.
    private List<string> CleanTags(NoteForm form)
    {
        var names = form.Tags.Select(TagName.Normalize).Where(n => n != "")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in names.Where(n => !TagName.IsValid(n)))
            ModelState.AddModelError(nameof(form.Tags), $"\"{name}\": {TagName.Rule}");
        form.Tags = names;
        return names;
    }

    // Reuses an existing tag when the name matches in any casing, so a tag
    // keeps the casing it was first typed with.
    private async Task SetTags(Note note, List<string> names)
    {
        var existing = await db.Tags.ToListAsync();
        note.Tags.Clear();
        foreach (var name in names)
            note.Tags.Add(existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? new Tag { Name = name });
    }

    // The references field holds scripture and Strong's numbers. Strong's
    // numbers must be in the imported lexicon. Errors are reported inline;
    // the note isn't saved until they're fixed.
    private async Task<(List<ScriptureRef>, List<StrongsEntry>)> ParseRefs(NoteForm form)
    {
        var (ids, rest) = StrongsParser.Extract(form.AllReferences);
        var result = ReferenceParser.Parse(rest);
        foreach (var error in result.Errors)
            ModelState.AddModelError(nameof(form.References), error);
        var strongs = await db.Strongs.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var id in ids.Except(strongs.Select(s => s.Id)))
            ModelState.AddModelError(nameof(form.References), $"\"{id}\": not in the Strong's lexicon.");
        return (result.Refs, strongs);
    }

    // Replaces the note's references (removed rows are deleted by EF).
    private static void SetRefs(Note note, List<ScriptureRef> refs, List<StrongsEntry> strongs)
    {
        note.Refs.Clear();
        foreach (var r in refs)
            note.Refs.Add(new NoteRef { Book = r.Book, Chapter = r.Chapter, VerseStart = r.VerseStart, VerseEnd = r.VerseEnd });
        note.Strongs.Clear();
        note.Strongs.AddRange(strongs);
    }

    private Task<int> DeleteUnusedTags() => db.Tags.Where(t => !t.Notes.Any()).ExecuteDeleteAsync();

    private static void Apply(Note note, NoteForm form, DateTime now)
    {
        note.Title = form.Title.Trim();
        note.Slug = Slugify(note.Title);
        note.BodyMd = form.BodyMd ?? "";
        note.IsPublic = form.IsPublic;
        note.UpdatedAt = now;
    }

    // "Romans 8: Life in the Spirit" -> "romans-8-life-in-the-spirit".
    // Public for the Logos import (Data/LogosImport.cs).
    public static string Slugify(string title)
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

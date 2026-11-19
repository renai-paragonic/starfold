# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

## 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

This repo has no test project. Verify instead by:
- `dotnet build` with no new warnings.
- `dotnet run` (http://localhost:5021) and check the page at **both** a phone width (~390px) and a desktop width (≥1024px). A change to one layout often breaks the other.
- Testing interactions by both mouse and touch (profile press, Talmy jumps, accordion).

Transform tasks into verifiable goals:
- "Fix the overflow" → "At 390×844 the bio no longer scrolls; desktop is unchanged"
- "Add a social link" → "Icon renders, links out, and spacing still fits at 360px"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

## 5. This Repository

**Starfold** is a personal home/bio page for the streamer Renai Paragonic. It's an ASP.NET Core 9 MVC app (`starfold_mobile` namespace). It may later also host an API for stream bots.

### Where things live
- `Views/Home/Index.cshtml`: the only real page. All bio and accordion content is here.
- `Views/Shared/_Layout.cshtml`: the page shell. It loads only `site.css`, `site.js`, and Font Awesome 6.4.2 from cdnjs.
- `wwwroot/css/site.css`: all styling.
- `wwwroot/js/site.js`: all behavior. Vanilla JS, no frameworks.
- `wwwroot/images/`: artwork. `Talmy Overdrive.gif` has a space in its name, so keep references to it exact.
- **Ezra** (notes feature): plan and status in `ProjectPlans/ezra.md`. Code in `Data/EzraDbContext.cs`, `Data/Migrations/`, and `Models/Ezra/`.
- `App_Data/`: the local dev SQLite database (git-ignored). Don't put it in `data/`: on Windows that's the same folder as `Data/` (the code).

### Layout rules
- There are two separate layouts, split at **767px / 768px**. Mobile styles live in `@media (max-width: 767px)` and desktop styles in `@media (min-width: 768px)`. Put each change in the block it applies to.
- The breakpoint variables in `:root` don't work inside media queries. Use the literal pixel values.
- Mobile is a fixed, non-scrolling screen: the profile takes 37vh and the bio the rest. Any new mobile content must fit without overflow.
- On desktop, the profile takes 40% and the bio 60%. The accordion shows only on desktop; mobile hides it.
- Keep both `100dvh` and its `100vh` fallback where they already appear.

### Interactions that must keep working
- **Profile image:** a press (or a hover on desktop) swaps `profile-default` for `profile-press` by adding `.pressed` for 2.5s. The two images are absolutely positioned with hand-tuned transforms, so don't change their offsets or scales unless asked.
- **Talmy sheep:** clicking a sheep makes it jump. After 10 total clicks that include both sheep, the "Talmy Overdrive" easter egg takes over the page. The left sheep is mirrored with `scaleX(-1)`, so every jump keyframe has to keep that flip (and the desktop `scale(2.10)`).
- **Accordion:** only one item can be open at a time, and items above the open one collapse (via `:has`).

### Content and voice
- The copy is written in Renai's own voice: playful, internet-native, and grounded in scripture. Don't rewrite, "fix", or tone down the copy unless asked. Point out typos rather than silently correcting them.
- Scripture links go to Bible Gateway with `version=NASB1995`. Follow the same format for new ones.
- In Razor, a literal `@` must be written `@@` (for example `youtube.com/@@renaiparagonic`).

### Conventions
- Some files have a UTF-8 BOM. Preserve the existing encoding.
- Add new images to `wwwroot/images/` and reference them with root-relative paths (`/images/...`), as the existing markup does.
- Keep it dependency-free: no npm, no bundler, and no new NuGet packages or CDN libraries without asking.
- Approved NuGet packages: `Microsoft.EntityFrameworkCore.Sqlite` and `.Design` (9.0.x), and `Markdig` (from Ezra step 3).
- `dotnet-ef` is a local tool (`dotnet tool restore`). Add migrations with `dotnet tool run dotnet-ef migrations add <Name> --output-dir Data/Migrations`. The app applies them at startup.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.

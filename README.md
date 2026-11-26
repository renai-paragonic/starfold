# starfold
Web app I vibe coded using Claude Sonata to serve as a home/bio page. Will likely also serve as an API for stream bots down the line.

## Running your own copy of Ezra

Ezra is the note-taking part of the site, at `/ezra`. It's built for Bible study and sermon prep:

- Markdown notes with a live preview, and a pop-out window that collects notes to page between.
- Free-form tags, plus scripture references (book, chapter or verse, LSB versification) and Strong's numbers.
- Browse by date, book, chapter, Strong's number or tag, and search across all of it.

There's one admin, who writes the notes. Each note is private unless marked public, and visitors can read the public ones.

The site's homepage (`/`) is my own bio page. Ezra is what you'd run a copy for.

### What you need

- The [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
- Git, to clone the repo.
- Nothing else. The database is SQLite, a single file the app creates for you, and there's no Node, npm or database server to install.

### 1. Get the code

```sh
git clone https://github.com/renai-paragonic/starfold.git
cd starfold
```

### 2. Set your admin password

Ezra stores a hash of the admin password, never the password itself. Make one with the app's own command, run in a normal terminal (it asks for the password twice, and won't work from a non-interactive shell):

```sh
dotnet run -- hash-password
```

It prints a long hash starting with `AQAAAA`. Put it in a new file called `appsettings.Development.json` in the project folder (it's git-ignored, so it stays on your machine):

```json
{
  "Ezra": {
    "AdminPasswordHash": "AQAAAA...paste the whole hash here..."
  }
}
```

Or set it as the environment variable `Ezra__AdminPasswordHash` instead. Without a hash, the site still runs, but nobody can sign in.

### 3. Run it

```sh
dotnet run
```

Then open http://localhost:5021/ezra. There's no sign-in link on the pages, so go to http://localhost:5021/ezra/login and sign in with your password.

- The database is created at `App_Data/starfold.db` on first run, and the app brings it up to date by itself on every start. Back up that file (and the `App_Data/keys` folder next to it, which keeps you signed in across restarts) to keep your notes safe.
- **If signing in doesn't stick** (the sign-in cookie is marked secure, and a browser may refuse it on plain `http://localhost`), use HTTPS instead: run `dotnet dev-certs https --trust` once, then `dotnet run --launch-profile https` and open https://localhost:7206/ezra.

The shell commands in steps 4 and 5 are written for bash (macOS, Linux, or Git Bash on Windows). In Windows PowerShell, make the folders with `New-Item -ItemType Directory`, and type `curl.exe` rather than `curl`, which there means a different command.

### 4. Optional: the Strong's lexicon

Strong's numbers work best with the lexicon loaded: entry pages show the lemma, transliteration and definition, and the editor can suggest numbers by English word. The data is [Open Scriptures' Strong's dictionaries](https://github.com/openscriptures/strongs) (CC BY-SA), so it isn't included in this MIT-licensed repo. Download the two files and import them:

```sh
mkdir -p App_Data/strongs
curl -o App_Data/strongs/strongs-greek-dictionary.js https://raw.githubusercontent.com/openscriptures/strongs/master/greek/strongs-greek-dictionary.js
curl -o App_Data/strongs/strongs-hebrew-dictionary.js https://raw.githubusercontent.com/openscriptures/strongs/master/hebrew/strongs-hebrew-dictionary.js
dotnet run -- import-strongs App_Data/strongs
```

It loads 14,197 entries in a few seconds and is safe to run again.

### 5. Optional: import notes from Logos

If you keep notes in Logos Bible Software, `import-logos` can bring them in, with their dates, their notebooks (each becomes a tag), their scripture anchors and their Strong's links. It reads a copy of Logos's local notes database plus a full HTML export of your notes, and needs the public-domain ASV text to place notes on verses:

```sh
mkdir -p App_Data/asv
curl -o App_Data/asv/asv.json https://api.getbible.net/v2/asv.json
dotnet run -- import-logos <path to a copy of notestool.db> <path to your notes export .html>
```

Without `--write` it's a dry run that only reports what it would do. Close Logos first, work from a copy of `notestool.db` (found under `%LOCALAPPDATA%\Logos\Documents\<id>\NotesToolManager\`), and add `--write` when the report looks right. Imported notes are public, and running it again skips notes it already imported.

### Running it in Docker instead

The repo's `Dockerfile` builds a Linux image that keeps its data in `/data`:

```sh
docker build -t starfold .
docker run -d -p 127.0.0.1:8080:8080 -v starfold-data:/data -e Ezra__AdminPasswordHash='AQAAAA...' starfold
```

Then open http://localhost:8080/ezra. The other commands run the same way, for example `docker run --rm -it starfold hash-password`.

### Good to know

- **The starfield background** (`wwwroot/images/star-bg.gif`) isn't in the repo, because its source and license are unknown. Without it, Ezra falls back to a plain night-sky color.
- **Scripture links** open the Legacy Standard Bible (LSB) on Bible Gateway, and lexicon links open Blue Letter Bible.
- **Tests:** `dotnet test tests/Starfold.Tests` runs the xUnit tests (Bible data, the reference parser and the Strong's parser).
- **Sharing a database with Paraclete:** the SQLite file is designed to be shared with my upcoming Twitch bot, Paraclete. Ezra only touches its own `ezra_` tables, so you can ignore that if you're only running Ezra.

## License

The code is MIT licensed (see `LICENSE`). The Strong's lexicon data you download in step 4 is CC BY-SA, from Open Scriptures.

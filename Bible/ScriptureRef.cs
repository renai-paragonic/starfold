namespace starfold_mobile.Bible;

// A scripture tag: a whole book (Chapter null), a whole chapter (VerseStart
// null), or a verse range within one chapter. A single verse has
// VerseEnd == VerseStart.
public sealed record ScriptureRef(int Book, int? Chapter = null, int? VerseStart = null, int? VerseEnd = null)
{
    // "Genesis", "Romans 8", "John 3:16", "John 3:16-18". The parser reads
    // this form back, so the editor can round-trip it.
    public string Format()
    {
        var name = BibleBooks.Get(Book).Name;
        if (Chapter is null) return name;
        if (VerseStart is null) return $"{name} {Chapter}";
        return VerseEnd == VerseStart ? $"{name} {Chapter}:{VerseStart}" : $"{name} {Chapter}:{VerseStart}-{VerseEnd}";
    }

    public string GatewayUrl =>
        $"https://www.biblegateway.com/passage/?search={Uri.EscapeDataString(Format())}&version=LSB";

    // The Ezra browse page for this reference (a range opens at its first verse).
    public string BrowsePath
    {
        get
        {
            var path = "/ezra/bible/" + BibleBooks.Get(Book).Slug;
            if (Chapter is not null) path += "/" + Chapter;
            if (VerseStart is not null) path += "/" + VerseStart;
            return path;
        }
    }
}

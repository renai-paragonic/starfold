namespace starfold_mobile.Models.Ezra;

// One lexicon entry, imported from Open Scriptures' strongs data
// (`dotnet run -- import-strongs <folder>`).
public class StrongsEntry
{
    public string Id { get; set; } = ""; // e.g. "G26", "H430"
    public string Lemma { get; set; } = "";
    public string? Transliteration { get; set; }
    public string? Definition { get; set; }  // strongs_def
    public string? Derivation { get; set; }
    public string? KjvDef { get; set; }       // kjv_def: how the KJV renders it

    public List<Note> Notes { get; set; } = [];
}

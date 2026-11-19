namespace starfold_mobile.Models.Ezra;

public class StrongsEntry
{
    public string Id { get; set; } = ""; // e.g. "G26", "H430"
    public string Lemma { get; set; } = "";
    public string? Transliteration { get; set; }
    public string? Definition { get; set; }

    public List<Note> Notes { get; set; } = [];
}

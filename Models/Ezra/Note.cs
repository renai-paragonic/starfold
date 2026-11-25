namespace starfold_mobile.Models.Ezra;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string BodyMd { get; set; } = "";
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // The Logos note this was imported from (its ExternalId), so a re-import skips it.
    public string? LogosId { get; set; }

    public List<Tag> Tags { get; set; } = [];
    public List<NoteRef> Refs { get; set; } = [];
    public List<StrongsEntry> Strongs { get; set; } = [];
}

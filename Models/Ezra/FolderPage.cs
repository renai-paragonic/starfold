namespace starfold_mobile.Models.Ezra;

public record Folder(string Label, string Url);

// One window of the folder view: an address line, optional toggle buttons,
// then folder icons followed by file icons (Strong's entries, or notes).
public class FolderPage
{
    public required string Title { get; init; }
    // From "Notes" down to this folder; the last step isn't linked.
    public List<Folder> Path { get; init; } = [];
    public List<Folder> Toggles { get; init; } = [];
    public string? PressedToggle { get; init; }
    public List<Folder> Folders { get; init; } = [];
    public List<StrongsEntry> Entries { get; init; } = [];
    public List<Note> Files { get; init; } = [];
    public string Empty { get; init; } = "This folder is empty.";
    public string? LsbUrl { get; init; }
    // "New note" (signed in). A reference or tag pre-fills the editor, and
    // then Save and Cancel come back to this folder.
    public bool NewNote { get; init; }
    public string? NewReferences { get; init; }
    public string? NewTag { get; init; }
}

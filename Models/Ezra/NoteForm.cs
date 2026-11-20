using System.ComponentModel.DataAnnotations;

namespace starfold_mobile.Models.Ezra;

// What the editor posts. Kept separate from Note so a post can't set ids or dates.
public class NoteForm
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    public string? BodyMd { get; set; }

    public bool IsPublic { get; set; }
}

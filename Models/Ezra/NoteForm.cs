using System.ComponentModel.DataAnnotations;

namespace starfold_mobile.Models.Ezra;

// What the editor posts. Kept separate from Note so a post can't set ids or dates.
public class NoteForm
{
    [Required, StringLength(200)]
    public string Title { get; set; } = "";

    public string? BodyMd { get; set; }

    public bool IsPublic { get; set; }

    // One entry per tag chip. Names are cleaned and checked in the controller.
    public List<string> Tags { get; set; } = [];

    // Semicolon-separated scripture references and Strong's numbers, read by
    // Bible/StrongsParser (G26, H430) and then Bible/ReferenceParser.
    [Display(Name = "Scripture references")]
    public string? References { get; set; }
}

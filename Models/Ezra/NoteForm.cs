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

    // The references box: chips (one scripture ref or Strong's number each,
    // picked from the flyout) plus any typed text. Both are joined with ";"
    // and read by Bible/StrongsParser (G26, H430) and then Bible/ReferenceParser.
    public List<string> ReferenceChips { get; set; } = [];

    [Display(Name = "Scripture and Strong's")]
    public string? References { get; set; }

    public string AllReferences => string.Join("; ", ReferenceChips.Append(References ?? "").Where(s => !string.IsNullOrWhiteSpace(s)));

    // Where Save and Cancel go back to, when the editor was opened from a
    // Bible browse or Strong's page. Local URLs only (checked in the controller).
    public string? ReturnUrl { get; set; }
}

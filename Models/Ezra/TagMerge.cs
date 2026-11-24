namespace starfold_mobile.Models.Ezra;

// The tag admin page's merge step: which tag survives, and its final name.
public static class TagMerge
{
    // `keep` is the id of a ticked tag whose name survives, or null for the
    // typed `newName`. A new name that matches a ticked tag (any casing)
    // keeps that tag under the typed casing; one that matches an unticked
    // tag is refused, since merging it in wasn't asked for.
    public static (Tag? Survivor, string Name, string? Error) Choose(
        IReadOnlyList<Tag> ticked, IEnumerable<string> otherNames, int? keep, string? newName)
    {
        if (ticked.Count < 2) return (null, "", "Tick at least two tags to merge.");
        if (keep is int id)
        {
            var kept = ticked.FirstOrDefault(t => t.Id == id);
            return kept is null ? (null, "", "Pick which name to keep.") : (kept, kept.Name, null);
        }

        var name = TagName.Normalize(newName);
        if (name == "") return (null, "", "Pick which name to keep, or type a new one.");
        if (!TagName.IsValid(name)) return (null, name, $"\"{name}\": {TagName.Rule}");
        var same = ticked.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (same is not null) return (same, name, null);
        if (otherNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            return (null, name, $"\"{name}\" is another tag. Tick it too to merge into it.");
        return (ticked[0], name, null);
    }
}

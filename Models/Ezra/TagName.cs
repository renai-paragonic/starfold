using System.Text;
using System.Text.RegularExpressions;

namespace starfold_mobile.Models.Ezra;

// The rules for user tag names, shared by the server and the editor's flyout
// (which gets ClientPattern and Rule through data attributes).
//
// To accept more characters, add them to ExtraChars and update Rule. Never
// allow '/': tag names appear in /ezra/tags/{name}.
public static partial class TagName
{
    public const int MaxLength = 50;

    // Accepted on top of letters and digits.
    private const string ExtraChars = " -";

    public const string Rule = "tags can use letters, digits, spaces and hyphens, up to 50 characters.";

    // Trims and collapses inner whitespace. Casing is kept as typed.
    public static string Normalize(string? raw) =>
        Whitespace().Replace((raw ?? "").Normalize(NormalizationForm.FormC).Trim(), " ");

    public static bool IsValid(string name) =>
        name.Length is > 0 and <= MaxLength
        && name.All(c => char.IsLetter(c) || char.IsDigit(c) || ExtraChars.Contains(c));

    // The same rule as a JavaScript regex (used with the "u" flag).
    public static string ClientPattern { get; } =
        $"^[\\p{{L}}\\p{{Nd}}{string.Concat(ExtraChars.Select(c => $"\\u{{{(int)c:X}}}"))}]{{1,{MaxLength}}}$";

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

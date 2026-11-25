using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class NoteSortTests
{
    private static Note N(int id, string title, int day) => new() { Id = id, Title = title, CreatedAt = new DateTime(2026, 1, day, 12, 0, 0) };

    private static readonly List<Note> Notes =
    [
        N(1, "Hebrews 1:10 — The Son made the world", 3),
        N(2, "hebrews 1:2 — Heir of all things", 1),
        N(3, "“And let all the angels worship Him”", 4),
        N(4, "Hebrews 1:2 — Heir of all things", 2),
        N(5, "Grace is not the only thing", 1),
    ];

    private static int[] Ids(string? sort) => [.. NoteFolders.Sorted(Notes, sort).Select(n => n.Id)];

    [Fact]
    public void Oldest_first_is_the_default_and_ties_go_by_id()
    {
        Assert.Equal([2, 5, 4, 1, 3], Ids(NoteFolders.Oldest));
        Assert.Equal(Ids(NoteFolders.Oldest), Ids(null));
        Assert.Equal(Ids(NoteFolders.Oldest), Ids("sideways"));
    }

    [Fact]
    public void Newest_first_is_the_reverse() => Assert.Equal([3, 1, 4, 5, 2], Ids(NoteFolders.Newest));

    [Fact]
    public void A_to_z_compares_numbers_by_value_and_ignores_case_and_leading_quotes() =>
        Assert.Equal([3, 5, 2, 4, 1], Ids(NoteFolders.AToZ));

    [Fact]
    public void Z_to_a_is_the_reverse_with_ties_still_oldest_first() =>
        Assert.Equal([1, 2, 4, 5, 3], Ids(NoteFolders.ZToA));
}

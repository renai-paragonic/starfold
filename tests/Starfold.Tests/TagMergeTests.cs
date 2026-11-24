using starfold_mobile.Models.Ezra;

namespace Starfold.Tests;

public class TagMergeTests
{
    private static readonly Tag Grace = new() { Id = 1, Name = "Grace" }, Mercy = new() { Id = 2, Name = "mercy" };
    private static readonly Tag[] Ticked = [Grace, Mercy];
    private static readonly string[] Others = ["Sermons"];

    [Fact]
    public void Keeps_a_ticked_tag_by_id()
    {
        var (survivor, name, error) = TagMerge.Choose(Ticked, Others, 2, null);
        Assert.Same(Mercy, survivor);
        Assert.Equal("mercy", name);
        Assert.Null(error);
    }

    [Fact]
    public void A_kept_id_wins_over_a_typed_name() =>
        Assert.Same(Grace, TagMerge.Choose(Ticked, Others, 1, "Lovingkindness").Survivor);

    [Fact]
    public void A_new_name_renames_the_first_ticked_tag()
    {
        var (survivor, name, _) = TagMerge.Choose(Ticked, Others, null, "  Loving   kindness ");
        Assert.Same(Grace, survivor);
        Assert.Equal("Loving kindness", name);
    }

    [Fact]
    public void A_new_name_matching_a_ticked_tag_keeps_it_with_the_typed_casing()
    {
        var (survivor, name, _) = TagMerge.Choose(Ticked, Others, null, "Mercy");
        Assert.Same(Mercy, survivor);
        Assert.Equal("Mercy", name);
    }

    [Theory]
    [InlineData(null, "sermons", "Tick it too")]   // an unticked tag's name
    [InlineData(null, "", "Pick which name")]
    [InlineData(null, "a/b", "tags can use")]       // breaks TagName
    [InlineData(9, null, "Pick which name")]        // not one of the ticked
    public void Refuses(int? keep, string? newName, string message)
    {
        var (survivor, _, error) = TagMerge.Choose(Ticked, Others, keep, newName);
        Assert.Null(survivor);
        Assert.Contains(message, error);
    }

    [Fact]
    public void Needs_two_tags() =>
        Assert.Contains("at least two", TagMerge.Choose([Grace], Others, 1, null).Error);
}

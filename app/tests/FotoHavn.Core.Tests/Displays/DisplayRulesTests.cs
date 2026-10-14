using FotoHavn.Core.Displays;
using static FotoHavn.Core.Tests.Support.TestDisplays;

namespace FotoHavn.Core.Tests.Displays;

public sealed class DisplayRulesTests
{
    private static readonly DisplayInfo Primary = Display(@"\\?\DISPLAY#DEL41A8#1", primary: true);
    private static readonly DisplayInfo Second = Display(@"\\?\DISPLAY#SAM0F9E#2", x: 1920);

    [Fact]
    public void The_same_device_path_matches()
    {
        var replugged = Second with { Bounds = new DisplayRect(-1920, 0, 2560, 1440), Name = "Renamed" };

        Assert.Same(replugged, DisplayMatching.Find([Primary, replugged], Second.Id));
    }

    [Fact]
    public void The_same_position_with_another_device_path_does_not_match()
    {
        var sameSpot = Second with { Id = @"\\?\DISPLAY#LEN65E9#3" };

        Assert.Null(DisplayMatching.Find([Primary, sameSpot], Second.Id));
    }

    [Fact]
    public void Matching_is_ordinal_and_never_by_index()
    {
        Assert.Null(DisplayMatching.Find([Primary, Second], Second.Id.ToLowerInvariant()));
        Assert.Null(DisplayMatching.Find([Primary, Second], "1"));
        Assert.Null(DisplayMatching.Find([Primary, Second], null));
        Assert.Null(DisplayMatching.Find([], Second.Id));
    }

    [Fact]
    public void The_primary_display_is_found_by_its_flag()
    {
        Assert.Same(Primary, DisplayMatching.FindPrimary([Second, Primary]));
        Assert.Null(DisplayMatching.FindPrimary([Second]));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void One_display_is_always_single_screen(bool primary, bool useOperatorId)
    {
        var only = Display("only", primary: primary);

        Assert.True(DisplayRules.IsSingleScreen([only], "only", useOperatorId ? "only" : null));
        Assert.True(DisplayRules.IsSingleScreen([], "gone", null));
    }

    [Fact]
    public void Binding_the_display_with_the_Operator_window_is_single_screen()
    {
        Assert.True(DisplayRules.IsSingleScreen([Primary, Second], Second.Id, Second.Id));
        Assert.False(DisplayRules.IsSingleScreen([Primary, Second], Second.Id, Primary.Id));
    }

    [Fact]
    public void The_operator_window_display_wins_over_the_primary()
    {
        // The operator moved the Operator window to the second display: binding the primary is two screens.
        Assert.False(DisplayRules.IsSingleScreen([Primary, Second], Primary.Id, Second.Id));
    }

    [Fact]
    public void Without_an_Operator_window_the_primary_display_stands_in()
    {
        Assert.True(DisplayRules.IsSingleScreen([Primary, Second], Primary.Id, null));
        Assert.False(DisplayRules.IsSingleScreen([Primary, Second], Second.Id, null));
        Assert.False(DisplayRules.IsSingleScreen([Second, Display("third", x: 3840)], Second.Id, null));
    }

    [Fact]
    public void Rules_check_their_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => DisplayMatching.Find(null!, "id"));
        Assert.Throws<ArgumentNullException>(() => DisplayMatching.FindPrimary(null!));
        Assert.Throws<ArgumentNullException>(() => DisplayRules.IsSingleScreen(null!, "id", null));
        Assert.Throws<ArgumentNullException>(() => DisplayRules.IsSingleScreen([], null!, null));
    }

    [Fact]
    public void Displays_are_numbered_left_to_right_then_top_to_bottom_then_by_id()
    {
        var left = Display("b-left", x: -1920);
        var topMiddle = Display("c-top", x: 0, y: -1080);
        var middle = Display("a-middle", x: 0, y: 0);
        var tie = Display("a-tie", x: 0, y: 0);

        var ordered = DisplayRules.NumberingOrder([middle, topMiddle, Second, left, tie]);

        Assert.Equal(["b-left", "c-top", "a-middle", "a-tie", Second.Id], ordered.Select(display => display.Id));
        Assert.Throws<ArgumentNullException>(() => DisplayRules.NumberingOrder(null!));
    }
}

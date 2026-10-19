using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core.Tests.GuestCycles.Orchestration;

/// <summary>The Arrange tab's commands on the brain (the pure rules are the model's): Move, drag, selection, tabs, and Template changes that keep every Capture.</summary>
public sealed class GuestCycleArrangeTests
{
    private static string[] Ids(GuestCycleRig rig) => [.. rig.Photographs.Windows.Select(window => window.CaptureId!)];

    private static string Drag(int source, string captureId, long revision, int target) =>
        $$"""{"sourceIndex":{{source}},"captureId":"{{captureId}}","revision":{{revision}},"targetIndex":{{target}}}""";

    [Fact]
    public async Task Selecting_a_photograph_then_Move_and_a_destination_swaps_the_two()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var before = Ids(rig);
        var revision = rig.Photographs.Revision;

        rig.Guest("SelectPosition", """{"index":1}""").ShouldBeAccepted();
        Assert.Equal(new PhotographSelectionView(1, PhotographSelectionMode.Selected), rig.Photographs.Selection);

        rig.Guest("BeginMove", """{"index":0}""").ShouldBeAccepted();
        Assert.Equal(PhotographSelectionMode.Moving, rig.Photographs.Selection.Mode);
        Assert.Equal(0, rig.Photographs.Selection.Index);
        Assert.False(rig.Photographs.CanContinue);

        rig.Guest("MoveTo", """{"index":2}""").ShouldBeAccepted();
        Assert.Equal([before[2], before[1], before[0], before[3]], Ids(rig));
        Assert.Equal(revision + 1, rig.Photographs.Revision);
        Assert.Equal(new PhotographSelectionView(2, PhotographSelectionMode.Selected), rig.Photographs.Selection);
        Assert.True(rig.Photographs.CanContinue);
    }

    [Fact]
    public async Task A_Move_can_be_cancelled_and_leaves_the_arrangement_alone()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var before = Ids(rig);
        var revision = rig.Photographs.Revision;

        rig.Guest("BeginMove", """{"index":3}""").ShouldBeAccepted();
        rig.Guest("CancelMove").ShouldBeAccepted();

        Assert.Equal(before, Ids(rig));
        Assert.Equal(revision, rig.Photographs.Revision);
        Assert.NotEqual(PhotographSelectionMode.Moving, rig.Photographs.Selection.Mode);
        rig.Guest("CancelMove").ShouldBeRejected();
        rig.Guest("MoveTo", """{"index":1}""").ShouldBeRejected();
    }

    [Fact]
    public async Task Drag_does_the_same_as_Move_for_the_same_intent()
    {
        using var moved = await GuestCycleRig.CreateAsync();
        moved.ToArrange();
        using var dragged = await GuestCycleRig.CreateAsync();
        dragged.ToArrange();
        var ids = Ids(dragged);

        moved.Guest("BeginMove", """{"index":0}""").ShouldBeAccepted();
        moved.Guest("MoveTo", """{"index":3}""").ShouldBeAccepted();
        dragged.Guest("DragPlace", Drag(0, ids[0], dragged.Photographs.Revision, 3)).ShouldBeAccepted();

        Assert.Equal(Ids(moved).Select(id => ids.ToList().IndexOf(id)), Ids(dragged).Select(id => ids.ToList().IndexOf(id)));
        Assert.Equal(moved.Photographs.Revision, dragged.Photographs.Revision);
        Assert.Equal(moved.Photographs.Selection, dragged.Photographs.Selection);
    }

    [Fact]
    public async Task A_stale_drag_is_accepted_and_changes_nothing()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = Ids(rig);
        var revision = rig.Photographs.Revision;

        rig.Guest("DragPlace", Drag(0, ids[0], revision - 1, 2)).ShouldBeAccepted();
        rig.Guest("DragPlace", Drag(0, ids[1], revision, 2)).ShouldBeAccepted();

        Assert.Equal(ids, Ids(rig));
        Assert.Equal(revision, rig.Photographs.Revision);
    }

    [Fact]
    public async Task Dropping_a_photograph_on_its_own_window_keeps_the_revision()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = Ids(rig);
        var revision = rig.Photographs.Revision;

        rig.Guest("DragPlace", Drag(2, ids[2], revision, 2)).ShouldBeAccepted();

        Assert.Equal(revision, rig.Photographs.Revision);
        Assert.Equal(ids, Ids(rig));
    }

    [Fact]
    public async Task A_window_index_outside_the_Template_is_an_invalid_payload()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.ToArrange();
        var ids = Ids(rig);

        rig.Guest("SelectPosition", """{"index":4}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("BeginMove", """{"index":-1}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("MoveTo", """{"index":10}""").ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("DragPlace", Drag(0, ids[0], rig.Photographs.Revision, 4)).ShouldBeRejected(RejectReason.InvalidPayload);
        rig.Guest("DragPlace", Drag(-1, ids[0], rig.Photographs.Revision, 1)).ShouldBeRejected(RejectReason.InvalidPayload);
    }

    [Fact]
    public async Task Arrangement_commands_are_refused_off_the_Photographs_step_and_while_a_sequence_runs()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.Guest("SelectPosition", """{"index":0}""").ShouldBeRejected();
        rig.Guest("BeginMove", """{"index":0}""").ShouldBeRejected();
        rig.Guest("AcceptRetake").ShouldBeRejected();

        rig.ToPhotographs();
        rig.Guest("SelectPosition", """{"index":0}""").ShouldBeAccepted();
        rig.Guest("BeginMove", """{"index":0}""").ShouldBeRejected();
        rig.Guest("TakePhotographs").ShouldBeAccepted();
        rig.Guest("SelectPosition", """{"index":0}""").ShouldBeRejected();
    }

    [Fact]
    public async Task The_tabs_switch_only_when_the_model_allows_it()
    {
        using var rig = await GuestCycleRig.CreateAsync();
        rig.Begin();
        rig.ToPhotographs();
        rig.Guest("ShowArrangeTab").ShouldBeRejected();
        rig.Guest("ShowCameraTab").ShouldBeRejected();

        rig.ToArrange2();
        rig.Guest("ShowCameraTab").ShouldBeAccepted();
        Assert.Equal(PhotographsTabName.Camera, rig.Photographs.Tab);
        rig.Guest("ShowArrangeTab").ShouldBeAccepted();
        Assert.Equal(PhotographsTabName.Arrange, rig.Photographs.Tab);
    }
}

internal static class GuestCycleRigArrangeSupport
{
    /// <summary>Takes every photograph on a cycle already on the Camera tab.</summary>
    public static void ToArrange2(this GuestCycleRig rig) => rig.TakeAll();
}

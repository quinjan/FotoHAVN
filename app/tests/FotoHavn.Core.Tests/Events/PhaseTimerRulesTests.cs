using FotoHavn.Core.Events;
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;

namespace FotoHavn.Core.Tests.Events;

public sealed class PhaseTimerRulesTests
{
    public static TheoryData<PhaseTimerPhase, int, int, int, bool> Table => new()
    {
        { PhaseTimerPhase.Template, 30, 20, 300, true },
        { PhaseTimerPhase.PhotographsCapture, 30, 20, 300, true },
        { PhaseTimerPhase.PhotographsReview, 45, 20, 300, true },
        { PhaseTimerPhase.Filters, 30, 20, 300, true },
        { PhaseTimerPhase.Keepsake, 12, 5, 60, false },
        { PhaseTimerPhase.GuestGallery, 60, 20, 300, false },
    };

    public static TheoryData<PhaseTimerPhase> Phases => [.. PhaseTimerRules.All];

    [Theory]
    [MemberData(nameof(Table))]
    public void Each_phase_matches_the_plans_table(PhaseTimerPhase phase, int defaultSeconds, int min, int max, bool canBeOff)
    {
        Assert.Equal(defaultSeconds, PhaseTimerRules.Default(phase));
        Assert.Equal(min, PhaseTimerRules.Min(phase));
        Assert.Equal(max, PhaseTimerRules.Max(phase));
        Assert.Equal(canBeOff, PhaseTimerRules.CanBeOff(phase));
        Assert.True(PhaseTimerRules.IsValid(phase, defaultSeconds));
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void Range_edges_are_valid_and_one_beyond_is_not(PhaseTimerPhase phase, int defaultSeconds, int min, int max, bool canBeOff)
    {
        Assert.NotEqual(0, defaultSeconds);
        Assert.True(PhaseTimerRules.IsValid(phase, min));
        Assert.True(PhaseTimerRules.IsValid(phase, max));
        Assert.False(PhaseTimerRules.IsValid(phase, min - 1));
        Assert.False(PhaseTimerRules.IsValid(phase, max + 1));
        Assert.False(PhaseTimerRules.IsValid(phase, -1));
        Assert.Equal(canBeOff, PhaseTimerRules.IsValid(phase, 0));
    }

    [Fact]
    public void Only_the_four_guest_cycle_phases_can_be_off()
    {
        Assert.Equal(
            [PhaseTimerPhase.Template, PhaseTimerPhase.PhotographsCapture, PhaseTimerPhase.PhotographsReview, PhaseTimerPhase.Filters],
            PhaseTimerRules.All.Where(PhaseTimerRules.CanBeOff));
    }

    [Fact]
    public void The_warning_is_20_seconds_and_the_Keepsake_has_none()
    {
        Assert.Equal(20, PhaseTimerRules.WarningSeconds);
        Assert.All(PhaseTimerRules.All.Where(phase => phase != PhaseTimerPhase.Keepsake), phase => Assert.True(PhaseTimerRules.HasWarning(phase)));
        Assert.False(PhaseTimerRules.HasWarning(PhaseTimerPhase.Keepsake));
    }

    [Fact]
    public void The_shortest_timer_leaves_a_whole_warning_to_respond_to()
    {
        Assert.All(PhaseTimerRules.All.Where(PhaseTimerRules.HasWarning), phase => Assert.True(PhaseTimerRules.Min(phase) >= PhaseTimerRules.WarningSeconds));
    }

    [Fact]
    public void Defaults_list_every_phase_once_in_plan_order()
    {
        var defaults = PhaseTimerRules.Defaults();

        Assert.Equal(PhaseTimerRules.All, defaults.Select(setting => setting.Phase));
        Assert.Equal([30, 30, 45, 30, 12, 60], defaults.Select(setting => setting.Seconds));
        Assert.Equal(Enum.GetValues<PhaseTimerPhase>().Order(), PhaseTimerRules.All.Order());
    }

    [Theory]
    [MemberData(nameof(Phases))]
    public void Storage_and_wire_phases_map_both_ways(PhaseTimerPhase phase)
    {
        var wire = PhaseTimerRules.ToProtocol(phase);

        Assert.Equal(phase.ToString(), wire.ToString());
        Assert.Equal(phase, PhaseTimerRules.FromProtocol(wire));
    }

    [Fact]
    public void Wire_and_storage_have_the_same_phases()
    {
        Assert.Equal(Enum.GetNames<PhaseTimerPhase>(), Enum.GetNames<PhaseTimer>());
    }

    [Fact]
    public void Unknown_phases_are_refused()
    {
        var unknown = (PhaseTimerPhase)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.Default(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.Min(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.Max(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.CanBeOff(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.HasWarning(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.IsValid(unknown, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.ToProtocol(unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhaseTimerRules.FromProtocol((PhaseTimer)99));
    }
}

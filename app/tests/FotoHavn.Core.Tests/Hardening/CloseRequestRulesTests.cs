using FotoHavn.Core.Hardening;

namespace FotoHavn.Core.Tests.Hardening;

public sealed class CloseRequestRulesTests
{
    [Theory]
    [InlineData(false, CloseSource.OperatorWindow, CloseRequestOutcome.CloseNow)]
    [InlineData(false, CloseSource.GuestWindow, CloseRequestOutcome.CloseNow)]
    [InlineData(false, CloseSource.SessionEnd, CloseRequestOutcome.CloseNow)]
    [InlineData(true, CloseSource.OperatorWindow, CloseRequestOutcome.AskToEndEvent)]
    [InlineData(true, CloseSource.GuestWindow, CloseRequestOutcome.Refuse)]
    [InlineData(true, CloseSource.SessionEnd, CloseRequestOutcome.EndEventAndClose)]
    public void Every_row_of_the_close_rule(bool eventActive, CloseSource source, CloseRequestOutcome expected)
    {
        Assert.Equal(expected, CloseRequestRules.Decide(eventActive, source));
    }

    [Fact]
    public void Every_source_is_decided_for_both_states()
    {
        foreach (var source in Enum.GetValues<CloseSource>())
        {
            _ = CloseRequestRules.Decide(true, source);
            _ = CloseRequestRules.Decide(false, source);
        }
    }

    [Fact]
    public void Nothing_ends_a_running_Event_without_asking_except_Windows_ending_the_session()
    {
        var withoutAsking = Enum.GetValues<CloseSource>()
            .Where(source => CloseRequestRules.Decide(true, source) is CloseRequestOutcome.CloseNow or CloseRequestOutcome.EndEventAndClose)
            .ToList();

        Assert.Equal([CloseSource.SessionEnd], withoutAsking);
    }

    [Fact]
    public void A_source_that_is_not_one_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CloseRequestRules.Decide(true, (CloseSource)99));
    }
}

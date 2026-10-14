using FotoHavn.Core.Events;

namespace FotoHavn.Core.Tests.Events;

public sealed class PinRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1234", true)]
    [InlineData("12345", true)]
    [InlineData("123456", true)]
    [InlineData("0000", true)]
    [InlineData("123", false)]
    [InlineData("1234567", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("12 4", false)]
    [InlineData("12a4", false)]
    [InlineData("12-4", false)]
    [InlineData("١٢٣٤", false)]
    [InlineData("１２３４", false)]
    public void A_PIN_is_4_to_6_ascii_digits(string? pin, bool valid)
    {
        Assert.Equal(valid, PinRules.IsValidFormat(pin));
    }

    [Theory]
    [InlineData(null, null, EventSetupError.PinRequired)]
    [InlineData("", "", EventSetupError.PinRequired)]
    [InlineData("12", "12", EventSetupError.PinFormat)]
    [InlineData("12a4", "12a4", EventSetupError.PinFormat)]
    [InlineData("", "1234", EventSetupError.PinFormat)]
    [InlineData("1234", "", EventSetupError.PinMismatch)]
    [InlineData("1234", "1235", EventSetupError.PinMismatch)]
    [InlineData("1234", "12345", EventSetupError.PinMismatch)]
    [InlineData("1234", "1234", null)]
    [InlineData("123456", "123456", null)]
    public void A_PIN_is_checked_with_its_confirmation(string? pin, string? confirmation, EventSetupError? expected)
    {
        Assert.Equal(expected, PinRules.Check(pin, confirmation));
    }

    [Fact]
    public void The_limits_match_the_plan()
    {
        Assert.Equal((4, 6, 5), (PinRules.MinDigits, PinRules.MaxDigits, PinRules.MaxWrongAttempts));
        Assert.Equal(TimeSpan.FromSeconds(30), PinRules.LockoutDuration);
    }

    [Fact]
    public void A_fresh_lockout_allows_every_attempt()
    {
        var lockout = new PinLockout();

        Assert.False(lockout.IsLocked(Start));
        Assert.Equal(0, lockout.RemainingSeconds(Start));
        Assert.Equal(5, lockout.AttemptsRemaining(Start));
        Assert.Equal(0, lockout.WrongAttempts);
    }

    [Fact]
    public void Four_wrong_attempts_leave_one_and_the_fifth_locks_for_30_seconds()
    {
        var lockout = new PinLockout();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.False(lockout.RegisterWrong(Start));
            Assert.False(lockout.IsLocked(Start));
            Assert.Equal(5 - attempt, lockout.AttemptsRemaining(Start));
        }

        Assert.True(lockout.RegisterWrong(Start));
        Assert.True(lockout.IsLocked(Start));
        Assert.Equal(30, lockout.RemainingSeconds(Start));
        Assert.Equal(0, lockout.AttemptsRemaining(Start));
    }

    [Fact]
    public void The_remaining_seconds_count_down_and_round_up()
    {
        var lockout = Locked();

        Assert.Equal(30, lockout.RemainingSeconds(Start));
        Assert.Equal(30, lockout.RemainingSeconds(Start + TimeSpan.FromMilliseconds(1)));
        Assert.Equal(29, lockout.RemainingSeconds(Start + TimeSpan.FromSeconds(1)));
        Assert.Equal(1, lockout.RemainingSeconds(Start + TimeSpan.FromSeconds(29.5)));
        Assert.True(lockout.IsLocked(Start + TimeSpan.FromSeconds(29.999)));
    }

    [Fact]
    public void The_lockout_ends_at_exactly_30_seconds_and_the_count_starts_again()
    {
        var lockout = Locked();
        var end = Start + TimeSpan.FromSeconds(30);

        Assert.False(lockout.IsLocked(end));
        Assert.Equal(0, lockout.RemainingSeconds(end));
        Assert.Equal(5, lockout.AttemptsRemaining(end));

        Assert.False(lockout.RegisterWrong(end));
        Assert.Equal(1, lockout.WrongAttempts);
        Assert.Equal(4, lockout.AttemptsRemaining(end));
        Assert.False(lockout.IsLocked(end));
    }

    [Fact]
    public void Wrong_attempts_while_locked_change_nothing_and_never_extend_it()
    {
        var lockout = Locked();

        Assert.False(lockout.RegisterWrong(Start + TimeSpan.FromSeconds(10)));
        Assert.False(lockout.RegisterWrong(Start + TimeSpan.FromSeconds(29)));

        Assert.Equal(5, lockout.WrongAttempts);
        Assert.Equal(1, lockout.RemainingSeconds(Start + TimeSpan.FromSeconds(29)));
        Assert.False(lockout.IsLocked(Start + TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void A_second_lockout_follows_five_more_wrong_attempts()
    {
        var lockout = Locked();
        var later = Start + TimeSpan.FromSeconds(31);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.False(lockout.RegisterWrong(later));
        }

        Assert.True(lockout.RegisterWrong(later));
        Assert.Equal(30, lockout.RemainingSeconds(later));
    }

    [Fact]
    public void A_correct_PIN_clears_the_count()
    {
        var lockout = new PinLockout();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            lockout.RegisterWrong(Start);
        }

        lockout.RegisterCorrect();

        Assert.Equal(0, lockout.WrongAttempts);
        Assert.Equal(5, lockout.AttemptsRemaining(Start));
        Assert.False(lockout.RegisterWrong(Start));
        Assert.Equal(4, lockout.AttemptsRemaining(Start));
    }

    [Fact]
    public void A_correct_PIN_also_clears_a_finished_lockout()
    {
        var lockout = Locked();

        lockout.RegisterCorrect();

        Assert.False(lockout.IsLocked(Start));
        Assert.Equal(0, lockout.WrongAttempts);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2.4, false)]
    [InlineData(2.5, true)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void The_exit_hold_needs_about_three_seconds(double seconds, bool complete)
    {
        Assert.Equal(TimeSpan.FromSeconds(3), ExitHoldRules.RequiredHold);
        Assert.Equal(complete, ExitHoldRules.IsComplete(TimeSpan.FromSeconds(seconds)));
    }

    private static PinLockout Locked()
    {
        var lockout = new PinLockout();
        for (var attempt = 1; attempt <= PinRules.MaxWrongAttempts; attempt++)
        {
            lockout.RegisterWrong(Start);
        }

        return lockout;
    }
}

using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class BoardRotationRecoveryTests
{
    private static readonly DateTime T0 = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Journey_cannot_take_over_rotation_while_board_is_owned(bool boardOwnsRotation, bool allowed)
    {
        Assert.Equal(allowed, BoardRotationRecovery.JourneyMayChangeRotation(boardOwnsRotation));
    }

    [Fact]
    public void Never_runs_outside_a_managed_board()
    {
        var guard = new BoardRotationRecovery();
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0, false, true, false));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddMinutes(1), false, true, false));
        Assert.Equal(0, guard.Attempts);
    }

    [Fact]
    public void Verifies_autoduty_mode_once_even_when_rotation_is_already_active()
    {
        var guard = new BoardRotationRecovery();
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0, true, true, true));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(5), true, true, true));
        Assert.Equal(BoardRotationAction.EnsureAutoDutyMode, guard.Tick(T0.AddSeconds(6), true, true, true));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(7), true, true, true));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(90), true, true, true));
    }

    [Fact]
    public void Retries_a_failed_start_then_reports_exhaustion_once()
    {
        var guard = new BoardRotationRecovery();
        guard.Tick(T0, true, true, false);
        Assert.Equal(BoardRotationAction.EnsureAutoDutyMode, guard.Tick(T0.AddSeconds(6), true, true, false));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(15), true, true, false));
        Assert.Equal(BoardRotationAction.RetryAutoDutyMode, guard.Tick(T0.AddSeconds(16), true, true, false));
        Assert.Equal(BoardRotationAction.RetryAutoDutyMode, guard.Tick(T0.AddSeconds(26), true, true, false));
        Assert.Equal(BoardRotationAction.Exhausted, guard.Tick(T0.AddSeconds(36), true, true, false));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(50), true, true, false));
        Assert.Equal(3, guard.Attempts);
    }

    [Fact]
    public void Suppresses_recovery_during_cutscenes_and_when_player_not_ready()
    {
        var guard = new BoardRotationRecovery();
        guard.Tick(T0, true, false, null);
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(20), true, false, null));
        Assert.Equal(BoardRotationAction.EnsureAutoDutyMode, guard.Tick(T0.AddSeconds(21), true, true, null));
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(60), true, false, false));
        Assert.Equal(BoardRotationAction.RetryAutoDutyMode, guard.Tick(T0.AddSeconds(61), true, true, false));
    }

    [Fact]
    public void Resets_for_the_next_board_without_carrying_over_retry_count()
    {
        var guard = new BoardRotationRecovery();
        guard.Tick(T0, true, true, false);
        guard.Tick(T0.AddSeconds(6), true, true, false);
        guard.Tick(T0.AddSeconds(16), true, true, false);
        Assert.Equal(2, guard.Attempts);
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(20), false, true, false));
        Assert.Equal(0, guard.Attempts);
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(21), true, true, false));
        Assert.Equal(BoardRotationAction.EnsureAutoDutyMode, guard.Tick(T0.AddSeconds(27), true, true, false));
        Assert.Equal(1, guard.Attempts);
    }

    [Fact]
    public void Recovers_if_rotation_drops_after_being_verified_active()
    {
        var guard = new BoardRotationRecovery();
        guard.Tick(T0, true, true, false);
        guard.Tick(T0.AddSeconds(6), true, true, false);
        guard.Tick(T0.AddSeconds(7), true, true, true);
        Assert.Equal(0, guard.Attempts);
        Assert.Equal(BoardRotationAction.None, guard.Tick(T0.AddSeconds(10), true, true, false));
        Assert.Equal(BoardRotationAction.RetryAutoDutyMode, guard.Tick(T0.AddSeconds(17), true, true, false));
        Assert.Equal(1, guard.Attempts);
    }
}

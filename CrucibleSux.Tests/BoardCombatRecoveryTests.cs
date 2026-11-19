using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class BoardCombatRecoveryTests
{
    private static readonly DateTime T0 = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    private readonly BoardCombatRecovery recovery = new();

    private BoardCombatAction Tick(int seconds, bool owned = true, bool ready = true,
        bool combat = false, bool pet = false, bool horn = true, bool usable = true,
        bool nearby = false, bool rotation = true, bool attacking = false) =>
        this.recovery.Tick(T0.AddSeconds(seconds), owned, ready, combat,
            pet, horn, usable, nearby, rotation, attacking);

    [Fact]
    public void Does_nothing_outside_an_owned_board_or_during_a_cutscene()
    {
        Assert.Equal(BoardCombatAction.None, this.Tick(0, owned: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(1, ready: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(7, ready: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(8));
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(11));
    }

    [Fact]
    public void Calls_first_horn_with_bounded_retries_before_any_attack()
    {
        Assert.Equal(BoardCombatAction.None, this.Tick(0, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(2, nearby: true));
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(3, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(4, nearby: true));
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(8, nearby: true));
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(13, nearby: true));
        Assert.Equal(BoardCombatAction.SummonFailed, this.Tick(18, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(23, nearby: true));
    }

    [Fact]
    public void Requires_a_registered_usable_horn_and_handles_cooldown()
    {
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(47, horn: false));
        Assert.Equal(BoardCombatAction.NoBattlehorn, this.Tick(48, horn: false));
        this.recovery.Reset();
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, usable: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(32, usable: false));
        Assert.Equal(BoardCombatAction.HornUnavailable, this.Tick(33, usable: false));
    }

    [Fact]
    public void A_team_loaded_before_the_horns_does_not_trigger_a_premature_abort()
    {
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(20, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(40, horn: false));
        // ActionManager finally exposes the assigned horn; the summon can proceed.
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(41));
    }

    [Fact]
    public void A_long_duty_loading_period_does_not_consume_the_assignment_grace()
    {
        this.Tick(0, ready: false, horn: false);
        this.Tick(100, ready: false, horn: false);
        Assert.Equal(BoardCombatAction.None, this.Tick(101, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(105, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(140, horn: false));
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(141));
    }

    [Fact]
    public void Missing_assignment_does_not_consume_the_horn_cooldown_window()
    {
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, horn: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(40, horn: false));
        // A horn finally appears but remains unusable: start a fresh 30-second clock.
        Assert.Equal(BoardCombatAction.None, this.Tick(41, usable: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(70, usable: false));
        Assert.Equal(BoardCombatAction.HornUnavailable, this.Tick(71, usable: false));
    }

    [Fact]
    public void Usable_horn_resets_a_previous_cooldown_window()
    {
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, usable: false));
        // A usable horn clears the prior cooldown deadline even if the summoned
        // familiar has not yet appeared; a later cooldown gets a full 30 seconds.
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(10, usable: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(11, usable: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(40, usable: false));
        Assert.Equal(BoardCombatAction.HornUnavailable, this.Tick(41, usable: false));
    }

    [Fact]
    public void Requires_a_familiar_active_rotation_and_stable_melee_target()
    {
        Assert.Equal(BoardCombatAction.None, this.Tick(0, pet: true, nearby: true, rotation: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(4, pet: true, nearby: true, rotation: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(6, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.EngageNearbyHostile, this.Tick(7, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(8, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(10, pet: true, nearby: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(11, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.EngageNearbyHostile, this.Tick(14, pet: true, nearby: true));
    }

    [Fact]
    public void Never_toggles_auto_attack_while_it_is_on()
    {
        this.Tick(0, pet: true);
        this.Tick(1, pet: true, nearby: true);
        Assert.Equal(BoardCombatAction.None, this.Tick(4, pet: true, nearby: true, attacking: true));
        Assert.Equal(BoardCombatAction.EngagementStalled, this.Tick(16, pet: true, nearby: true, attacking: true));
    }

    [Fact]
    public void Combat_and_board_exit_cancel_unsafe_rescue()
    {
        this.Tick(0, pet: true);
        this.Tick(4, pet: true, nearby: true);
        Assert.Equal(BoardCombatAction.None, this.Tick(8, pet: true, nearby: true, combat: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(10, owned: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(11, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.None, this.Tick(13, pet: true, nearby: true));
        Assert.Equal(BoardCombatAction.EngageNearbyHostile, this.Tick(14, pet: true, nearby: true));
    }

    [Fact]
    public void A_normal_summon_clears_retries_and_preserves_ownership()
    {
        this.Tick(0);
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(3));
        Assert.Equal(BoardCombatAction.None, this.Tick(4, pet: true));
        Assert.Equal(0, this.recovery.SummonAttempts);
        Assert.Equal(BoardCombatAction.None, this.Tick(7, pet: true, nearby: true, rotation: false));
    }
}

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
        Assert.Equal(BoardCombatAction.SummonFirstHorn, this.Tick(8));
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
        Assert.Equal(BoardCombatAction.NoBattlehorn, this.Tick(3, horn: false));
        this.recovery.Reset();
        this.Tick(0);
        Assert.Equal(BoardCombatAction.None, this.Tick(3, usable: false));
        Assert.Equal(BoardCombatAction.None, this.Tick(32, usable: false));
        Assert.Equal(BoardCombatAction.HornUnavailable, this.Tick(33, usable: false));
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

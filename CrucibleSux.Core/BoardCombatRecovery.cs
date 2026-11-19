namespace CrucibleSux.Core;

/// <summary>The safe actions Leveler may take when AutoDuty controls a Crucible board.</summary>
public enum BoardCombatAction
{
    None,
    SummonFirstHorn,
    EngageNearbyHostile,
    NoBattlehorn,
    HornUnavailable,
    SummonFailed,
    EngagementStalled,
}

/// <summary>
/// Provides the missing first-familiar/initial-engagement handoff for board runs.
/// AutoDuty retains control of movement and the path; Leveler intervenes only
/// after the board has loaded, with explicit grace periods and bounded attempts.
/// Pure logic makes the safety conditions and timing testable without Dalamud.
/// </summary>
public sealed class BoardCombatRecovery
{
    public static readonly TimeSpan EntryGrace = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan SummonRetry = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan HornCooldownLimit = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan NearbyDwell = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan EngageRetry = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan AlreadyAttackingLimit = TimeSpan.FromSeconds(15);
    public const int MaxSummonAttempts = 3;
    public const int MaxEngageAttempts = 3;

    private DateTime? enteredAt;
    private DateTime nextSummon;
    private DateTime? unableToSummonSince;
    private DateTime? nearbySince;
    private DateTime nextEngage;
    private bool completed;

    public int SummonAttempts { get; private set; }
    public int EngageAttempts { get; private set; }

    public void Reset()
    {
        this.enteredAt = null;
        this.nextSummon = default;
        this.unableToSummonSince = null;
        this.nearbySince = null;
        this.nextEngage = default;
        this.SummonAttempts = 0;
        this.EngageAttempts = 0;
        this.completed = false;
    }

    public BoardCombatAction Tick(
        DateTime now, bool boardOwned, bool playerReady, bool inCombat,
        bool familiarPresent, bool hornAssigned, bool hornUsable,
        bool hostileInMeleeRange, bool rotationActive, bool autoAttackActive)
    {
        if (!boardOwned)
        {
            this.Reset();
            return BoardCombatAction.None;
        }

        this.enteredAt ??= now;
        if (!playerReady || this.completed)
            return BoardCombatAction.None;

        if (inCombat)
        {
            // A successful pull completes the rescue. The next battle can be handled
            // by RSR/AutoDuty; subsequent out-of-combat calls still check the pet.
            this.nearbySince = null;
            this.EngageAttempts = 0;
            return BoardCombatAction.None;
        }

        if (!familiarPresent)
        {
            this.nearbySince = null;
            this.EngageAttempts = 0;
            if (now < this.enteredAt.Value + EntryGrace)
                return BoardCombatAction.None;
            if (!hornAssigned)
            {
                this.completed = true;
                return BoardCombatAction.NoBattlehorn;
            }
            if (!hornUsable)
            {
                this.unableToSummonSince ??= now;
                if (now - this.unableToSummonSince >= HornCooldownLimit)
                {
                    this.completed = true;
                    return BoardCombatAction.HornUnavailable;
                }
                return BoardCombatAction.None;
            }
            this.unableToSummonSince = null;
            if (now < this.nextSummon)
                return BoardCombatAction.None;
            if (this.SummonAttempts >= MaxSummonAttempts)
            {
                this.completed = true;
                return BoardCombatAction.SummonFailed;
            }
            this.SummonAttempts++;
            this.nextSummon = now + SummonRetry;
            return BoardCombatAction.SummonFirstHorn;
        }

        this.SummonAttempts = 0;
        this.unableToSummonSince = null;
        if (!hostileInMeleeRange || !rotationActive)
        {
            this.nearbySince = null;
            this.EngageAttempts = 0;
            return BoardCombatAction.None;
        }

        this.nearbySince ??= now;
        if (now < this.nearbySince.Value + NearbyDwell)
            return BoardCombatAction.None;

        // Never re-toggle auto-attack when it is already on; instead report a
        // genuine stall if combat still fails to start.
        if (autoAttackActive)
        {
            if (now - this.nearbySince >= AlreadyAttackingLimit)
            {
                this.completed = true;
                return BoardCombatAction.EngagementStalled;
            }
            return BoardCombatAction.None;
        }

        if (now < this.nextEngage)
            return BoardCombatAction.None;
        if (this.EngageAttempts >= MaxEngageAttempts)
        {
            this.completed = true;
            return BoardCombatAction.EngagementStalled;
        }
        this.EngageAttempts++;
        this.nextEngage = now + EngageRetry;
        return BoardCombatAction.EngageNearbyHostile;
    }
}

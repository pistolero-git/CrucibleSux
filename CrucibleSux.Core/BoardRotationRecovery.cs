namespace CrucibleSux.Core;

/// <summary>Decisions for the board-owned RSR handshake. No plugin APIs are needed for testing.</summary>
public enum BoardRotationAction
{
    None,
    EnsureAutoDutyMode,
    RetryAutoDutyMode,
    Exhausted,
}

/// <summary>
/// A bounded, per-board handshake. AutoDuty gets time to initialize on its own; once the player
/// has entered the board we assert the SAME AutoDuty mode (not Manual or Auto) and verify it.
/// If the rotation later drops out, retry with a delay. Never drive the rotation outside a
/// running Crucible board, or while the character is unavailable or in a cutscene.
/// </summary>
public sealed class BoardRotationRecovery
{
    public static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);
    public const int MaxAttempts = 3;

    private DateTime? enteredUtc;
    private DateTime nextAttemptUtc;
    private bool ensured;
    private bool exhausted;

    public int Attempts { get; private set; }

    /// <summary>Journey must not override RSR while a managed board owns the rotation.</summary>
    public static bool JourneyMayChangeRotation(bool boardOwnsRotation) => !boardOwnsRotation;

    public void Reset()
    {
        this.enteredUtc = null;
        this.nextAttemptUtc = default;
        this.ensured = false;
        this.exhausted = false;
        this.Attempts = 0;
    }

    public BoardRotationAction Tick(DateTime now, bool boardRunning, bool playerReady, bool? rsrActive)
    {
        if (!boardRunning)
        {
            this.Reset();
            return BoardRotationAction.None;
        }

        if (this.enteredUtc is null)
        {
            this.enteredUtc = now;
            this.nextAttemptUtc = now + StartupGrace;
            return BoardRotationAction.None;
        }

        // Confirm AutoDuty mode once per board even when autorotation is already active:
        // the public Active IPC reports a boolean, not whether the mode is AutoDuty.
        if (!this.ensured)
        {
            if (!playerReady || now < this.nextAttemptUtc)
                return BoardRotationAction.None;

            this.ensured = true;
            this.Attempts = 1;
            this.nextAttemptUtc = now + RetryInterval;
            return BoardRotationAction.EnsureAutoDutyMode;
        }

        if (rsrActive == true)
        {
            // Once verified, a later dropout gets its own bounded recovery allowance.
            this.Attempts = 0;
            this.exhausted = false;
            this.nextAttemptUtc = now + RetryInterval;
            return BoardRotationAction.None;
        }

        if (!playerReady || now < this.nextAttemptUtc || this.exhausted)
            return BoardRotationAction.None;

        if (this.Attempts >= MaxAttempts)
        {
            this.exhausted = true;
            return BoardRotationAction.Exhausted;
        }

        this.Attempts++;
        this.nextAttemptUtc = now + RetryInterval;
        return BoardRotationAction.RetryAutoDutyMode;
    }
}

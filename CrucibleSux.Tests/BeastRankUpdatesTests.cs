using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class BeastRankUpdatesTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void An_actual_rank_replaces_an_incorrect_old_estimate()
    {
        var old = new BeastProgress(1, 22, 70, true, Now.AddDays(-1));

        var updated = BeastRankUpdates.Merge(1, 12, 35, old, Now);

        Assert.Equal(12, updated?.Rank);
        Assert.Equal(35, updated?.Exp);
        Assert.False(updated!.RankIsEstimated);
    }

    [Fact]
    public void Synced_party_rank_does_not_overwrite_a_higher_known_rank()
    {
        var old = new BeastProgress(1, 20, 48, false, Now.AddHours(-1));

        Assert.Null(BeastRankUpdates.Merge(1, 5, null, old, Now, authoritative: false));
        Assert.Null(BeastRankUpdates.Merge(1, 5, 12, old, Now));
    }

    [Fact]
    public void Team_reading_preserves_exp_until_the_board_result_updates_it()
    {
        var old = new BeastProgress(1, 9, 64, false, Now.AddHours(-1));
        Assert.Null(BeastRankUpdates.Merge(1, 9, null, old, Now, authoritative: false));

        var result = BeastRankUpdates.Merge(1, 10, 7, old, Now);
        Assert.Equal(10, result?.Rank);
        Assert.Equal(7, result?.Exp);
    }

    [Fact]
    public void Bad_or_unreadable_rank_cannot_change_saved_progress()
    {
        var old = new BeastProgress(1, 10, 20, false, Now);

        Assert.Null(BeastRankUpdates.Merge(1, null, 99, old, Now));
        Assert.Null(BeastRankUpdates.Merge(1, 26, 99, old, Now));
        Assert.Null(BeastRankUpdates.Merge(1, 0, 99, old, Now));
    }
}

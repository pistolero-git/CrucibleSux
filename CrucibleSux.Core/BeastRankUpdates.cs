namespace CrucibleSux.Core;

/// <summary>Rules for merging observed beast ranks from the Bestiary, registered team, and board results.</summary>
public static class BeastRankUpdates
{
    public const int MaxRank = 25;

    /// <summary>
    /// Returns a changed direct observation, or null for invalid, duplicate or regressing readings.
    /// Team display ranks may be synced to a board; only authoritative Bestiary/result readings
    /// may correct a previously estimated rank downwards.
    /// </summary>
    public static BeastProgress? Merge(uint id, int? rank, int? exp, BeastProgress? previous,
        DateTime seenUtc, bool authoritative = true)
    {
        if (rank is not (>= 1 and <= MaxRank))
            return null;
        if (previous?.Rank > rank && (!previous.RankIsEstimated || !authoritative))
            return null;

        int? effectiveExp = exp is >= 0 and <= 100 ? exp : previous?.Rank == rank ? previous.Exp : null;
        if (previous?.Rank == rank && previous.Exp == effectiveExp && !previous.RankIsEstimated)
            return null;

        return new BeastProgress(id, rank, effectiveExp, false, seenUtc);
    }
}

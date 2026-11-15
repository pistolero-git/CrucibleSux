using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class CarryLevelingTests
{
    [Fact]
    public void Carry_leveling_uses_AutoDuty_three_carriers_and_lower_ranked_beasts()
    {
        var options = AutoDutyOverrides.For("BossMod", RunOptions.Default with { CarryLeveling = true });

        Assert.Equal("Leveling", options["Meta.Crucible.TeamMode"]);
        Assert.Equal("Carry_Minus_2", options["Meta.Crucible.LevelingMode"]);
        Assert.False(options.ContainsKey("Meta.Crucible.RecommendedTeamCap"));
        Assert.Equal("false", options["Meta.Crucible.Shop"]);
    }

    [Fact]
    public void Carry_leveling_takes_precedence_over_the_legacy_team_setting()
    {
        var options = AutoDutyOverrides.For("BossMod",
            RunOptions.Default with { TeamOfThree = false, CarryLeveling = true });

        Assert.Equal("Leveling", options["Meta.Crucible.TeamMode"]);
        Assert.Equal("Carry_Minus_2", options["Meta.Crucible.LevelingMode"]);
    }

    [Fact]
    public void Default_still_uses_three_beasts_for_scoring()
    {
        var options = AutoDutyOverrides.For("BossMod", RunOptions.Default);

        Assert.Equal("Recommended", options["Meta.Crucible.TeamMode"]);
        Assert.Equal("3", options["Meta.Crucible.RecommendedTeamCap"]);
        Assert.False(options.ContainsKey("Meta.Crucible.LevelingMode"));
    }
}

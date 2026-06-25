using Palpitao.Api.Enums;
using Palpitao.Api.Services.Scoring;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

/// <summary>
/// FIFA World Cup scoring: same exact-score categories as England, but phase-based
/// multipliers plus the knockout classic (campeãs mundiais) doubling.
/// </summary>
public class WorldCupScoringTests
{
    private readonly ScoringService _s = new();

    // Default World Cup ruleset — proves the defaults reproduce the historical numbers.
    private static readonly ScoringRuleSet Rules =
        ScoringDefaults.ForTournamentType(TournamentType.FifaWorldCup, new HashSet<Guid>());

    // --- Phase multipliers (no classic) ------------------------------------

    [Theory]
    [InlineData(MatchPhase.WorldCupGroupStage, 1)]
    [InlineData(MatchPhase.WorldCupRoundOf32, 2)]
    [InlineData(MatchPhase.WorldCupRoundOf16, 2)]
    [InlineData(MatchPhase.WorldCupQuarterFinal, 3)]
    [InlineData(MatchPhase.WorldCupSemiFinal, 3)]
    [InlineData(MatchPhase.WorldCupThirdPlace, 3)]
    [InlineData(MatchPhase.WorldCupFinal, 3)]
    public void Phase_multipliers(MatchPhase phase, int expected)
        => Assert.Equal(expected, _s.GetMultiplier(Rules, Competition.FifaWorldCup, phase, false, false));

    // --- Classics (both world champions) double in the knockout -------------

    [Fact]
    public void Brazil_vs_Germany_group_stage_is_not_doubled()
        => Assert.Equal(1, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupGroupStage, true, true));

    [Fact]
    public void Brazil_vs_Germany_round_of_32_is_x4()
        => Assert.Equal(4, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupRoundOf32, true, true));

    [Fact]
    public void Argentina_vs_France_round_of_16_is_x4()
        => Assert.Equal(4, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupRoundOf16, true, true));

    [Fact]
    public void Brazil_vs_England_quarter_final_is_x6()
        => Assert.Equal(6, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupQuarterFinal, true, true));

    [Fact]
    public void Spain_vs_Uruguay_final_is_x6()
        => Assert.Equal(6, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupFinal, true, true));

    [Fact]
    public void Brazil_vs_Japan_quarter_final_is_x3_because_japan_is_not_a_champion()
        => Assert.Equal(3, _s.GetMultiplier(Rules, Competition.FifaWorldCup, MatchPhase.WorldCupQuarterFinal, true, false));

    // --- Full points = base * multiplier (categories identical to England) --

    [Fact]
    public void Group_stage_column_only_is_1()
        => Assert.Equal(1, Final(1, 0, 2, 0, MatchPhase.WorldCupGroupStage)); // home column, not exact

    [Fact]
    public void Group_stage_exact_1x1_is_3()
        => Assert.Equal(3, Final(1, 1, 1, 1, MatchPhase.WorldCupGroupStage)); // Traditional x1

    [Fact]
    public void Round_of_32_traditional_is_6()
        => Assert.Equal(6, Final(1, 0, 1, 0, MatchPhase.WorldCupRoundOf32)); // Traditional(3) x2

    [Fact]
    public void Round_of_16_medium_is_10()
        => Assert.Equal(10, Final(0, 0, 0, 0, MatchPhase.WorldCupRoundOf16)); // Medium(5) x2

    [Fact]
    public void Quarter_final_column_only_is_3()
        => Assert.Equal(3, Final(1, 0, 3, 1, MatchPhase.WorldCupQuarterFinal)); // column x3

    [Fact]
    public void Semi_final_uncommon_is_21()
        => Assert.Equal(21, Final(3, 2, 3, 2, MatchPhase.WorldCupSemiFinal)); // Uncommon(7) x3

    [Fact]
    public void Third_place_extra_uncommon_is_30()
        => Assert.Equal(30, Final(5, 0, 5, 0, MatchPhase.WorldCupThirdPlace)); // ExtraUncommon(10) x3

    [Fact]
    public void Final_traditional_is_9()
        => Assert.Equal(9, Final(1, 0, 1, 0, MatchPhase.WorldCupFinal)); // Traditional(3) x3

    private int Final(int ph, int pa, int ah, int aa, MatchPhase phase)
        => _s.ScorePrediction(Rules, ph, pa, ah, aa, Competition.FifaWorldCup, phase, false, false).FinalPoints;
}

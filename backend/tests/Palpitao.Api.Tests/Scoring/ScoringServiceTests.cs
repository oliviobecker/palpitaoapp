using Palpitao.Api.Enums;
using Palpitao.Api.Services.Scoring;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

public class ScoringServiceTests
{
    private readonly ScoringService _sut = new();

    // Default England ruleset — proves the defaults reproduce the historical numbers.
    private static readonly ScoringRuleSet Rules =
        ScoringDefaults.ForTournamentType(TournamentType.PalpitaoEngland, new Dictionary<Guid, Competition>());

    // Two classic groups, as the England defaults set them up.
    private static readonly Guid Arsenal = Guid.NewGuid();
    private static readonly Guid Chelsea = Guid.NewGuid();
    private static readonly Guid Millwall = Guid.NewGuid();
    private static readonly Guid WestHam = Guid.NewGuid();
    private static readonly Guid Everton = Guid.NewGuid();

    private static readonly ScoringRuleSet Grouped =
        ScoringDefaults.ForTournamentType(TournamentType.PalpitaoEngland, new Dictionary<Guid, Competition>
        {
            [Arsenal] = Competition.PremierLeague,
            [Chelsea] = Competition.PremierLeague,
            [Millwall] = Competition.Championship,
            [WestHam] = Competition.Championship,
        });

    // -----------------------------------------------------------------------
    // Exact score categories
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData(1, 1, ScoreCategory.Traditional, 3)]
    [InlineData(1, 0, ScoreCategory.Traditional, 3)]
    [InlineData(0, 1, ScoreCategory.Traditional, 3)]
    [InlineData(2, 2, ScoreCategory.Medium, 5)]
    [InlineData(3, 1, ScoreCategory.Medium, 5)]
    [InlineData(3, 2, ScoreCategory.Uncommon, 7)]
    [InlineData(4, 2, ScoreCategory.Uncommon, 7)]
    [InlineData(5, 0, ScoreCategory.ExtraUncommon, 10)]
    [InlineData(4, 3, ScoreCategory.ExtraUncommon, 10)]
    public void GetExactScoreCategory_returns_expected_category_and_points(
        int home, int away, ScoreCategory expectedCategory, int expectedPoints)
    {
        var category = _sut.GetExactScoreCategory(Rules, home, away);

        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedPoints, _sut.GetBasePoints(Rules, category));
    }

    // -----------------------------------------------------------------------
    // Column / exact / wrong — real result 2x1
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData(1, 0, ScoreCategory.ColumnOnly, 1)] // correct column (home win)
    [InlineData(2, 1, ScoreCategory.Traditional, 3)] // exact score
    [InlineData(1, 1, ScoreCategory.None, 0)] // wrong (draw)
    public void Scoring_against_result_2x1(int predHome, int predAway, ScoreCategory expectedCategory, int expectedPoints)
    {
        var category = _sut.GetCategory(Rules, predHome, predAway, 2, 1);

        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedPoints, _sut.GetBasePoints(Rules, category));
    }

    // -----------------------------------------------------------------------
    // Column / exact / wrong — real result 1x1
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData(0, 0, ScoreCategory.ColumnOnly, 1)] // correct column (draw)
    [InlineData(1, 1, ScoreCategory.Traditional, 3)] // exact score
    [InlineData(1, 2, ScoreCategory.None, 0)] // wrong (away win)
    public void Scoring_against_result_1x1(int predHome, int predAway, ScoreCategory expectedCategory, int expectedPoints)
    {
        var category = _sut.GetCategory(Rules, predHome, predAway, 1, 1);

        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedPoints, _sut.GetBasePoints(Rules, category));
    }

    // -----------------------------------------------------------------------
    // Columns
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData(2, 1, ScoreColumn.Home)]
    [InlineData(1, 1, ScoreColumn.Draw)]
    [InlineData(0, 3, ScoreColumn.Away)]
    public void GetColumn_returns_expected(int home, int away, ScoreColumn expected)
    {
        Assert.Equal(expected, _sut.GetColumn(home, away));
    }

    // -----------------------------------------------------------------------
    // Multipliers
    // -----------------------------------------------------------------------
    [Fact]
    public void PremierLeague_BigSeven_derby_is_double()
    {
        // Arsenal x Chelsea (both Big Seven)
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.PremierLeague, MatchPhase.Regular, isClassicPair: true));
    }

    [Fact]
    public void PremierLeague_non_BigSeven_is_single()
    {
        // Arsenal x Everton (away not Big Seven)
        Assert.Equal(1, _sut.GetMultiplier(Rules, Competition.PremierLeague, MatchPhase.Regular, isClassicPair: false));
    }

    [Fact]
    public void FACup_semifinal_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupSemiFinal, isClassicPair: false));
    }

    [Fact]
    public void FACup_final_is_triple()
    {
        Assert.Equal(3, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupFinal, isClassicPair: false));
    }

    [Fact]
    public void FACup_derby_in_regular_phase_is_double()
    {
        // Arsenal x Chelsea in the FA Cup regular phase
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.Regular, isClassicPair: true));
    }

    [Fact]
    public void FACup_final_derby_uses_phase_only_no_accumulation()
    {
        // Arsenal x Chelsea in the FA Cup final -> phase wins (3), not 3*2
        Assert.Equal(3, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupFinal, isClassicPair: true));
    }

    [Fact]
    public void Championship_normal_is_single()
    {
        Assert.Equal(1, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.Regular, isClassicPair: false));
    }

    [Fact]
    public void Championship_derby_is_double()
    {
        // Millwall x West Ham (both in the Championship classic group)
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.Regular, isClassicPair: true));
    }

    [Fact]
    public void Championship_playoff_semifinal_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.PlayoffSemiFinal, isClassicPair: false));
    }

    [Fact]
    public void Championship_playoff_final_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.PlayoffFinal, isClassicPair: false));
    }

    [Fact]
    public void Championship_playoff_derby_uses_phase_only_no_accumulation()
    {
        // The playoff row is (2, 2): the classic does not stack on top of the phase.
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.PlayoffFinal, isClassicPair: true));
    }

    [Fact]
    public void LeagueOne_is_always_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.LeagueOne, MatchPhase.Regular, isClassicPair: false));
    }

    // -----------------------------------------------------------------------
    // Classic pairs: same group only, whatever the competition
    // -----------------------------------------------------------------------
    [Fact]
    public void Same_group_teams_are_a_classic_pair()
    {
        Assert.True(Grouped.IsClassicPair(Arsenal, Chelsea));
        Assert.True(Grouped.IsClassicPair(Millwall, WestHam));
    }

    [Fact]
    public void Teams_from_different_groups_are_never_a_classic_pair()
    {
        // West Ham (Championship group) against Arsenal (Premier League group) in the FA Cup:
        // both are classic-eligible, but not with each other.
        Assert.False(Grouped.IsClassicPair(WestHam, Arsenal));
        Assert.Equal(1, _sut.GetMultiplier(
            Grouped, Competition.FACup, MatchPhase.Regular, Grouped.IsClassicPair(WestHam, Arsenal)));
    }

    [Fact]
    public void A_team_outside_the_classic_list_never_pairs()
    {
        Assert.False(Grouped.IsClassicPair(Arsenal, Everton));
    }

    [Fact]
    public void Championship_pair_also_doubles_in_the_FA_Cup()
    {
        // The group decides the pair; the match's competition decides the multiplier row.
        Assert.Equal(2, _sut.GetMultiplier(
            Grouped, Competition.FACup, MatchPhase.Regular, Grouped.IsClassicPair(Millwall, WestHam)));
    }

    // -----------------------------------------------------------------------
    // Final points = base * multiplier (integration)
    // -----------------------------------------------------------------------
    [Fact]
    public void ScorePrediction_applies_multiplier_to_exact_score()
    {
        // Arsenal x Chelsea (PL classic, x2), exact 2x1 (Traditional = 3) -> 6
        var result = _sut.ScorePrediction(
            Rules, 2, 1, 2, 1, Competition.PremierLeague, MatchPhase.Regular, isClassicPair: true);

        Assert.True(result.IsExactScore);
        Assert.True(result.IsCorrectColumn);
        Assert.Equal(ScoreCategory.Traditional, result.Category);
        Assert.Equal(3, result.BasePoints);
        Assert.Equal(2, result.Multiplier);
        Assert.Equal(6, result.FinalPoints);
    }

    [Fact]
    public void ScorePrediction_wrong_is_zero_even_with_multiplier()
    {
        // League One (x2) but wrong column/score -> 0
        var result = _sut.ScorePrediction(
            Rules, 0, 1, 2, 0, Competition.LeagueOne, MatchPhase.Regular, isClassicPair: false);

        Assert.False(result.IsCorrectColumn);
        Assert.Equal(ScoreCategory.None, result.Category);
        Assert.Equal(0, result.BasePoints);
        Assert.Equal(2, result.Multiplier);
        Assert.Equal(0, result.FinalPoints);
    }
}

using Palpitao.Api.Enums;
using Palpitao.Api.Services.Scoring;
using Xunit;

namespace Palpitao.Api.Tests.Scoring;

public class ScoringServiceTests
{
    private readonly ScoringService _sut = new();

    // Default England ruleset — proves the defaults reproduce the historical numbers.
    private static readonly ScoringRuleSet Rules =
        ScoringDefaults.ForTournamentType(TournamentType.PalpitaoEngland, new HashSet<Guid>());

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
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.PremierLeague, MatchPhase.Regular, true, true));
    }

    [Fact]
    public void PremierLeague_non_BigSeven_is_single()
    {
        // Arsenal x Everton (away not Big Seven)
        Assert.Equal(1, _sut.GetMultiplier(Rules, Competition.PremierLeague, MatchPhase.Regular, true, false));
    }

    [Fact]
    public void FACup_semifinal_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupSemiFinal, false, false));
    }

    [Fact]
    public void FACup_final_is_triple()
    {
        Assert.Equal(3, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupFinal, false, false));
    }

    [Fact]
    public void FACup_BigSeven_derby_in_regular_phase_is_double()
    {
        // Arsenal x Chelsea in the FA Cup regular phase
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.Regular, true, true));
    }

    [Fact]
    public void FACup_final_BigSeven_derby_uses_phase_only_no_accumulation()
    {
        // Arsenal x Chelsea in the FA Cup final -> phase wins (3), not 3*2
        Assert.Equal(3, _sut.GetMultiplier(Rules, Competition.FACup, MatchPhase.FACupFinal, true, true));
    }

    [Fact]
    public void Championship_normal_is_single()
    {
        Assert.Equal(1, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.Regular, false, false));
    }

    [Fact]
    public void Championship_playoff_semifinal_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.PlayoffSemiFinal, false, false));
    }

    [Fact]
    public void Championship_playoff_final_is_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.Championship, MatchPhase.PlayoffFinal, false, false));
    }

    [Fact]
    public void LeagueOne_is_always_double()
    {
        Assert.Equal(2, _sut.GetMultiplier(Rules, Competition.LeagueOne, MatchPhase.Regular, false, false));
    }

    // -----------------------------------------------------------------------
    // Final points = base * multiplier (integration)
    // -----------------------------------------------------------------------
    [Fact]
    public void ScorePrediction_applies_multiplier_to_exact_score()
    {
        // Arsenal x Chelsea (PL classic, x2), exact 2x1 (Traditional = 3) -> 6
        var result = _sut.ScorePrediction(Rules, 2, 1, 2, 1, Competition.PremierLeague, MatchPhase.Regular, true, true);

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
        var result = _sut.ScorePrediction(Rules, 0, 1, 2, 0, Competition.LeagueOne, MatchPhase.Regular, false, false);

        Assert.False(result.IsCorrectColumn);
        Assert.Equal(ScoreCategory.None, result.Category);
        Assert.Equal(0, result.BasePoints);
        Assert.Equal(2, result.Multiplier);
        Assert.Equal(0, result.FinalPoints);
    }
}

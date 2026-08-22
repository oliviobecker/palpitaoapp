using Palpitao.Api.Enums;
using Palpitao.Api.Services.Results;
using Xunit;

namespace Palpitao.Api.Tests.Results;

public class MatchStatusParserTests
{
    /// <summary>
    /// The vocabulary OneFootball's web-experience cards actually use in their <c>period</c>
    /// field. A live match reading as NotStarted is the bug this table exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("PRE_MATCH", MatchStatus.NotStarted)]
    [InlineData("FIRST_HALF", MatchStatus.InProgress)]
    [InlineData("HALF_TIME", MatchStatus.InProgress)]
    [InlineData("SECOND_HALF", MatchStatus.InProgress)]
    [InlineData("EXTRA_TIME_FIRST_HALF", MatchStatus.InProgress)]
    [InlineData("PENALTY_SHOOTOUT", MatchStatus.InProgress)]
    [InlineData("FULL_TIME", MatchStatus.Finished)]
    [InlineData("FULL_TIME_PENALTIES", MatchStatus.Finished)]
    [InlineData("ABANDONED", MatchStatus.Cancelled)]
    public void Reads_the_onefootball_vocabulary(string raw, MatchStatus expected)
        => Assert.Equal(expected, MatchStatusParser.Parse(raw, null, null));

    /// <summary>Codes the two old provider tables knew — none of them may regress.</summary>
    [Theory]
    [InlineData("live", MatchStatus.InProgress)]
    [InlineData("in_progress", MatchStatus.InProgress)]
    [InlineData("inprogress", MatchStatus.InProgress)]
    [InlineData("playing", MatchStatus.InProgress)]
    [InlineData("1H", MatchStatus.InProgress)]
    [InlineData("2H", MatchStatus.InProgress)]
    [InlineData("HT", MatchStatus.InProgress)]
    [InlineData("FT", MatchStatus.Finished)]
    [InlineData("finished", MatchStatus.Finished)]
    [InlineData("ended", MatchStatus.Finished)]
    [InlineData("AET", MatchStatus.Finished)]
    [InlineData("PEN", MatchStatus.Finished)]
    [InlineData("postponed", MatchStatus.Postponed)]
    [InlineData("SUSP", MatchStatus.Postponed)]
    [InlineData("suspended", MatchStatus.Postponed)]
    [InlineData("cancelled", MatchStatus.Cancelled)]
    [InlineData("canceled", MatchStatus.Cancelled)]
    public void Keeps_the_short_codes_the_old_tables_supported(string raw, MatchStatus expected)
        => Assert.Equal(expected, MatchStatusParser.Parse(raw, null, null));

    [Theory]
    [InlineData("full_time")]
    [InlineData("Full Time")]
    [InlineData("FULL-TIME")]
    [InlineData("fullTime")]
    [InlineData("  FULL_TIME  ")]
    public void Ignores_separators_and_casing(string raw)
        => Assert.Equal(MatchStatus.Finished, MatchStatusParser.Parse(raw, null, null));

    [Theory]
    [InlineData("66'")]
    [InlineData("45+2'")]
    public void Falls_back_to_the_running_clock_for_an_unknown_label(string clock)
        => Assert.Equal(MatchStatus.InProgress, MatchStatusParser.Parse("SOMETHING_NEW", null, null, clock));

    /// <summary>
    /// The clock only counts when it looks like a running minute: "Full time" and a kickoff time
    /// must never be read as live.
    /// </summary>
    [Theory]
    [InlineData("Full time")]
    [InlineData("Abandoned")]
    [InlineData("20:00")]
    [InlineData("")]
    public void Does_not_read_a_non_clock_as_live(string clock)
        => Assert.NotEqual(MatchStatus.InProgress, MatchStatusParser.Parse("SOMETHING_NEW", null, null, clock));

    /// <summary>A live scoreline must not be frozen as final by the "has a score" fallback.</summary>
    [Fact]
    public void Prefers_the_clock_over_the_scoreline()
        => Assert.Equal(MatchStatus.InProgress, MatchStatusParser.Parse("SOMETHING_NEW", 3, 0, "66'"));

    [Fact]
    public void Falls_back_to_finished_when_only_a_scoreline_is_known()
    {
        Assert.Equal(MatchStatus.Finished, MatchStatusParser.Parse("", 3, 0));
        Assert.Equal(MatchStatus.Finished, MatchStatusParser.Parse("SOMETHING_NEW", 3, 0));
    }

    [Fact]
    public void Falls_back_to_not_started_without_a_clock_or_a_scoreline()
    {
        Assert.Equal(MatchStatus.NotStarted, MatchStatusParser.Parse(null, null, null));
        Assert.Equal(MatchStatus.NotStarted, MatchStatusParser.Parse("", null, null));
        Assert.Equal(MatchStatus.NotStarted, MatchStatusParser.Parse("SOMETHING_NEW", null, null));
        Assert.Equal(MatchStatus.NotStarted, MatchStatusParser.Parse("SOMETHING_NEW", 3, null));
    }

    [Fact]
    public void Reports_only_a_label_it_could_not_map()
    {
        MatchStatusParser.Parse("SECOND_HALF", null, null, null, out var mappedIsUnknown);
        Assert.False(mappedIsUnknown);

        // No label at all is nothing to warn about — only a label we failed to map is.
        MatchStatusParser.Parse("", null, null, null, out var absentIsUnknown);
        Assert.False(absentIsUnknown);

        MatchStatusParser.Parse("SOMETHING_NEW", null, null, null, out var unmappedIsUnknown);
        Assert.True(unmappedIsUnknown);
    }
}

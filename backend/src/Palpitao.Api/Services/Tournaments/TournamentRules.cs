using Palpitao.Api.Enums;

namespace Palpitao.Api.Services.Tournaments;

/// <summary>
/// Pure rules that depend only on the <see cref="TournamentType"/>: which
/// competitions and phases are allowed, and World Cup phase classification.
/// Kept free of DB/HTTP so it can be reused by services, validation and tests.
/// </summary>
public static class TournamentRules
{
    private static readonly IReadOnlyList<Competition> EnglandCompetitions = new[]
    {
        Competition.PremierLeague,
        Competition.FACup,
        Competition.Championship,
        Competition.LeagueOne,
    };

    private static readonly IReadOnlyList<Competition> WorldCupCompetitions = new[]
    {
        Competition.FifaWorldCup,
    };

    private static readonly IReadOnlyList<MatchPhase> EnglandPhases = new[]
    {
        MatchPhase.Regular,
        MatchPhase.PlayoffSemiFinal,
        MatchPhase.PlayoffFinal,
        MatchPhase.FACupSemiFinal,
        MatchPhase.FACupFinal,
        MatchPhase.Other,
    };

    private static readonly IReadOnlyList<MatchPhase> WorldCupPhases = new[]
    {
        MatchPhase.WorldCupGroupStage,
        MatchPhase.WorldCupRoundOf32,
        MatchPhase.WorldCupRoundOf16,
        MatchPhase.WorldCupQuarterFinal,
        MatchPhase.WorldCupSemiFinal,
        MatchPhase.WorldCupThirdPlace,
        MatchPhase.WorldCupFinal,
    };

    /// <summary>Competitions a group of this type may register matches for.</summary>
    public static IReadOnlyList<Competition> AllowedCompetitions(TournamentType type) => type switch
    {
        TournamentType.FifaWorldCup => WorldCupCompetitions,
        _ => EnglandCompetitions,
    };

    /// <summary>Match phases a group of this type may use.</summary>
    public static IReadOnlyList<MatchPhase> AllowedPhases(TournamentType type) => type switch
    {
        TournamentType.FifaWorldCup => WorldCupPhases,
        _ => EnglandPhases,
    };

    public static bool IsCompetitionAllowed(TournamentType type, Competition competition)
        => AllowedCompetitions(type).Contains(competition);

    public static bool IsPhaseAllowed(TournamentType type, MatchPhase phase)
        => AllowedPhases(type).Contains(phase);

    /// <summary>World Cup knockout phases (everything from the Round of 32 onward).
    /// The classic (campeãs mundiais) multiplier only doubles from the knockout on.</summary>
    public static bool IsWorldCupKnockout(MatchPhase phase) => phase switch
    {
        MatchPhase.WorldCupRoundOf32
            or MatchPhase.WorldCupRoundOf16
            or MatchPhase.WorldCupQuarterFinal
            or MatchPhase.WorldCupSemiFinal
            or MatchPhase.WorldCupThirdPlace
            or MatchPhase.WorldCupFinal => true,
        _ => false,
    };

    /// <summary>Phases that activate the World Cup Regra Flávio (from the quarter-finals).</summary>
    public static bool IsWorldCupFlavioPhase(MatchPhase phase) => phase switch
    {
        MatchPhase.WorldCupQuarterFinal
            or MatchPhase.WorldCupSemiFinal
            or MatchPhase.WorldCupThirdPlace
            or MatchPhase.WorldCupFinal => true,
        _ => false,
    };
}

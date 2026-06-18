namespace Palpitao.Api.Enums;

public enum MatchPhase
{
    Regular,
    PlayoffSemiFinal,
    PlayoffFinal,
    FACupSemiFinal,
    FACupFinal,
    Other,

    // FIFA World Cup phases (stored as strings, so ordinals are irrelevant).
    WorldCupGroupStage,
    WorldCupRoundOf32,
    WorldCupRoundOf16,
    WorldCupQuarterFinal,
    WorldCupSemiFinal,
    WorldCupThirdPlace,
    WorldCupFinal,
}

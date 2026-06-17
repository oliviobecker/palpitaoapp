namespace Palpitao.Api.Enums;

public enum Competition
{
    PremierLeague,
    FACup,
    Championship,
    LeagueOne,

    // FIFA World Cup certames. Stored as a string (HasConversion<string>), so the
    // ordinal value is irrelevant — appended to avoid renumbering existing rows.
    FifaWorldCup,
}

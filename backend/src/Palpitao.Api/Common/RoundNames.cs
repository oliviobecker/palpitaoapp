using System.Globalization;

namespace Palpitao.Api.Common;

/// <summary>
/// How a round is named: its label ("10", or "10.2" for the second part of a round played in
/// parts) and the default title the round form pre-fills ("Décima Rodada" / "Round 10"). The
/// frontend twin is <c>shared/utils/round-name.util.ts</c> — keep both in step.
/// </summary>
public static class RoundNames
{
    // Feminine ordinals (for "rodada") in Portuguese, 1–99 (+100); beyond that "Rodada N".
    private static readonly string[] PtUnits =
        ["", "primeira", "segunda", "terceira", "quarta", "quinta", "sexta", "sétima", "oitava", "nona"];

    private static readonly string[] PtTens =
    [
        "", "décima", "vigésima", "trigésima", "quadragésima", "quinquagésima", "sexagésima",
        "septuagésima", "octogésima", "nonagésima",
    ];

    /// <summary>
    /// "10" for a standalone round, "10.2" for part 2 of round 10. Always a dot: the OCR reads
    /// "x", "×", ":" and dashes between two digits as a score, so "Rodada 10-2" would become one.
    /// </summary>
    public static string Label(int number, int part) =>
        part > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{number}.{part}")
            : number.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The title the round form pre-fills for a number: "Primeira Rodada", "Segunda Rodada", …
    /// in Portuguese, "Round 1" in English.
    /// </summary>
    public static string DefaultTitle(int number, string language)
    {
        if (language.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            var ordinal = PtFeminineOrdinal(number);
            return ordinal is null
                ? string.Create(CultureInfo.InvariantCulture, $"Rodada {number}")
                : $"{char.ToUpperInvariant(ordinal[0])}{ordinal[1..]} Rodada";
        }

        return string.Create(CultureInfo.InvariantCulture, $"Round {number}");
    }

    /// <summary>
    /// The title a renumbered round should carry. A title still equal to the default of its old
    /// number (in either language) follows the new number; anything the admin typed is kept. A
    /// closed round's title cannot be edited, so a stale "Sétima Rodada" on round 6 would stay.
    /// </summary>
    public static string? RenumberDefaultTitle(string? title, int oldNumber, int newNumber)
    {
        if (title is null || oldNumber == newNumber)
        {
            return title;
        }

        foreach (var language in new[] { "pt", "en" })
        {
            if (title == DefaultTitle(oldNumber, language))
            {
                return DefaultTitle(newNumber, language);
            }
        }

        return title;
    }

    private static string? PtFeminineOrdinal(int n)
    {
        if (n is >= 1 and <= 9)
        {
            return PtUnits[n];
        }

        if (n == 100)
        {
            return "centésima";
        }

        if (n is >= 10 and <= 99)
        {
            var tens = PtTens[n / 10];
            var unit = n % 10;
            return unit == 0 ? tens : $"{tens} {PtUnits[unit]}";
        }

        return null;
    }
}

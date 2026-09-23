using Palpitao.Api.Common;
using Xunit;

namespace Palpitao.Api.Tests.Rounds;

/// <summary>Mirrors frontend/src/app/shared/utils/round-name.util.spec.ts.</summary>
public class RoundNamesTests
{
    [Theory]
    [InlineData(10, 0, "10")]
    [InlineData(10, 1, "10.1")]
    [InlineData(10, 2, "10.2")]
    public void Labels_use_a_dot_for_parts(int number, int part, string label)
        => Assert.Equal(label, RoundNames.Label(number, part));

    [Theory]
    [InlineData(1, "pt", "Primeira Rodada")]
    [InlineData(7, "pt-BR", "Sétima Rodada")]
    [InlineData(10, "pt", "Décima Rodada")]
    [InlineData(21, "pt", "Vigésima primeira Rodada")]
    [InlineData(100, "pt", "Centésima Rodada")]
    [InlineData(101, "pt", "Rodada 101")]
    [InlineData(7, "en", "Round 7")]
    [InlineData(7, "en-US", "Round 7")]
    public void Default_titles_match_the_round_form(int number, string language, string title)
        => Assert.Equal(title, RoundNames.DefaultTitle(number, language));

    [Theory]
    [InlineData("Sétima Rodada", 7, 6, "Sexta Rodada")]
    [InlineData("Round 7", 7, 6, "Round 6")]
    [InlineData("Clássico de sábado", 7, 6, "Clássico de sábado")]
    [InlineData("Sétima Rodada", 8, 7, "Sétima Rodada")]
    [InlineData("Sétima Rodada", 7, 7, "Sétima Rodada")]
    [InlineData(null, 7, 6, null)]
    public void Only_an_untouched_default_title_follows_a_new_number(
        string? title, int oldNumber, int newNumber, string? expected)
        => Assert.Equal(expected, RoundNames.RenumberDefaultTitle(title, oldNumber, newNumber));
}

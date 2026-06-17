using Palpitao.Api.Common;
using Palpitao.Api.Services.Localization;
using Xunit;

namespace Palpitao.Api.Tests.Localization;

public class LocalizationServiceTests
{
    private readonly LocalizationService _sut = new(null!);

    [Theory]
    [InlineData("pt-BR", "pt")]
    [InlineData("pt", "pt")]
    [InlineData("en-US", "en")]
    [InlineData("fr-FR", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void ResolveLanguage_maps_accept_language(string? accept, string expected)
    {
        Assert.Equal(expected, _sut.ResolveLanguage(accept));
    }

    [Fact]
    public void Returns_portuguese_for_pt()
    {
        Assert.Equal("E-mail ou senha inválidos.", _sut.Get("auth.invalidCredentials", "pt-BR"));
    }

    [Fact]
    public void Returns_english_for_en()
    {
        Assert.Equal("Invalid e-mail or password.", _sut.Get("auth.invalidCredentials", "en-US"));
    }

    [Fact]
    public void Falls_back_to_english()
    {
        Assert.Equal("Invalid e-mail or password.", _sut.Get("auth.invalidCredentials", "fr-FR"));
    }

    [Theory]
    [InlineData("notFound.round", "Rodada não encontrada.", "Round not found.")]
    [InlineData("prediction.deadlinePassed", "O prazo para palpitar nesta rodada já encerrou.", "The deadline to predict for this round has passed.")]
    [InlineData("round.duplicateNumber", "Já existe uma rodada com esse número nesta temporada.", "A round with this number already exists in this season.")]
    [InlineData("ocr.tooLarge", "A imagem deve ter no máximo 10 MB.", "The image must be at most 10 MB.")]
    public void Domain_keys_resolve_pt_and_en(string key, string pt, string en)
    {
        Assert.Equal(pt, _sut.Get(key, "pt-BR"));
        Assert.Equal(en, _sut.Get(key, "en-US"));
    }

    [Fact]
    public void Unknown_key_falls_back_to_itself()
    {
        Assert.Equal("some.unmapped.key", _sut.Get("some.unmapped.key", "en-US"));
    }

    [Fact]
    public void Every_catalog_entry_has_pt_and_en_text()
    {
        Assert.All(DomainMessages.Catalog, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Value.Pt), $"PT missing for {e.Key}");
            Assert.False(string.IsNullOrWhiteSpace(e.Value.En), $"EN missing for {e.Key}");
        });
    }

    [Fact]
    public void Keyed_exceptions_carry_key_and_portuguese_message()
    {
        var business = new BusinessRuleException("prediction.negativeScore");
        Assert.Equal("prediction.negativeScore", business.Key);
        Assert.Equal("O placar não pode ser negativo.", business.Message);

        var notFound = new NotFoundException("notFound.round");
        Assert.Equal("notFound.round", notFound.Key);
        Assert.Equal("Rodada não encontrada.", notFound.Message);
    }
}

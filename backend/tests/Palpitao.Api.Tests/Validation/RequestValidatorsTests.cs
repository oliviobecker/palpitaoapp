using Palpitao.Api.Common;
using Palpitao.Api.DTOs.Auth;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.Enums;
using Palpitao.Api.Validation;
using Xunit;

namespace Palpitao.Api.Tests.Validation;

/// <summary>
/// FluentValidation validators produce DomainMessages keys (resolved to the request
/// language by the middleware), so every validation message exists in PT and EN.
/// </summary>
public class RequestValidatorsTests
{
    private static string FirstError<T>(FluentValidation.AbstractValidator<T> validator, T instance)
        => validator.Validate(instance).Errors[0].ErrorMessage;

    [Fact]
    public void Login_requires_email_and_password()
    {
        var validator = new LoginRequestValidator();
        Assert.Equal("validation.email.required",
            FirstError(validator, new LoginRequest { Email = "", Password = "x" }));
        Assert.Equal("validation.email.invalid",
            FirstError(validator, new LoginRequest { Email = "not-an-email", Password = "x" }));
        Assert.Equal("validation.password.required",
            FirstError(validator, new LoginRequest { Email = "a@b.com", Password = "" }));
    }

    [Fact]
    public void Login_accepts_a_valid_request()
        => Assert.True(new LoginRequestValidator()
            .Validate(new LoginRequest { Email = "a@b.com", Password = "Senha123" }).IsValid);

    [Fact]
    public void Match_requires_competition_phase_teams_and_date()
    {
        var validator = new CreateMatchRequestValidator();
        var errors = validator.Validate(new CreateMatchRequest()).Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Contains("validation.competition.required", errors);
        Assert.Contains("validation.phase.required", errors);
        Assert.Contains("validation.homeTeam.required", errors);
        Assert.Contains("validation.awayTeam.required", errors);
        Assert.Contains("validation.startsAt.required", errors);
    }

    [Fact]
    public void Prediction_item_rejects_negative_scores()
    {
        var validator = new PredictionItemRequestValidator();
        var errors = validator.Validate(new PredictionItemRequest
        {
            RoundMatchId = Guid.NewGuid(),
            PredictedHomeScore = -1,
            PredictedAwayScore = 0,
        }).Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Contains("validation.score.negative", errors);
    }

    [Theory]
    [InlineData("validation.email.required")]
    [InlineData("validation.email.invalid")]
    [InlineData("validation.password.required")]
    [InlineData("validation.competition.required")]
    [InlineData("validation.homeTeam.required")]
    [InlineData("validation.awayTeam.required")]
    [InlineData("validation.score.negative")]
    [InlineData("validation.justification.required")]
    [InlineData("tournamentType.required")]
    public void Every_validation_key_resolves_in_both_languages(string key)
    {
        var pt = DomainMessages.Resolve(key, "pt");
        var en = DomainMessages.Resolve(key, "en");
        Assert.NotEqual(key, pt); // not the literal key (i.e. present in the catalog)
        Assert.NotEqual(key, en);
        Assert.NotEqual(pt, en);  // PT and EN are actually different
    }
}

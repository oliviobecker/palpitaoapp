using Palpitao.Application.Absences;
using Palpitao.Application.Auth;
using Palpitao.Application.Flavio;
using Palpitao.Application.Predictions;
using Palpitao.Application.Rounds;
using Palpitao.Application.Users;
using Palpitao.Domain.Common;

namespace Palpitao.UnitTests.Validation;

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

    /// <summary>Each validator with a justification, and its first error for a given text.</summary>
    public static TheoryData<string, Func<string, string>> JustificationValidators => new()
    {
        { "absence override", j => FirstError(new AbsenceOverrideRequestValidator(), new AbsenceOverrideRequest { UserId = Guid.NewGuid(), Justification = j }) },
        { "reactivation", j => FirstError(new ReactivateRequestValidator(), new ReactivateRequest { Justification = j }) },
        { "absence review", j => FirstError(new AbsenceReviewRequestValidator(), new AbsenceReviewRequest { Justification = j }) },
        { "elimination", j => FirstError(new EliminateRequestValidator(), new EliminateRequest { Justification = j }) },
        { "Flávio override", j => FirstError(new FlavioOverrideRequestValidator(), new FlavioOverrideRequest { UserId = Guid.NewGuid(), Justification = j }) },
    };

    [Theory]
    [MemberData(nameof(JustificationValidators))]
    public void An_empty_justification_reports_a_catalog_key(string _, Func<string, string> firstError)
    {
        // Not FluentValidation's default English text, which the catalog does not know.
        var error = firstError("");
        Assert.NotEqual(error, DomainMessages.Resolve(error, "en"));
    }

    [Theory]
    [MemberData(nameof(JustificationValidators))]
    public void A_justification_over_500_characters_is_too_long(string _, Func<string, string> firstError)
        => Assert.Equal("validation.justification.tooLong", firstError(new string('x', 501)));

    [Fact]
    public void An_absence_review_needs_its_list_of_rounds()
        => Assert.Equal("validation.required", FirstError(new AbsenceReviewRequestValidator(),
            new AbsenceReviewRequest { Justification = "Revisão", Rounds = null! }));

    [Theory]
    [InlineData("abc123")]   // six characters: the old minimum, short of the policy's eight
    [InlineData("abcdefgh")] // no digit
    [InlineData("12345678")] // no letter
    public void A_participant_password_follows_the_account_policy(string password)
        => Assert.Equal("auth.weakPassword", FirstError(new CreateParticipantRequestValidator(),
            new CreateParticipantRequest { Name = "Ana", Email = "ana@example.com", Password = password }));

    [Fact]
    public void A_participant_with_a_strong_password_is_valid()
        => Assert.True(new CreateParticipantRequestValidator().Validate(
            new CreateParticipantRequest { Name = "Ana", Email = "ana@example.com", Password = "Senha123" }).IsValid);

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
    [InlineData("validation.justification.tooLong")]
    [InlineData("validation.required")]
    [InlineData("auth.weakPassword")]
    public void Every_validation_key_resolves_in_both_languages(string key)
    {
        var pt = DomainMessages.Resolve(key, "pt");
        var en = DomainMessages.Resolve(key, "en");
        Assert.NotEqual(key, pt); // not the literal key (i.e. present in the catalog)
        Assert.NotEqual(key, en);
        Assert.NotEqual(pt, en);  // PT and EN are actually different
    }
}

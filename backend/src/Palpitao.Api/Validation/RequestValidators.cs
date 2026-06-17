using FluentValidation;
using Palpitao.Api.DTOs.Absences;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.DTOs.Auth;
using Palpitao.Api.DTOs.Fixtures;
using Palpitao.Api.DTOs.Groups;
using Palpitao.Api.DTOs.Matches;
using Palpitao.Api.DTOs.Predictions;
using Palpitao.Api.DTOs.Rounds;
using Palpitao.Api.DTOs.Scoring;
using Palpitao.Api.DTOs.Seasons;

namespace Palpitao.Api.Validation;

// FluentValidation validators for the request DTOs. Messages are DomainMessages
// keys (resolved to the request language by the ValidationActionFilter / middleware),
// so every validation message is available in both Portuguese and English.

// --- Auth -------------------------------------------------------------------

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.name.required")
            .Length(2, 120).WithMessage("validation.name.length");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("validation.passwordConfirm.required");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("validation.group.required");
    }
}

public class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.GroupName).NotEmpty().WithMessage("validation.group.nameRequired")
            .Length(2, 120).WithMessage("validation.group.nameLength");
        RuleFor(x => x.TournamentType).NotNull().WithMessage("tournamentType.required");
        RuleFor(x => x.AdminName).NotEmpty().WithMessage("validation.adminName.required")
            .Length(2, 120).WithMessage("validation.name.length");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("validation.passwordConfirm.required");
    }
}

// --- Seasons / Rounds -------------------------------------------------------

public class SeasonRequestValidator : AbstractValidator<SeasonRequest>
{
    public SeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.seasonName.required");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("validation.startDate.required");
        RuleFor(x => x.EndDate).NotEmpty().WithMessage("validation.endDate.required");
    }
}

public class CreateRoundRequestValidator : AbstractValidator<CreateRoundRequest>
{
    public CreateRoundRequestValidator()
    {
        RuleFor(x => x.SeasonId).NotEmpty().WithMessage("validation.season.required");
        RuleFor(x => x.Number).GreaterThanOrEqualTo(1).WithMessage("validation.roundNumber.min");
    }
}

public class UpdateRoundRequestValidator : AbstractValidator<UpdateRoundRequest>
{
    public UpdateRoundRequestValidator()
        => RuleFor(x => x.Number).GreaterThanOrEqualTo(1).WithMessage("validation.roundNumber.min");
}

// --- Matches (shared rules for create + update) -----------------------------

public class MatchRequestValidator<T> : AbstractValidator<T> where T : CreateMatchRequest
{
    public MatchRequestValidator()
    {
        RuleFor(x => x.Competition).NotNull().WithMessage("validation.competition.required");
        RuleFor(x => x.Phase).NotNull().WithMessage("validation.phase.required");
        RuleFor(x => x.HomeTeamId).NotEmpty().WithMessage("validation.homeTeam.required");
        RuleFor(x => x.AwayTeamId).NotEmpty().WithMessage("validation.awayTeam.required");
        RuleFor(x => x.StartsAt).NotNull().WithMessage("validation.startsAt.required");
    }
}

public class CreateMatchRequestValidator : MatchRequestValidator<CreateMatchRequest> { }

public class UpdateMatchRequestValidator : MatchRequestValidator<UpdateMatchRequest> { }

// --- Fixtures ---------------------------------------------------------------

public class SearchFixturesRequestValidator : AbstractValidator<SearchFixturesRequest>
{
    public SearchFixturesRequestValidator()
    {
        RuleFor(x => x.StartDate).NotNull().WithMessage("validation.startDate.required");
        RuleFor(x => x.EndDate).NotNull().WithMessage("validation.endDate.required");
    }
}

public class ImportFixtureItemValidator : AbstractValidator<ImportFixtureItem>
{
    public ImportFixtureItemValidator()
    {
        RuleFor(x => x.ExternalId).NotEmpty().WithMessage("validation.externalId.required");
        RuleFor(x => x.HomeTeamName).NotEmpty().WithMessage("validation.homeTeam.required");
        RuleFor(x => x.AwayTeamName).NotEmpty().WithMessage("validation.awayTeam.required");
    }
}

// --- Predictions ------------------------------------------------------------

public class PredictionItemRequestValidator : AbstractValidator<PredictionItemRequest>
{
    public PredictionItemRequestValidator()
    {
        RuleFor(x => x.RoundMatchId).NotEmpty().WithMessage("validation.match.required");
        RuleFor(x => x.PredictedHomeScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
        RuleFor(x => x.PredictedAwayScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
    }
}

public class SavePredictionsRequestValidator : AbstractValidator<SavePredictionsRequest>
{
    public SavePredictionsRequestValidator()
    {
        RuleFor(x => x.Predictions).NotEmpty().WithMessage("validation.predictions.required");
        RuleForEach(x => x.Predictions).SetValidator(new PredictionItemRequestValidator());
    }
}

public class ManualPredictionRequestValidator : AbstractValidator<ManualPredictionRequest>
{
    public ManualPredictionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("validation.participant.required");
        RuleFor(x => x.Predictions).NotEmpty().WithMessage("validation.predictions.required");
        RuleForEach(x => x.Predictions).SetValidator(new PredictionItemRequestValidator());
    }
}

// --- Scoring ----------------------------------------------------------------

public class MatchResultRequestValidator : AbstractValidator<MatchResultRequest>
{
    public MatchResultRequestValidator()
    {
        RuleFor(x => x.HomeScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
        RuleFor(x => x.AwayScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
    }
}

// --- Participants / Absences ------------------------------------------------

public class CreateParticipantRequestValidator : AbstractValidator<CreateParticipantRequest>
{
    public CreateParticipantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.name.required");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required")
            .MinimumLength(6).WithMessage("validation.password.min6");
    }
}

public class UpdateParticipantRequestValidator : AbstractValidator<UpdateParticipantRequest>
{
    public UpdateParticipantRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.name.required");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
    }
}

public class EliminateRequestValidator : AbstractValidator<EliminateRequest>
{
    public EliminateRequestValidator()
        => RuleFor(x => x.Justification).NotEmpty().MinimumLength(3)
            .WithMessage("validation.justification.required");
}

public class AbsenceOverrideRequestValidator : AbstractValidator<AbsenceOverrideRequest>
{
    public AbsenceOverrideRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("validation.participant.required");
        RuleFor(x => x.Justification).NotEmpty().MinimumLength(3)
            .WithMessage("validation.justification.required");
    }
}

public class ReactivateRequestValidator : AbstractValidator<ReactivateRequest>
{
    public ReactivateRequestValidator()
        => RuleFor(x => x.Justification).NotEmpty().MinimumLength(3)
            .WithMessage("validation.justification.required");
}

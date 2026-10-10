using FluentValidation;

namespace Palpitao.Application.Rounds;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

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

using FluentValidation;

namespace Palpitao.Application.Scoring;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class MatchResultRequestValidator : AbstractValidator<MatchResultRequest>
{
    public MatchResultRequestValidator()
    {
        RuleFor(x => x.HomeScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
        RuleFor(x => x.AwayScore).GreaterThanOrEqualTo(0).WithMessage("validation.score.negative");
    }
}

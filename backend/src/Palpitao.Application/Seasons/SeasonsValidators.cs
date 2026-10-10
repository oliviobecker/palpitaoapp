using FluentValidation;

namespace Palpitao.Application.Seasons;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class SeasonRequestValidator : AbstractValidator<SeasonRequest>
{
    public SeasonRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.seasonName.required");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("validation.startDate.required");
        RuleFor(x => x.EndDate).NotEmpty().WithMessage("validation.endDate.required");
    }
}

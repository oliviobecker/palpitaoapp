using FluentValidation;

namespace Palpitao.Application.Fixtures;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

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

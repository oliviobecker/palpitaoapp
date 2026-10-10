using FluentValidation;
using Palpitao.Application.Predictions;

namespace Palpitao.Application.AdminPredictions;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class ManualPredictionRequestValidator : AbstractValidator<ManualPredictionRequest>
{
    public ManualPredictionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("validation.participant.required");
        RuleFor(x => x.Predictions).NotEmpty().WithMessage("validation.predictions.required");
        RuleForEach(x => x.Predictions).SetValidator(new PredictionItemRequestValidator());
    }
}

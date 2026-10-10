using FluentValidation;

namespace Palpitao.Application.Predictions;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

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

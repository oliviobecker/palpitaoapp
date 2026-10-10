using FluentValidation;

namespace Palpitao.Application.Absences;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class AbsenceOverrideRequestValidator : AbstractValidator<AbsenceOverrideRequest>
{
    public AbsenceOverrideRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("validation.participant.required");
        RuleFor(x => x.Justification).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("validation.justification.required")
            .MinimumLength(3).WithMessage("validation.justification.required")
            .MaximumLength(500).WithMessage("validation.justification.tooLong");
    }
}

public class ReactivateRequestValidator : AbstractValidator<ReactivateRequest>
{
    public ReactivateRequestValidator()
        => RuleFor(x => x.Justification).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("validation.justification.required")
            .MinimumLength(3).WithMessage("validation.justification.required")
            .MaximumLength(500).WithMessage("validation.justification.tooLong");
}

public class AbsenceReviewRequestValidator : AbstractValidator<AbsenceReviewRequest>
{
    public AbsenceReviewRequestValidator()
    {
        RuleFor(x => x.Justification).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("validation.justification.required")
            .MinimumLength(3).WithMessage("validation.justification.required")
            .MaximumLength(500).WithMessage("validation.justification.tooLong");
        RuleFor(x => x.Rounds).NotNull().WithMessage("validation.required");
        RuleForEach(x => x.Rounds).ChildRules(round =>
            round.RuleFor(d => d.RoundId).NotEmpty().WithMessage("notFound.round"));
    }
}

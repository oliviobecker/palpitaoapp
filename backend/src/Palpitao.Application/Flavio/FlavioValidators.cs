using FluentValidation;

namespace Palpitao.Application.Flavio;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class FlavioOverrideRequestValidator : AbstractValidator<FlavioOverrideRequest>
{
    public FlavioOverrideRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("validation.participant.required");
        RuleFor(x => x.Justification).NotEmpty().WithMessage("flavio.justificationRequired")
            .MaximumLength(500).WithMessage("flavio.justificationRequired");
    }
}

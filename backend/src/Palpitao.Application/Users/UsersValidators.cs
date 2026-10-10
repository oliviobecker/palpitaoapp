using FluentValidation;

namespace Palpitao.Application.Users;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

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
            .WithMessage("validation.justification.required")
            .MaximumLength(500).WithMessage("validation.justification.tooLong");
}

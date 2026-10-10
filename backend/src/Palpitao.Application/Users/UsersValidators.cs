using FluentValidation;
using Palpitao.Domain.Common;

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
        // The account policy (8+ characters, a letter and a digit), as the service enforces it.
        RuleFor(x => x.Password).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("validation.password.required")
            .Must(PasswordPolicy.IsStrong).WithMessage("auth.weakPassword");
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
        => RuleFor(x => x.Justification).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("validation.justification.required")
            .MinimumLength(3).WithMessage("validation.justification.required")
            .MaximumLength(500).WithMessage("validation.justification.tooLong");
}

using FluentValidation;

namespace Palpitao.Application.Auth;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("validation.name.required")
            .Length(2, 120).WithMessage("validation.name.length");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("validation.passwordConfirm.required");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("validation.group.required");
    }
}

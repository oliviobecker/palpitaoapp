using FluentValidation;

namespace Palpitao.Application.Groups;

// Messages are DomainMessages keys, resolved to the request language by the
// ValidationActionFilter, so every validation message exists in Portuguese and English.

public class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.GroupName).NotEmpty().WithMessage("validation.group.nameRequired")
            .Length(2, 120).WithMessage("validation.group.nameLength");
        RuleFor(x => x.AdminName).NotEmpty().WithMessage("validation.adminName.required")
            .Length(2, 120).WithMessage("validation.name.length");
        RuleFor(x => x.Email).NotEmpty().WithMessage("validation.email.required")
            .EmailAddress().WithMessage("validation.email.invalid");
        RuleFor(x => x.Password).NotEmpty().WithMessage("validation.password.required");
        RuleFor(x => x.ConfirmPassword).NotEmpty().WithMessage("validation.passwordConfirm.required");
    }
}

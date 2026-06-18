using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = Palpitao.Api.Common.ValidationException;

namespace Palpitao.Api.Validation;

/// <summary>
/// Runs the registered FluentValidation validator for each action argument. On the
/// first failure it throws a <see cref="ValidationException"/> carrying the message
/// key, which the exception middleware turns into a localized HTTP 400 (replacing the
/// old DataAnnotations ModelState 400).
/// </summary>
public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                // ErrorMessage is a DomainMessages key (set via WithMessage); the
                // middleware localizes it to the request language.
                throw new ValidationException(result.Errors[0].ErrorMessage);
            }
        }

        await next();
    }
}

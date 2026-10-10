using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;
using Palpitao.Api.Auth;

namespace Palpitao.Api.OpenApi;

/// <summary>
/// Declares the error responses once instead of on every action, all with the problem-details
/// body: 400, 404, 422 and 500 everywhere; 401 and 403 where a signed-in user is required; 403
/// where a group role is; 429 where a rate-limit policy applies. Documentation only — it changes
/// no response.
/// </summary>
internal sealed class ErrorResponsesConvention : IActionModelConvention
{
    private const string ProblemJson = "application/problem+json";

    public void Apply(ActionModel action)
    {
        var attributes = action.Attributes.Concat(action.Controller.Attributes).ToList();
        var statuses = new List<int>
        {
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound,
            StatusCodes.Status422UnprocessableEntity,
            StatusCodes.Status500InternalServerError,
        };

        var signedIn = attributes.OfType<IAuthorizeData>().Any() && !attributes.OfType<IAllowAnonymous>().Any();
        if (signedIn)
        {
            statuses.Add(StatusCodes.Status401Unauthorized);
        }

        if (signedIn || attributes.Any(a => a is RequireGroupParticipantAttribute or RequireGroupAdminAttribute))
        {
            statuses.Add(StatusCodes.Status403Forbidden);
        }

        if (attributes.OfType<EnableRateLimitingAttribute>().Any())
        {
            statuses.Add(StatusCodes.Status429TooManyRequests);
        }

        foreach (var status in statuses)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), status, ProblemJson));
        }
    }
}

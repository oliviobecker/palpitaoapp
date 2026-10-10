using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Palpitao.Api.Auth;
using Palpitao.Api.Localization;
using Palpitao.Api.OpenApi;
using Palpitao.Api.Validation;
using Palpitao.Application.Abstractions;
using Palpitao.Application.Auth;

namespace Palpitao.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// MVC, validation, problem details and the OpenAPI document, plus the ports that are read from
    /// the HTTP request: the signed-in user, the request's group and its language.
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers(options =>
            {
                // FluentValidation runs the request validators and throws a localized 400.
                options.Filters.Add<ValidationActionFilter>();
                options.Conventions.Add(new ErrorResponsesConvention());
            })
            .AddJsonOptions(options =>
            {
                // Serialize enums as readable strings (e.g. "PremierLeague").
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Suppress the DataAnnotations ModelState 400 so the ValidationActionFilter owns validation
        // (localized via DomainMessages).
        services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);

        services.AddApiProblemDetails();
        services.AddApiDocumentation();

        services.AddHttpContextAccessor();
        // DB-free per-request group accessor consumed by AppDbContext for the multi-tenant query
        // filter + insert-stamping (defence in depth, separate from access validation).
        services.AddScoped<IRequestGroupContext, RequestGroupContext>();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddScoped<ILocalizationService, LocalizationService>();

        return services;
    }

    /// <summary>JWT bearer authentication validated against <paramref name="jwt"/>, and authorization.</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtSettings jwt)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                };
            });
        services.AddAuthorization();

        return services;
    }
}

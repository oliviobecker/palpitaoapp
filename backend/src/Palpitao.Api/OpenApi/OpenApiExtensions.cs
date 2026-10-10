using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Palpitao.Api.Auth;
using Scalar.AspNetCore;

namespace Palpitao.Api.OpenApi;

public static class OpenApiExtensions
{
    private const string BearerScheme = "Bearer";

    /// <summary>
    /// The OpenAPI document: the API's description, the JWT bearer scheme on every operation that
    /// requires a signed-in user, and the <c>X-Group-Id</c> header on every group-scoped one. The
    /// XML comments on controllers and DTOs become the operations' summaries.
    /// </summary>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "FanPicks / Palpitão API",
                    Version = "v1",
                    Description =
                        "Multi-group football prediction pools. Sign in with POST /auth/login, send the access " +
                        "token as a bearer token and name the group with the X-Group-Id header. Errors are " +
                        "RFC 7807 problem details localized by Accept-Language (pt-BR, en-US).",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The access token returned by POST /auth/login or /auth/refresh.",
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var signedIn = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
                if (signedIn)
                {
                    operation.Security ??= [];
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
                    });
                }

                if (metadata.Any(m => m is RequireGroupParticipantAttribute or RequireGroupAdminAttribute))
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = HttpCurrentUser.GroupHeader,
                        In = ParameterLocation.Header,
                        Required = true,
                        Description = "The group the request acts in. Revalidated against an approved, active membership.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                    });
                }

                return Task.CompletedTask;
            });
        });

    /// <summary>
    /// Serves the document at <c>/openapi/v1.json</c> and the Scalar reference UI at
    /// <c>/scalar</c> when <c>OpenApi:Enabled</c> is on (appsettings.Development.json turns it on).
    /// </summary>
    public static void MapApiDocumentation(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("OpenApi:Enabled"))
        {
            return;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("FanPicks / Palpitão API"));
    }
}

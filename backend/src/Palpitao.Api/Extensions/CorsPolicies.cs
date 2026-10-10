namespace Palpitao.Api.Extensions;

/// <summary>
/// Development allows the Angular dev server; other environments allow the trusted origins
/// configured under <c>Cors:AllowedOrigins</c> (e.g. the deployed frontend URL), so the API can
/// serve a cross-origin SPA without being open.
/// </summary>
public static class CorsPolicies
{
    public const string Development = "AllowFrontendDev";
    public const string ConfiguredOrigins = "AllowConfiguredOrigins";

    public static string[] AllowedOrigins(IConfiguration configuration) =>
        configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    public static IServiceCollection AddCorsPolicies(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = AllowedOrigins(configuration);
        services.AddCors(options =>
        {
            options.AddPolicy(Development, policy =>
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod());

            if (allowedOrigins.Length > 0)
            {
                options.AddPolicy(ConfiguredOrigins, policy =>
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            }
        });

        return services;
    }
}

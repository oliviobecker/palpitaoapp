using Microsoft.EntityFrameworkCore;
using Palpitao.Infrastructure.Persistence;
using Palpitao.Infrastructure.Persistence.Seed;

namespace Palpitao.Api.Extensions;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending migrations at startup (creates the schema + seed on first run), unless
    /// <c>Database:ApplyMigrationsOnStartup</c> is false. If the database is unreachable in
    /// Development, logs and keeps the API up so /health still responds; anywhere else a
    /// migration/schema failure is rethrown so the deploy is rolled back or the host restarts
    /// instead of serving a drifted schema. Then, in Development, seeds the development admin into
    /// an empty database; anywhere else, flags that admin if it still has its published password.
    /// </summary>
    public static void ApplyDatabaseMigrations(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        try
        {
            // Log the target database (password redacted) so a misconfigured production
            // connection string is obvious in the logs.
            var connectionString = app.Configuration.GetConnectionString(ConnectionStrings.DefaultName);
            logger.LogInformation("Connecting to database: {ConnectionString}", ConnectionStrings.Redact(connectionString));

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            logger.LogInformation("Database migrations applied.");

            if (app.Environment.IsDevelopment() && DevelopmentAdmin.SeedIfNoUsers(db))
            {
                logger.LogInformation("Seeded the development admin into an empty database.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Applying database migrations at startup failed.");

            if (!app.Environment.IsDevelopment())
            {
                throw;
            }

            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            WarnIfTheDevelopmentPasswordIsLive(scope.ServiceProvider.GetRequiredService<AppDbContext>(), logger);
        }
    }

    /// <summary>
    /// Databases created through the migrations start with the development admin and its
    /// published password. Logged as an error — so it reaches Sentry — until the password is
    /// changed. A failing check is only a warning: it must never stop the host.
    /// </summary>
    private static void WarnIfTheDevelopmentPasswordIsLive(AppDbContext db, ILogger logger)
    {
        try
        {
            if (DevelopmentAdmin.HasPublishedPassword(db))
            {
                logger.LogError(
                    "The seeded admin account still uses the development password published in the repository. Change it.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not check the seeded admin account's password.");
        }
    }
}

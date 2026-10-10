using Microsoft.EntityFrameworkCore;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.Api.Extensions;

public static class DatabaseMigrationExtensions
{
    /// <summary>
    /// Applies pending migrations at startup (creates the schema + seed on first run), unless
    /// <c>Database:ApplyMigrationsOnStartup</c> is false. If the database is unreachable in
    /// Development, logs and keeps the API up so /health still responds; anywhere else a
    /// migration/schema failure is rethrown so the deploy is rolled back or the host restarts
    /// instead of serving a drifted schema.
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

            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
            logger.LogInformation("Database migrations applied.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Applying database migrations at startup failed.");

            if (!app.Environment.IsDevelopment())
            {
                throw;
            }
        }
    }
}

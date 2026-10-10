using Npgsql;

namespace Palpitao.Infrastructure.Persistence;

public static class ConnectionStrings
{
    /// <summary>The <c>ConnectionStrings</c> entry the database is read from.</summary>
    public const string DefaultName = "DefaultConnection";

    /// <summary>Redacts the password from a Npgsql connection string for safe logging.</summary>
    public static string Redact(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "(not configured)";
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "***";
            }

            return builder.ConnectionString;
        }
        catch
        {
            return "(invalid connection string)";
        }
    }
}

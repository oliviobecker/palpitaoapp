#:package Npgsql@10.0.2

// Minimal SQL runner used by the database-reset workflow
// (.github/workflows/reset-db.yml). It is a .NET 10 "file-based app": run it
// with `dotnet run scripts/run-sql.cs` -- no project file needed.
//
// Inputs (env vars):
//   DB_CONNECTION  Npgsql connection string of the target database (required).
//   SQL_FILE       Path to the .sql file to execute (or pass it as argv[0]).
//
// It echoes RAISE NOTICE messages and any result-set rows so the wipe and its
// count report show up in the Actions log. Exit code is non-zero on any error,
// so a failed wipe (e.g. the admin email is not found -> the DO block RAISEs and
// rolls itself back) fails the workflow step with nothing committed.

using Npgsql;

var conn = Environment.GetEnvironmentVariable("DB_CONNECTION");
if (string.IsNullOrWhiteSpace(conn))
{
    Console.Error.WriteLine("ERROR: DB_CONNECTION environment variable is not set.");
    return 1;
}

var sqlFile = Environment.GetEnvironmentVariable("SQL_FILE");
if (string.IsNullOrWhiteSpace(sqlFile) && args.Length > 0)
    sqlFile = args[0];
if (string.IsNullOrWhiteSpace(sqlFile) || !File.Exists(sqlFile))
{
    Console.Error.WriteLine($"ERROR: SQL file not found (SQL_FILE='{sqlFile}').");
    return 1;
}

var sql = await File.ReadAllTextAsync(sqlFile);
Console.WriteLine($"Executing {sqlFile} ({sql.Length} chars) against the target database...");

try
{
    await using var db = new NpgsqlConnection(conn);
    db.Notice += (_, e) => Console.WriteLine($"[pg] {e.Notice.MessageText}");
    await db.OpenAsync();

    await using var cmd = new NpgsqlCommand(sql, db);
    await using var reader = await cmd.ExecuteReaderAsync();
    do
    {
        if (reader.FieldCount == 0)
            continue;

        var header = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            header[i] = reader.GetName(i);
        var headerLine = string.Join("  |  ", header);

        Console.WriteLine();
        Console.WriteLine(headerLine);
        Console.WriteLine(new string('-', Math.Max(20, headerLine.Length)));

        while (await reader.ReadAsync())
        {
            var row = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = await reader.IsDBNullAsync(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
            Console.WriteLine(string.Join("  |  ", row));
        }
    } while (await reader.NextResultAsync());

    Console.WriteLine();
    Console.WriteLine("SQL executed successfully.");
    return 0;
}
catch (PostgresException pex)
{
    Console.Error.WriteLine($"POSTGRES ERROR [{pex.SqlState}]: {pex.MessageText}");
    if (!string.IsNullOrEmpty(pex.Where))
        Console.Error.WriteLine($"  where: {pex.Where}");
    return 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

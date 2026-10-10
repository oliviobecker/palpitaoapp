using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.UnitTests.TestSupport;

/// <summary>
/// A fresh SQLite in-memory database with the whole schema and its seed (teams, default group,
/// dev admin). SQLite rather than an in-memory provider, so constraints, transactions and
/// <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> are real. The context owns its connection:
/// disposing the context drops the database.
/// </summary>
public static class TestDatabase
{
    public static AppDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection, contextOwnsConnection: true)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}

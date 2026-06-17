using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Controllers;
using Palpitao.Api.Data;
using Xunit;

namespace Palpitao.Api.Tests.Health;

public class HealthControllerTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static HealthController CreateController(AppDbContext db) =>
        new(db, NullLogger<HealthController>.Instance);

    [Fact]
    public void Liveness_returns_ok()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>(CreateController(db).Get());
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_database_is_reachable()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>(await CreateController(db).Database(Ct));
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_503_when_database_is_unreachable()
    {
        var db = CreateContext();
        db.Dispose(); // Closing the in-memory connection makes the database unreachable.

        var result = Assert.IsType<ObjectResult>(await CreateController(db).Database(Ct));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void Health_endpoint_is_anonymous()
    {
        var anonymous = Attribute.GetCustomAttribute(typeof(HealthController), typeof(AllowAnonymousAttribute));
        Assert.NotNull(anonymous);
    }
}

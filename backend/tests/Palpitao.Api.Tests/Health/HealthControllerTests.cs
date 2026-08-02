using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Controllers;
using Palpitao.Api.Data;
using Palpitao.Api.Services.Ocr;
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

    private sealed class StubOcrEngine(params string[] missing) : IOcrEngine
    {
        public string ExtractText(byte[] image, string language) => string.Empty;
        public IReadOnlyList<string> MissingLanguages(string language) => missing;
    }

    private static HealthController CreateController(AppDbContext db, IOcrEngine? ocr = null) =>
        new(db, ocr ?? new StubOcrEngine(), NullLogger<HealthController>.Instance);

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
    public void Ocr_readiness_returns_ok_when_every_language_model_is_present()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>(CreateController(db).Ocr());
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void Ocr_readiness_returns_503_naming_the_missing_models()
    {
        using var db = CreateContext();
        var controller = CreateController(db, new StubOcrEngine("por"));

        var result = Assert.IsType<ObjectResult>(controller.Ocr());

        Assert.Equal(503, result.StatusCode);

        var properties = result.Value!.GetType().GetProperties();
        var missing = properties.Single(p => p.Name == "missing").GetValue(result.Value);
        Assert.Equal(["por"], Assert.IsAssignableFrom<IReadOnlyList<string>>(missing));

        // Language codes are safe to hand out; the tessdata path is not — it goes to the engine's
        // log instead, because this endpoint is anonymous.
        Assert.Equal(["status", "missing"], properties.Select(p => p.Name));
    }

    [Fact]
    public void Health_endpoint_is_anonymous()
    {
        var anonymous = Attribute.GetCustomAttribute(typeof(HealthController), typeof(AllowAnonymousAttribute));
        Assert.NotNull(anonymous);
    }
}

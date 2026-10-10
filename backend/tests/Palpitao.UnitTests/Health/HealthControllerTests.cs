using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Controllers;
using Palpitao.Application.Ocr;
using Palpitao.Infrastructure.Persistence.Seed;
using Palpitao.Infrastructure.Persistence;

namespace Palpitao.UnitTests.Health;

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
        DevelopmentAdmin.SeedIfNoUsers(db);
        return db;
    }

    private sealed class StubOcrEngine(params string[] missing) : IOcrEngine
    {
        public IReadOnlyList<OcrReading> ReadVariants(byte[] image, string language) => [];
        public IReadOnlyList<string> MissingLanguages(string language) => missing;
    }

    private static HealthController CreateController(AppDbContext db, IOcrEngine? ocr = null) =>
        new(new EfDatabaseHealthProbe(db), ocr ?? new StubOcrEngine(), NullLogger<HealthController>.Instance);

    [Fact]
    public void Liveness_returns_ok()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>(CreateController(db).Get().Result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_ok_when_database_is_reachable()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>((await CreateController(db).Database(Ct)).Result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task Readiness_returns_503_when_database_is_unreachable()
    {
        var db = CreateContext();
        db.Dispose(); // Closing the in-memory connection makes the database unreachable.

        var result = Assert.IsType<ObjectResult>((await CreateController(db).Database(Ct)).Result);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void Ocr_readiness_returns_ok_when_every_language_model_is_present()
    {
        using var db = CreateContext();
        var result = Assert.IsType<OkObjectResult>(CreateController(db).Ocr().Result);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public void Ocr_readiness_returns_503_naming_the_missing_models()
    {
        using var db = CreateContext();
        var controller = CreateController(db, new StubOcrEngine("por"));

        var result = Assert.IsType<ObjectResult>(controller.Ocr().Result);

        Assert.Equal(503, result.StatusCode);

        var body = Assert.IsType<HealthResponse>(result.Value);
        Assert.Equal(["por"], body.Missing!);

        // Language codes are safe to hand out; the tessdata path is not — it goes to the engine's
        // log instead, because this endpoint is anonymous.
        var json = JsonDocument.Parse(JsonSerializer.Serialize(body, JsonSerializerOptions.Web)).RootElement;
        Assert.Equal(["status", "missing"], json.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Health_endpoint_is_anonymous()
    {
        var anonymous = Attribute.GetCustomAttribute(typeof(HealthController), typeof(AllowAnonymousAttribute));
        Assert.NotNull(anonymous);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Palpitao.Api.Services.Ocr;
using Xunit;

namespace Palpitao.Api.Tests.Ocr;

/// <summary>
/// Covers the language-model lookup only — the part that reads the filesystem and that a deploy
/// can get wrong. Recognition itself needs the native Tesseract libraries and the ~38 MB models,
/// so it is exercised by the staging smoke test, not here.
/// </summary>
public class TesseractOcrEngineTests : IDisposable
{
    private readonly string _tessdata =
        Path.Combine(Path.GetTempPath(), $"palpitao-tessdata-{Guid.NewGuid():N}");

    public TesseractOcrEngineTests() => Directory.CreateDirectory(_tessdata);

    public void Dispose()
    {
        Directory.Delete(_tessdata, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Palpitao.Api";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private TesseractOcrEngine CreateEngine(string? tessdataPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ocr:TessdataPath"] = tessdataPath })
            .Build();

        return new TesseractOcrEngine(
            configuration, new StubHostEnvironment(), NullLogger<TesseractOcrEngine>.Instance);
    }

    private void WriteModel(string code) => File.WriteAllText(Path.Combine(_tessdata, $"{code}.traineddata"), "x");

    [Fact]
    public void MissingLanguages_is_empty_when_every_model_is_present()
    {
        WriteModel("por");
        WriteModel("eng");

        Assert.Empty(CreateEngine(_tessdata).MissingLanguages("por+eng"));
    }

    [Theory]
    [InlineData("por+eng", "por", new[] { "eng" })]
    [InlineData("por+eng", "eng", new[] { "por" })]
    [InlineData("eng", "por", new[] { "eng" })]
    public void MissingLanguages_names_the_codes_without_a_model(
        string requested, string present, string[] expected)
    {
        WriteModel(present);

        Assert.Equal(expected, CreateEngine(_tessdata).MissingLanguages(requested));
    }

    [Fact]
    public void MissingLanguages_reports_everything_when_the_folder_has_no_models()
    {
        // The production failure: the deploy copies an empty tessdata folder, so every code is
        // missing and the Tesseract constructor would throw on the first upload.
        Assert.Equal(["por", "eng"], CreateEngine(_tessdata).MissingLanguages("por+eng"));
    }

    [Fact]
    public void MissingLanguages_reports_rather_than_throws_when_the_folder_does_not_exist()
    {
        // A wrong Ocr:TessdataPath has to surface as a clean 422, never as an unhandled 500.
        var engine = CreateEngine(Path.Combine(_tessdata, "nope"));

        Assert.Equal(["por", "eng"], engine.MissingLanguages("por+eng"));
    }
}

namespace Palpitao.Api.Services.Ocr;

/// <summary>Abstraction over the OCR engine so it can be faked in tests.</summary>
public interface IOcrEngine
{
    string ExtractText(byte[] image, string language);

    /// <summary>
    /// The codes in <paramref name="language"/> ("por", "por+eng") the engine has no model for.
    /// Empty means <see cref="ExtractText"/> can run. Checked before the upload is recorded, so a
    /// server missing its models says so instead of blaming the image.
    /// </summary>
    IReadOnlyList<string> MissingLanguages(string language);
}

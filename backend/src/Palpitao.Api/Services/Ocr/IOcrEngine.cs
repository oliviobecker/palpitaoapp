namespace Palpitao.Api.Services.Ocr;

/// <summary>One pass of the OCR engine over an image, as prepared for that pass.</summary>
/// <param name="Variant">Which preparation produced it ("original", "prepared", "prepared+inverted").</param>
/// <param name="Confidence">The engine's own mean confidence, 0–1.</param>
public sealed record OcrReading(string Variant, string Text, float Confidence);

/// <summary>Abstraction over the OCR engine so it can be faked in tests.</summary>
public interface IOcrEngine
{
    /// <summary>
    /// Every reading the engine made of the image, in a fixed order. Which one is right cannot be
    /// told from the pixels alone — the engine's confidence picked a reading with fewer fixtures on
    /// 6 of 51 screenshots measured — so the import chooses, knowing the round.
    /// </summary>
    IReadOnlyList<OcrReading> ReadVariants(byte[] image, string language);

    /// <summary>
    /// The codes in <paramref name="language"/> ("por", "por+eng") the engine has no model for.
    /// Empty means <see cref="ReadVariants"/> can run. Checked before the upload is recorded, so a
    /// server missing its models says so instead of blaming the image.
    /// </summary>
    IReadOnlyList<string> MissingLanguages(string language);
}

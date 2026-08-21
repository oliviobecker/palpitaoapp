using Microsoft.Extensions.Hosting;
using Tesseract;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Tesseract-based OCR engine. Requires the traineddata files under the tessdata
/// folder (see README): tessdata/por.traineddata and tessdata/eng.traineddata.
///
/// Screenshots from messaging apps (dark chat bubbles, flag emoji) confuse the OCR.
/// A grayscale + upscale + Otsu binarization pass markedly improves recognition on
/// those images (validated: 9→12 matches recovered on a real WhatsApp print), and the
/// page is read both as-is and inverted because a dark-mode bubble is white-on-dark,
/// the reverse of what Tesseract expects. The whole pass can be turned off with
/// Ocr:Preprocess = false.
/// </summary>
public class TesseractOcrEngine : IOcrEngine
{
    /// <summary>Width the preprocessing aims for, and the hard cap on enlarging to reach it.</summary>
    private const int TargetWidth = 1100;
    private const int MaxUpscale = 4;

    private readonly string _tessdataPath;
    private readonly bool _preprocess;
    private readonly ILogger<TesseractOcrEngine> _logger;

    public TesseractOcrEngine(
        IConfiguration configuration,
        IHostEnvironment env,
        ILogger<TesseractOcrEngine> logger)
    {
        _tessdataPath = configuration["Ocr:TessdataPath"]
            ?? Path.Combine(env.ContentRootPath, "tessdata");
        _preprocess = configuration.GetValue("Ocr:Preprocess", true);
        _logger = logger;
    }

    public IReadOnlyList<string> MissingLanguages(string language)
    {
        var missing = language
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => !File.Exists(Path.Combine(_tessdataPath, $"{code}.traineddata")))
            .ToArray();

        if (missing.Length > 0)
        {
            // The path is logged here and nowhere else: this is the one place that knows it, and
            // both callers (the import preflight and /health/ocr) need it in the log without
            // putting a server path in an HTTP response.
            _logger.LogWarning(
                "Modelos de OCR ausentes ({Missing}) em {TessdataPath}. Veja Ocr:TessdataPath.",
                string.Join(", ", missing),
                _tessdataPath);
        }

        return missing;
    }

    public string ExtractText(byte[] image, string language)
    {
        using var engine = new TesseractEngine(_tessdataPath, language, EngineMode.Default);
        using var pix = Pix.LoadFromMemory(image);

        if (!_preprocess)
        {
            return Read(engine, pix, "original").Text;
        }

        // The untouched image competes too. Preprocessing is tuned for a bubble so small that
        // Tesseract reads it as noise, but binarization is a lossy bet: on a dark-mode screenshot
        // whose text is grey on near-black, a global Otsu threshold flattens most of the page into
        // the background. Measured on the screenshot that prompted this (391x712, dark mode):
        // original 83% and every fixture, binarized 66% and one, binarized+inverted 67% and three.
        // Reading only the prepared variants is what threw the other twenty away.
        var original = Read(engine, pix, "original");
        var best = original;

        var prepared = Preprocess(pix);
        try
        {
            var normal = Read(engine, prepared, "prepared");
            if (normal.Confidence > best.Confidence)
            {
                best = normal;
            }

            // Tesseract expects dark text on a light background. A dark-mode chat screenshot is
            // the opposite, and binarization keeps it that way — so read the inverse too and let
            // the engine's own confidence pick. Which one wins depends on the sender's theme,
            // which we cannot know from the bytes.
            var inverted = Invert(prepared);
            float? invertedConfidence = null;
            if (inverted is not null)
            {
                try
                {
                    var alternative = Read(engine, inverted, "prepared+inverted");
                    invertedConfidence = alternative.Confidence;
                    if (alternative.Confidence > best.Confidence)
                    {
                        best = alternative;
                    }
                }
                finally
                {
                    inverted.Dispose();
                }
            }

            // Every candidate, not just the winner: when an import comes back short the first
            // question is which variant won and by how much, and this answers it in one line.
            _logger.LogInformation(
                "OCR: variante {Variant} escolhida. original {Original:P0}, prepared {Prepared:P0}, "
                    + "prepared+inverted {Inverted:P0}; {Width}x{Height}px preparados.",
                best.Variant,
                original.Confidence,
                normal.Confidence,
                invertedConfidence ?? 0f,
                prepared.Width,
                prepared.Height);

            return best.Text;
        }
        finally
        {
            if (!ReferenceEquals(prepared, pix))
            {
                prepared.Dispose();
            }
        }
    }

    private static (string Text, float Confidence, string Variant) Read(
        TesseractEngine engine, Pix pix, string variant)
    {
        using var page = engine.Process(pix);
        var text = page.GetText() ?? string.Empty;
        return (text, page.GetMeanConfidence(), variant);
    }

    private Pix? Invert(Pix source)
    {
        try
        {
            return source.Invert();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR inversion failed; keeping the non-inverted reading.");
            return null;
        }
    }

    /// <summary>
    /// Grayscale, upscale, then Otsu binarization. Returns the original Pix when
    /// preprocessing is disabled or fails, so OCR always has an image to work with.
    /// </summary>
    private Pix Preprocess(Pix source)
    {
        Pix? gray = null;
        Pix? scaled = null;
        try
        {
            gray = source.Depth > 8 ? source.ConvertRGBToGray() : source;
            scaled = Upscale(gray);
            return scaled.BinarizeOtsuAdaptiveThreshold(2000, 2000, 0, 0, 0.1f);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR preprocessing failed; falling back to the original image.");
            return source;
        }
        finally
        {
            if (!ReferenceEquals(scaled, gray))
            {
                scaled?.Dispose();
            }

            if (!ReferenceEquals(gray, source))
            {
                gray?.Dispose();
            }
        }
    }

    /// <summary>
    /// Enlarges small screenshots. Tesseract wants roughly 30px-tall glyphs; a phone
    /// screenshot of a single chat bubble arrives ~300px wide with ~9px text, which it
    /// reads as noise. Capped at 4x, and a no-op for images already wide enough — the
    /// full-screen screenshots that already worked are left untouched.
    /// </summary>
    private static Pix Upscale(Pix source)
    {
        var factor = Math.Clamp((int)Math.Ceiling(TargetWidth / (double)source.Width), 1, MaxUpscale);
        return factor == 1 ? source : source.Scale(factor, factor);
    }
}

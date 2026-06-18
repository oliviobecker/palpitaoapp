using Microsoft.Extensions.Hosting;
using Tesseract;

namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Tesseract-based OCR engine. Requires the traineddata files under the tessdata
/// folder (see README): tessdata/por.traineddata and tessdata/eng.traineddata.
///
/// Screenshots from messaging apps (dark chat bubbles, flag emoji) confuse the OCR.
/// A grayscale + Otsu binarization pass markedly improves recognition on those
/// images (validated: 9→12 matches recovered on a real WhatsApp print). It can be
/// turned off with Ocr:Preprocess = false.
/// </summary>
public class TesseractOcrEngine : IOcrEngine
{
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

    public string ExtractText(byte[] image, string language)
    {
        using var engine = new TesseractEngine(_tessdataPath, language, EngineMode.Default);
        using var pix = Pix.LoadFromMemory(image);

        var prepared = Preprocess(pix);
        try
        {
            using var page = engine.Process(prepared);
            return page.GetText() ?? string.Empty;
        }
        finally
        {
            if (!ReferenceEquals(prepared, pix))
            {
                prepared.Dispose();
            }
        }
    }

    /// <summary>
    /// Grayscale + Otsu binarization. Returns the original Pix when preprocessing
    /// is disabled or fails, so OCR always has an image to work with.
    /// </summary>
    private Pix Preprocess(Pix source)
    {
        if (!_preprocess)
        {
            return source;
        }

        try
        {
            var gray = source.Depth > 8 ? source.ConvertRGBToGray() : source;
            var binary = gray.BinarizeOtsuAdaptiveThreshold(2000, 2000, 0, 0, 0.1f);
            if (!ReferenceEquals(gray, source))
            {
                gray.Dispose();
            }

            return binary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR preprocessing failed; falling back to the original image.");
            return source;
        }
    }
}

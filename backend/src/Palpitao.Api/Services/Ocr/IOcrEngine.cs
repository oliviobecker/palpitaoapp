namespace Palpitao.Api.Services.Ocr;

/// <summary>Abstraction over the OCR engine so it can be faked in tests.</summary>
public interface IOcrEngine
{
    string ExtractText(byte[] image, string language);
}

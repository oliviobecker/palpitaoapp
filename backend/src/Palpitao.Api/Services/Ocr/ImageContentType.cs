namespace Palpitao.Api.Services.Ocr;

/// <summary>
/// Identifies an image from its file header. The upload is validated by file name extension
/// (<see cref="OcrService.ValidateFile"/>), which says nothing about the actual bytes — and since
/// those bytes are now stored and served back to a browser, the real type has to come from the
/// content itself. Only the three formats the upload already allows are recognised.
/// </summary>
public static class ImageContentType
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    /// <summary>
    /// Detects the image type from <paramref name="bytes"/>. Returns false for anything that is
    /// not a PNG, JPEG or WebP, regardless of what the file is named.
    /// </summary>
    public static bool TryDetect(ReadOnlySpan<byte> bytes, out string contentType, out string extension)
    {
        if (StartsWith(bytes, PngSignature))
        {
            contentType = "image/png";
            extension = ".png";
            return true;
        }

        if (StartsWith(bytes, JpegSignature))
        {
            contentType = "image/jpeg";
            extension = ".jpg";
            return true;
        }

        // WebP is a RIFF container: "RIFF" <4-byte size> "WEBP". Checking only "RIFF" would also
        // accept WAV/AVI, so the form marker at offset 8 has to match too.
        if (StartsWith(bytes, RiffSignature) && bytes.Length >= 12 && StartsWith(bytes[8..], WebpSignature))
        {
            contentType = "image/webp";
            extension = ".webp";
            return true;
        }

        contentType = string.Empty;
        extension = string.Empty;
        return false;
    }

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature)
        => bytes.Length >= signature.Length && bytes[..signature.Length].SequenceEqual(signature);
}

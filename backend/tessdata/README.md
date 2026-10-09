# tessdata

Put the Tesseract (OCR) language files here:

- `por.traineddata` — Portuguese
- `eng.traineddata` — English

## Where to get them

Download the official `tessdata` models (`tessdata`, or `tessdata_fast` / `tessdata_best`):

- https://github.com/tesseract-ocr/tessdata
- por.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata
- eng.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata

Expected layout:

```
backend/tessdata/por.traineddata
backend/tessdata/eng.traineddata
```

The path can be overridden with the `Ocr:TessdataPath` setting (or the `Ocr__TessdataPath`
environment variable). Without the files the OCR import returns an error; the rest of the API keeps
working, and `GET /health/ocr` names the missing languages.

> The `*.traineddata` files are large binaries and are gitignored — each machine downloads its own.
> The native libraries (`leptonica`, `tesseract50.dll`) come with the `Tesseract` NuGet package.

## Servers (staging/production)

Not a manual step: the deploy workflows download both models here **before** `dotnet publish`,
pinned to commit `ced7875` of the `tessdata` repository and verified by SHA-256, so they ship inside
the publish output. **Do not copy anything onto the server by hand** — the deploy mirrors the
published folder with `robocopy /MIR` and deletes whatever did not come with it. To check an
environment: `GET /health/ocr`.

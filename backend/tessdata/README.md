# tessdata

Coloque aqui os arquivos de idioma do Tesseract (OCR):

- `por.traineddata` — Português
- `eng.traineddata` — Inglês

## Onde baixar

Baixe os modelos `tessdata` oficiais (versão `tessdata` ou `tessdata_fast`):

- https://github.com/tesseract-ocr/tessdata (ou `tessdata_fast` / `tessdata_best`)
- por.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/por.traineddata
- eng.traineddata: https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata

Estrutura esperada:

```
backend/tessdata/por.traineddata
backend/tessdata/eng.traineddata
```

O caminho pode ser sobrescrito via configuração `Ocr:TessdataPath`
(ou variável `Ocr__TessdataPath`). Sem esses arquivos, a importação por OCR
retornará erro (a API continua funcionando normalmente para o resto).

> Os `*.traineddata` são binários grandes e estão no `.gitignore` — cada máquina
> baixa os seus. Os comandos acima (`curl ... raw/main/...`) já deixam os arquivos
> no lugar esperado. O engine real foi validado nesta máquina (libs nativas
> `leptonica`/`tesseract50.dll` + modelos `por`/`eng`).

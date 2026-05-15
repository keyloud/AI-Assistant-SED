using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AssistantApi.Services.DocumentValidation;

public class DocumentValidationService : IDocumentValidationService
{
    private readonly DocumentValidationOptions _options;
    private readonly ILlmService? _llmService;
    private readonly ILogger<DocumentValidationService> _logger;

    public DocumentValidationService(
        IOptions<DocumentValidationOptions> options,
        ILlmService llmService,
        ILogger<DocumentValidationService> logger)
    {
        _options = options.Value;
        _llmService = llmService;
        _logger = logger;
    }

    public DocumentValidationService(
        IOptions<DocumentValidationOptions> options,
        ILogger<DocumentValidationService> logger)
    {
        _options = options.Value;
        _llmService = null;
        _logger = logger;
    }

    public async Task<DocumentValidationResponse> ValidateAsync(
        Stream fileStream,
        string fileName,
        string? documentTypeHint,
        bool summaryOnly,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        _logger.LogDebug($"Пайплайн валидации запущен: requestId={requestId}, файл={fileName}, расширение={extension}, подсказка типа={documentTypeHint ?? "<нет>"}");

        if (extension is not ".docx" and not ".pdf")
        {
            _logger.LogWarning($"Неподдерживаемый формат файла {fileName}: {extension}");
            return new DocumentValidationResponse
            {
                Status = "bad_request",
                ExtractedText = string.Empty,
                Remarks = [_options.UnsupportedFormatMessage]
            };
        }

        await using var sourceBuffer = new MemoryStream();
        await fileStream.CopyToAsync(sourceBuffer, ct);
        var fileBytes = sourceBuffer.ToArray();

        _logger.LogInformation($"[Шаг 1/5] Получен файл для анализа: requestId={requestId}, файл={fileName}, размер={fileBytes.Length} байт");

        _logger.LogInformation($"[Шаг 2/5] Запущено извлечение текста: requestId={requestId}, файл={fileName}");
        var extractedText = extension == ".docx"
            ? await ExtractDocxTextAsync(new MemoryStream(fileBytes), ct)
            : await ExtractPdfTextAsync(new MemoryStream(fileBytes), ct);

        var ocrUsed = false;
        if (extension == ".pdf" && extractedText.Length < _options.Ocr.MinExtractedTextLength && _options.Ocr.Enabled)
        {
            _logger.LogInformation($"[Шаг 2.1/5] Запущен OCR fallback: requestId={requestId}, файл={fileName}, текущая длина текста={extractedText.Length}");

            var ocrText = await TryExtractPdfTextWithOcrAsync(fileBytes, ct);
            if (!string.IsNullOrWhiteSpace(ocrText) && ocrText.Length > extractedText.Length)
            {
                extractedText = ocrText;
                ocrUsed = true;
            }
        }

        _logger.LogDebug($"Извлечение текста завершено для {fileName}: requestId={requestId}, длина извлеченного текста={extractedText.Length}, использован OCR={ocrUsed}");

        _logger.LogInformation($"[Шаг 3/5] Запущена ML-классификация: requestId={requestId}, файл={fileName}");
        var mlResult = await TryClassifyWithMlAsync(fileName, extractedText, ct);

        _logger.LogInformation($"[Шаг 3/5] ML-классификация завершена: requestId={requestId}, тип={mlResult?.DocumentType ?? "<нет>"}, уверенность={mlResult?.Confidence ?? 0}");

        _logger.LogInformation($"[Шаг 4/5] Подбор шаблона и формирование результата: requestId={requestId}, файл={fileName}");

        var template = ResolveTemplate(documentTypeHint, fileName, extractedText);
        if (mlResult is not null
            && mlResult.Confidence >= _options.Ml.ConfidenceThreshold
            && !string.IsNullOrWhiteSpace(mlResult.DocumentType))
        {
            var mlTemplate = _options.Templates.FirstOrDefault(t =>
                string.Equals(t.DisplayName, mlResult.DocumentType, StringComparison.OrdinalIgnoreCase));
            if (mlTemplate is not null)
            {
                template = mlTemplate;
            }
        }

        _logger.LogDebug($"Результат подбора шаблона для {fileName}: requestId={requestId}, шаблон={template?.DisplayName ?? "<не_найден>"}, mlТип={mlResult?.DocumentType ?? "<нет>"}, mlУверенность={mlResult?.Confidence ?? 0}");

        var remarks = summaryOnly
            ? new List<string>()
            : BuildValidationRemarks(template, extractedText, extension, ocrUsed);
        _logger.LogDebug($"Замечания валидации сформированы для {fileName}: requestId={requestId}, количество={remarks.Count}");

        var recommendations = BuildRecommendations(remarks, template?.DisplayName, ocrUsed, mlResult);
        var summary = BuildSummary(extractedText, template?.DisplayName, remarks, mlResult, summaryOnly);

        if (template is null)
        {
            if (!summaryOnly)
            {
                remarks.Add(_options.TemplateNotFoundMessage);
            }
            _logger.LogInformation($"[Шаг 5/5] Валидация завершена для {fileName}: requestId={requestId}, status=template_not_found, замечаний={remarks.Count}, время={sw.ElapsedMilliseconds}мс");

            return new DocumentValidationResponse
            {
                Status = "template_not_found",
                ClassificationConfidence = mlResult?.Confidence,
                OcrUsed = ocrUsed,
                ExtractedTextLength = extractedText.Length,
                ExtractedText = extractedText,
                Summary = summary,
                Recommendations = recommendations,
                Remarks = remarks
            };
        }

        var status = summaryOnly || remarks.Count == 0 ? "ok" : "needs_fix";
        _logger.LogInformation($"[Шаг 5/5] Валидация завершена для {fileName}: requestId={requestId}, статус={status}, типДокумента={template.DisplayName}, замечаний={remarks.Count}, время={sw.ElapsedMilliseconds}мс");

        return new DocumentValidationResponse
        {
            Status = status,
            DocumentType = template.DisplayName,
            ClassificationConfidence = mlResult?.Confidence,
            OcrUsed = ocrUsed,
            ExtractedTextLength = extractedText.Length,
            ExtractedText = extractedText,
            Summary = summary,
            Recommendations = recommendations,
            Remarks = remarks
        };
    }

    private List<string> BuildValidationRemarks(DocumentTemplateRule? template, string extractedText, string extension, bool ocrUsed)
    {
        var remarks = new List<string>();
        var noTextExtracted = string.IsNullOrWhiteSpace(extractedText);

        if (noTextExtracted)
        {
            remarks.Add(_options.TextExtractionFailedMessage);
            if (extension == ".pdf")
            {
                remarks.Add(_options.Ocr.Enabled ? _options.OcrFailedMessage : _options.PdfOcrRecommendationMessage);
            }

            _logger.LogDebug($"Не удалось извлечь текст из содержимого файла. Расширение={extension}");

            return remarks;
        }

        if (template is null)
        {
            return remarks;
        }

        var normalized = extractedText.ToLowerInvariant();
        foreach (var keyword in template.RequiredKeywords.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            if (!normalized.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal))
            {
                remarks.Add($"Не найден обязательный реквизит: {keyword}.");
                _logger.LogDebug($"Отсутствует обязательное ключевое слово: {keyword}");
            }
        }

        return remarks;
    }

    private List<string> BuildRecommendations(
        List<string> remarks,
        string? documentType,
        bool ocrUsed,
        MlClassificationResult? mlResult)
    {
        var recommendations = new List<string>();

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            recommendations.Add($"Проверьте документ по шаблону типа '{documentType}'.");
        }

        if (ocrUsed)
        {
            recommendations.Add("OCR был применен к PDF. Рекомендуется визуально проверить критичные реквизиты.");
        }

        if (remarks.Any(r => r.Contains("реквизит", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Дополните обязательные реквизиты и повторите проверку.");
        }

        if (mlResult?.Recommendations is { Count: > 0 })
        {
            recommendations.AddRange(mlResult.Recommendations);
        }

        return recommendations
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private string BuildSummary(
        string extractedText,
        string? documentType,
        List<string> remarks,
        MlClassificationResult? mlResult,
        bool summaryOnly)
    {
        if (_options.Ml.EnableSummary && !string.IsNullOrWhiteSpace(mlResult?.Summary))
        {
            return mlResult.Summary;
        }

        var typeText = string.IsNullOrWhiteSpace(documentType) ? "тип документа не определен" : $"тип: {documentType}";
        var snippet = extractedText.Length > 180 ? extractedText[..180] + "..." : extractedText;

        if (string.IsNullOrWhiteSpace(snippet))
        {
            return summaryOnly
                ? $"Анализ завершен, {typeText}."
                : $"Проверка завершена, {typeText}, {(remarks.Count == 0 ? "замечаний не найдено" : $"замечаний: {remarks.Count}")}.";
        }

        return summaryOnly
            ? $"Анализ завершен, {typeText}. Кратко: {snippet}"
            : $"Проверка завершена, {typeText}, {(remarks.Count == 0 ? "замечаний не найдено" : $"замечаний: {remarks.Count}")}. Кратко: {snippet}";
    }

    private async Task<string?> TryExtractPdfTextWithOcrAsync(byte[] fileBytes, CancellationToken ct)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"docval-in-{Guid.NewGuid():N}.pdf");
        var outputPath = Path.Combine(Path.GetTempPath(), $"docval-out-{Guid.NewGuid():N}.pdf");
        Process? process = null;
        var ocrSw = Stopwatch.StartNew();

        try
        {
            await File.WriteAllBytesAsync(inputPath, fileBytes, ct);

            var arguments = _options.Ocr.ArgumentsTemplate
                .Replace("{input}", $"\"{inputPath}\"", StringComparison.Ordinal)
                .Replace("{output}", $"\"{outputPath}\"", StringComparison.Ordinal)
                .Replace("{lang}", _options.Ocr.Language, StringComparison.Ordinal);

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.Ocr.Command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _logger.LogInformation($"OCR запущен: команда={_options.Ocr.Command}, таймаут={_options.Ocr.TimeoutSeconds}с, вход={inputPath}, выход={outputPath}");

            if (!process.Start())
            {
                _logger.LogWarning("Не удалось запустить OCR-процесс для PDF-файла");
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.Ocr.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            var exitTask = process.WaitForExitAsync(linkedCts.Token);
            while (!exitTask.IsCompleted)
            {
                var tickTask = Task.Delay(TimeSpan.FromSeconds(15), linkedCts.Token);
                await Task.WhenAny(exitTask, tickTask);

                if (!exitTask.IsCompleted)
                {
                    _logger.LogInformation($"OCR все еще выполняется: pid={process.Id}, прошло={ocrSw.ElapsedMilliseconds}мс", process.Id, ocrSw.ElapsedMilliseconds);
                }
            }

            await exitTask;

            _logger.LogInformation($"OCR завершен: pid={process.Id}, код={process.ExitCode}, время={ocrSw.ElapsedMilliseconds}мс", process.Id, process.ExitCode, ocrSw.ElapsedMilliseconds);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning($"OCR-процесс завершился с кодом {process.ExitCode}: {error}");
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogWarning("OCR-процесс завершился без выходного файла");
                return null;
            }

            _logger.LogInformation("OCR завершен успешно, начинается извлечение текста из OCR-PDF");
            var ocrPdfBytes = await File.ReadAllBytesAsync(outputPath, ct);
            var ocrText = await ExtractPdfTextAsync(new MemoryStream(ocrPdfBytes), ct);
            _logger.LogInformation($"Извлечение текста из OCR-PDF завершено: длина текста={ocrText.Length}", ocrText.Length);
            return string.IsNullOrWhiteSpace(ocrText) ? null : ocrText;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Достигнут таймаут OCR-процесса для PDF-файла");
            if (process is not null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    _logger.LogWarning("OCR-процесс принудительно остановлен после таймаута");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось принудительно завершить OCR-процесс после таймаута");
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка выполнения OCR для PDF-файла");
            return null;
        }
        finally
        {
            process?.Dispose();
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private async Task<MlClassificationResult?> TryClassifyWithMlAsync(
        string fileName,
        string extractedText,
        CancellationToken ct)
    {
        if (!_options.Ml.Enabled || _llmService is null || string.IsNullOrWhiteSpace(extractedText))
        {
            return null;
        }

        var allowedTypes = _options.Templates
            .Select(t => t.DisplayName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowedTypes.Count == 0)
        {
            return null;
        }

        string sample;
        var maxChars = _options.Ml.MaxInputChars;
        if (extractedText.Length <= maxChars)
        {
            sample = extractedText;
        }
        else
        {
            // Формируем сэмпл из начала / середины / конца документа
            // Пропорции: 20% начало, 40% середина, 40% конец — суммарно ~= maxChars
            var total = maxChars;
            var part1Len = Math.Max(1, (int)Math.Round(total * 0.2));
            var part2Len = Math.Max(1, (int)Math.Round(total * 0.4));
            var part3Len = total - part1Len - part2Len;
            if (part3Len < 1)
            {
                part3Len = 1;
                part2Len = Math.Max(1, total - part1Len - part3Len);
            }

            var len = extractedText.Length;
            var part1 = extractedText.Substring(0, Math.Min(part1Len, len));

            var midStart = Math.Max(0, (len - part2Len) / 2);
            var part2 = extractedText.Substring(midStart, Math.Min(part2Len, Math.Max(0, len - midStart)));

            var part3Start = Math.Max(0, len - part3Len);
            var part3 = extractedText.Substring(part3Start, Math.Min(part3Len, Math.Max(0, len - part3Start)));

            sample =
                "[Начало документа]\n" + part1 +
                "\n\n[Средняя часть документа]\n" + part2 +
                "\n\n[Конец документа]\n" + part3;
        }

        var prompt = $@"Ты — интеллектуальный ассистент системы электронного документооборота фармацевтической компании Psuti-Pharms.
Твоя задача:
1) Определить тип документа
2) Сформировать понятную и полезную выжимку документа
3) Дать рекомендации при необходимости

Верни строго JSON без пояснений.
Разрешенные documentType: {string.Join(", ", allowedTypes)}.
Формат ответа:
{{
    ""documentType"": ""одно из разрешенных значений"",
    ""confidence"": 0.0,
    ""summary"": ""понятная выжимка документа для сотрудника компании"",
    ""recommendations"": [""совет 1"", ""совет 2""]
}}

Требования к summary:
- Пиши простым и понятным языком
- Не используй канцеляризмы
- Объясняй смысл документа, а не пересказывай его
- Указывай важные изменения, сроки, ограничения и последствия
- Пиши как внутренний корпоративный ассистент
- Summary должен читаться естественно и не быть сухим
- Допустимо 3-5 предложений

Имя файла: {fileName}
Текст документа:
{sample}";

        try
        {
            var raw = await _llmService.GenerateAsync(
                prompt,
                history: null,
                temperature: _options.Ml.SummaryTemperature,
                topP: _options.Ml.SummaryTopP,
                ct: ct);
            var json = ExtractJsonObject(raw);
            if (json is null)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var documentType = root.TryGetProperty("documentType", out var typeNode)
                ? typeNode.GetString()
                : null;

            var confidence = root.TryGetProperty("confidence", out var confidenceNode)
                && confidenceNode.ValueKind is JsonValueKind.Number
                ? confidenceNode.GetSingle()
                : 0f;

            var summary = root.TryGetProperty("summary", out var summaryNode)
                ? summaryNode.GetString()
                : null;

            var recommendations = new List<string>();
            if (root.TryGetProperty("recommendations", out var recNode) && recNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in recNode.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        recommendations.Add(item.GetString()!);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(documentType))
            {
                return null;
            }

            return new MlClassificationResult(
                documentType,
                Math.Clamp(confidence, 0f, 1f),
                summary,
                recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"ML-классификация завершилась ошибкой для файла {fileName}");
            return null;
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return raw[start..(end + 1)];
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors.
        }
    }

    private DocumentTemplateRule? ResolveTemplate(string? hint, string fileName, string text)
    {
        var templates = _options.Templates
            .Where(t => !string.IsNullOrWhiteSpace(t.DisplayName))
            .ToList();

        if (templates.Count == 0)
        {
            return null;
        }

        var joined = $"{hint} {fileName} {text}".ToLowerInvariant();
        DocumentTemplateRule? bestMatch = null;
        var bestScore = 0;

        foreach (var template in templates)
        {
            var score = 0;
            foreach (var keyword in template.DetectionKeywords.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                if (joined.Contains(keyword.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    score++;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = template;
            }
        }

        return bestScore > 0 ? bestMatch : null;
    }

    private async Task<string> ExtractDocxTextAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var documentEntry = archive.GetEntry("word/document.xml");
            if (documentEntry is null)
            {
                return string.Empty;
            }

            await using var entryStream = documentEntry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var xml = await reader.ReadToEndAsync(ct);

            var withParagraphs = Regex.Replace(xml, "</w:p>", "\n", RegexOptions.IgnoreCase);
            var withoutTags = Regex.Replace(withParagraphs, "<[^>]+>", " ");
            var decoded = WebUtility.HtmlDecode(withoutTags);

            return NormalizeText(decoded);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось извлечь текст из DOCX-файла");
            return string.Empty;
        }
    }

    private async Task<string> ExtractPdfTextAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            var fileBytes = memory.ToArray();

            // Primary strategy: pdftotext handles most OCR-generated PDFs correctly.
            var pdftotextResult = await TryExtractPdfTextWithPdftotextAsync(fileBytes, ct);
            if (!string.IsNullOrWhiteSpace(pdftotextResult))
            {
                _logger.LogDebug($"PDF-текст извлечен через pdftotext: длина={pdftotextResult.Length}");
                return pdftotextResult;
            }

            _logger.LogDebug("pdftotext не вернул текст, включен regex-fallback");

            // Fallback strategy: low-level extraction for edge-case PDFs.
            var raw = Encoding.Latin1.GetString(fileBytes);
            var sb = new StringBuilder();
            foreach (Match match in Regex.Matches(raw, @"\((?<text>(?:\\.|[^\\\)])*)\)\s*Tj", RegexOptions.Singleline))
            {
                var value = match.Groups["text"].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sb.AppendLine(UnescapePdfText(value));
                }
            }

            foreach (Match match in Regex.Matches(raw, @"\[(?<parts>.*?)\]\s*TJ", RegexOptions.Singleline))
            {
                var parts = match.Groups["parts"].Value;
                var line = new StringBuilder();

                foreach (Match textPart in Regex.Matches(parts, @"\((?<text>(?:\\.|[^\\\)])*)\)"))
                {
                    line.Append(UnescapePdfText(textPart.Groups["text"].Value));
                }

                if (line.Length > 0)
                {
                    sb.AppendLine(line.ToString());
                }
            }

            var fallbackText = NormalizeText(sb.ToString());
            _logger.LogDebug($"Regex-fallback извлечение PDF завершено: длина={fallbackText.Length}");

            return fallbackText;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось извлечь текст из PDF-файла");
            return string.Empty;
        }
    }

    private async Task<string?> TryExtractPdfTextWithPdftotextAsync(byte[] fileBytes, CancellationToken ct)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"docval-pdf-{Guid.NewGuid():N}.pdf");
        Process? process = null;

        try
        {
            await File.WriteAllBytesAsync(inputPath, fileBytes, ct);

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pdftotext",
                    Arguments = $"-enc UTF-8 -layout \"{inputPath}\" -",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                _logger.LogDebug("Не удалось запустить pdftotext");
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogDebug($"pdftotext завершился с кодом {process.ExitCode}: {error}");
                return null;
            }

            var text = await process.StandardOutput.ReadToEndAsync(ct);
            return NormalizeText(text);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("pdftotext прерван по таймауту или отмене");
            if (process is not null && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, $"Ошибка запуска pdftotext");
            return null;
        }
        finally
        {
            process?.Dispose();
            TryDelete(inputPath);
        }
    }

    private static string UnescapePdfText(string value)
    {
        return value
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, @"\s+", " ").Trim();
    }

    private sealed record MlClassificationResult(
        string DocumentType,
        float Confidence,
        string? Summary,
        List<string> Recommendations);
}

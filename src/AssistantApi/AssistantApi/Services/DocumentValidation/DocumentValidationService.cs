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
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        _logger.LogDebug(
            "Validation pipeline started: file={FileName}, extension={Extension}, hint={DocumentTypeHint}",
            fileName,
            extension,
            documentTypeHint ?? "<none>");

        if (extension is not ".docx" and not ".pdf")
        {
            _logger.LogWarning("Unsupported format for {FileName}: {Extension}", fileName, extension);
            return new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = [_options.UnsupportedFormatMessage]
            };
        }

        await using var sourceBuffer = new MemoryStream();
        await fileStream.CopyToAsync(sourceBuffer, ct);
        var fileBytes = sourceBuffer.ToArray();

        var extractedText = extension == ".docx"
            ? await ExtractDocxTextAsync(new MemoryStream(fileBytes), ct)
            : await ExtractPdfTextAsync(new MemoryStream(fileBytes), ct);

        var ocrUsed = false;
        if (extension == ".pdf"
            && extractedText.Length < _options.Ocr.MinExtractedTextLength
            && _options.Ocr.Enabled)
        {
            var ocrText = await TryExtractPdfTextWithOcrAsync(fileBytes, ct);
            if (!string.IsNullOrWhiteSpace(ocrText) && ocrText.Length > extractedText.Length)
            {
                extractedText = ocrText;
                ocrUsed = true;
            }
        }

        _logger.LogDebug(
            "Text extraction finished for {FileName}: extractedTextLength={ExtractedTextLength}, ocrUsed={OcrUsed}",
            fileName,
            extractedText.Length,
            ocrUsed);

        var mlResult = await TryClassifyWithMlAsync(fileName, extractedText, ct);
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

        _logger.LogDebug(
            "Template resolve result for {FileName}: template={Template}, mlType={MlType}, mlConfidence={MlConfidence}",
            fileName,
            template?.DisplayName ?? "<not_found>",
            mlResult?.DocumentType ?? "<none>",
            mlResult?.Confidence ?? 0);

        var remarks = BuildValidationRemarks(template, extractedText, extension, ocrUsed);
        _logger.LogDebug(
            "Validation remarks built for {FileName}: remarksCount={RemarksCount}",
            fileName,
            remarks.Count);

        var recommendations = BuildRecommendations(remarks, template?.DisplayName, ocrUsed, mlResult);
        var summary = BuildSummary(extractedText, template?.DisplayName, remarks, mlResult);

        if (template is null)
        {
            remarks.Add(_options.TemplateNotFoundMessage);
            _logger.LogInformation(
                "Validation finished for {FileName}: status=template_not_found, remarksCount={RemarksCount}, elapsedMs={ElapsedMs}",
                fileName,
                remarks.Count,
                sw.ElapsedMilliseconds);

            return new DocumentValidationResponse
            {
                Status = "template_not_found",
                ClassificationConfidence = mlResult?.Confidence,
                OcrUsed = ocrUsed,
                ExtractedTextLength = extractedText.Length,
                Summary = summary,
                Recommendations = recommendations,
                Remarks = remarks
            };
        }

        var status = remarks.Count == 0 ? "ok" : "needs_fix";
        _logger.LogInformation(
            "Validation finished for {FileName}: status={Status}, documentType={DocumentType}, remarksCount={RemarksCount}, elapsedMs={ElapsedMs}",
            fileName,
            status,
            template.DisplayName,
            remarks.Count,
            sw.ElapsedMilliseconds);

        return new DocumentValidationResponse
        {
            Status = status,
            DocumentType = template.DisplayName,
            ClassificationConfidence = mlResult?.Confidence,
            OcrUsed = ocrUsed,
            ExtractedTextLength = extractedText.Length,
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

            _logger.LogDebug("No text extracted from file content. Extension={Extension}", extension);

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
                _logger.LogDebug("Missing required keyword: {Keyword}", keyword);
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
        MlClassificationResult? mlResult)
    {
        if (_options.Ml.EnableSummary && !string.IsNullOrWhiteSpace(mlResult?.Summary))
        {
            return mlResult.Summary;
        }

        var typeText = string.IsNullOrWhiteSpace(documentType) ? "тип документа не определен" : $"тип: {documentType}";
        var remarksText = remarks.Count == 0 ? "замечаний не найдено" : $"замечаний: {remarks.Count}";
        var snippet = extractedText.Length > 180 ? extractedText[..180] + "..." : extractedText;

        if (string.IsNullOrWhiteSpace(snippet))
        {
            return $"Проверка завершена, {typeText}, {remarksText}.";
        }

        return $"Проверка завершена, {typeText}, {remarksText}. Кратко: {snippet}";
    }

    private async Task<string?> TryExtractPdfTextWithOcrAsync(byte[] fileBytes, CancellationToken ct)
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"docval-in-{Guid.NewGuid():N}.pdf");
        var outputPath = Path.Combine(Path.GetTempPath(), $"docval-out-{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllBytesAsync(inputPath, fileBytes, ct);

            var arguments = _options.Ocr.ArgumentsTemplate
                .Replace("{input}", $"\"{inputPath}\"", StringComparison.Ordinal)
                .Replace("{output}", $"\"{outputPath}\"", StringComparison.Ordinal)
                .Replace("{lang}", _options.Ocr.Language, StringComparison.Ordinal);

            using var process = new Process
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

            if (!process.Start())
            {
                _logger.LogWarning("Failed to start OCR process for PDF file");
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.Ocr.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token);

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync(ct);
                _logger.LogWarning("OCR process failed with code {ExitCode}: {Error}", process.ExitCode, error);
                return null;
            }

            if (!File.Exists(outputPath))
            {
                _logger.LogWarning("OCR process finished without output file");
                return null;
            }

            var ocrPdfBytes = await File.ReadAllBytesAsync(outputPath, ct);
            var ocrText = await ExtractPdfTextAsync(new MemoryStream(ocrPdfBytes), ct);
            return string.IsNullOrWhiteSpace(ocrText) ? null : ocrText;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OCR process timeout reached for PDF file");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR execution failed for PDF file");
            return null;
        }
        finally
        {
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

        var sample = extractedText.Length > _options.Ml.MaxInputChars
            ? extractedText[.._options.Ml.MaxInputChars]
            : extractedText;

                var prompt = $@"Ты классификатор документов СЭД. Верни строго JSON без пояснений.
Разрешенные documentType: {string.Join(", ", allowedTypes)}.
Формат ответа:
{{
    ""documentType"": ""одно из разрешенных значений"",
    ""confidence"": 0.0,
    ""summary"": ""краткая выжимка 1-2 предложения"",
    ""recommendations"": [""совет 1"", ""совет 2""]
}}

Имя файла: {fileName}
Текст документа:
{sample}";

        try
        {
            var raw = await _llmService.GenerateAsync(prompt, history: null, ct);
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
            _logger.LogWarning(ex, "ML classification failed for {FileName}", fileName);
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
            _logger.LogWarning(ex, "Failed to extract text from DOCX file");
            return string.Empty;
        }
    }

    private async Task<string> ExtractPdfTextAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct);
            var raw = Encoding.Latin1.GetString(memory.ToArray());
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

            return NormalizeText(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from PDF file");
            return string.Empty;
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

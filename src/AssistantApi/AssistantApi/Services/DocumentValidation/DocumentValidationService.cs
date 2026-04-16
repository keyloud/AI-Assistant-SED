using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using AssistantApi.Models.Responses;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace AssistantApi.Services.DocumentValidation;

public class DocumentValidationService : IDocumentValidationService
{
    private readonly DocumentValidationOptions _options;
    private readonly ILogger<DocumentValidationService> _logger;

    public DocumentValidationService(
        IOptions<DocumentValidationOptions> options,
        ILogger<DocumentValidationService> logger)
    {
        _options = options.Value;
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

        var extractedText = extension == ".docx"
            ? await ExtractDocxTextAsync(fileStream, ct)
            : await ExtractPdfTextAsync(fileStream, ct);

        _logger.LogDebug(
            "Text extraction finished for {FileName}: extractedTextLength={ExtractedTextLength}",
            fileName,
            extractedText.Length);

        var template = ResolveTemplate(documentTypeHint, fileName, extractedText);
        _logger.LogDebug(
            "Template resolve result for {FileName}: template={Template}",
            fileName,
            template?.DisplayName ?? "<not_found>");

        var remarks = BuildValidationRemarks(template, extractedText, extension);
        _logger.LogDebug(
            "Validation remarks built for {FileName}: remarksCount={RemarksCount}",
            fileName,
            remarks.Count);

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
                ExtractedTextLength = extractedText.Length,
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
            ExtractedTextLength = extractedText.Length,
            Remarks = remarks
        };
    }

    private List<string> BuildValidationRemarks(DocumentTemplateRule? template, string extractedText, string extension)
    {
        var remarks = new List<string>();
        var noTextExtracted = string.IsNullOrWhiteSpace(extractedText);

        if (noTextExtracted)
        {
            remarks.Add(_options.TextExtractionFailedMessage);
            if (extension == ".pdf")
            {
                remarks.Add(_options.PdfOcrRecommendationMessage);
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
}

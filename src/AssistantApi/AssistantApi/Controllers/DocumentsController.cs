using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private const string TemplateNotFoundMessage = "Эталонный шаблон не найден. Обратитесь к специалисту.";

    private static readonly Dictionary<DocumentKind, (string DisplayName, string[] RequiredKeywords)> RequiredRules =
        new()
        {
            [DocumentKind.Order] = ("приказ", new[] { "приказ", "номер", "дата", "основан", "подпись" }),
            [DocumentKind.PowerOfAttorney] = ("доверенность", new[] { "доверенность", "доверител", "представител", "паспорт", "дата", "подпись" }),
            [DocumentKind.Contract] = ("договор", new[] { "договор", "предмет", "сторон", "срок", "подпись" })
        };

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Validate([FromForm] DocumentValidateRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = { "Файл не передан." }
            });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension is not ".docx" and not ".pdf")
        {
            return BadRequest(new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = { "Поддерживаются только форматы DOCX и PDF." }
            });
        }

        await using var stream = request.File.OpenReadStream();
        var extractedText = extension == ".docx"
            ? await ExtractDocxTextAsync(stream, ct)
            : await ExtractPdfTextAsync(stream, ct);

        var resolvedKind = ResolveDocumentKind(request.DocumentTypeHint, request.File.FileName, extractedText);
        if (resolvedKind == DocumentKind.Unknown)
        {
            return Ok(new DocumentValidationResponse
            {
                Status = "template_not_found",
                ExtractedTextLength = extractedText.Length,
                Remarks = { TemplateNotFoundMessage }
            });
        }

        var validationRemarks = ValidateRequiredRequisites(resolvedKind, extractedText);
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            validationRemarks.Insert(0, "Не удалось извлечь текст из файла. Проверьте качество файла или формат содержимого.");
        }

        return Ok(new DocumentValidationResponse
        {
            Status = validationRemarks.Count == 0 ? "ok" : "needs_fix",
            DocumentType = RequiredRules[resolvedKind].DisplayName,
            ExtractedTextLength = extractedText.Length,
            Remarks = validationRemarks
        });
    }

    private static async Task<string> ExtractDocxTextAsync(Stream stream, CancellationToken ct)
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
        catch
        {
            return string.Empty;
        }
    }

    private static async Task<string> ExtractPdfTextAsync(Stream stream, CancellationToken ct)
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
        catch
        {
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

    private static DocumentKind ResolveDocumentKind(string? hint, string fileName, string text)
    {
        var joined = $"{hint} {fileName} {text}".ToLowerInvariant();

        if (joined.Contains("доверенн", StringComparison.Ordinal))
        {
            return DocumentKind.PowerOfAttorney;
        }

        if (joined.Contains("договор", StringComparison.Ordinal))
        {
            return DocumentKind.Contract;
        }

        if (joined.Contains("приказ", StringComparison.Ordinal))
        {
            return DocumentKind.Order;
        }

        return DocumentKind.Unknown;
    }

    private static List<string> ValidateRequiredRequisites(DocumentKind kind, string text)
    {
        var remarks = new List<string>();
        var normalized = text.ToLowerInvariant();
        var rules = RequiredRules[kind];

        foreach (var keyword in rules.RequiredKeywords)
        {
            if (!normalized.Contains(keyword, StringComparison.Ordinal))
            {
                remarks.Add($"Не найден обязательный реквизит: {keyword}.");
            }
        }

        return remarks;
    }

    private enum DocumentKind
    {
        Unknown,
        Order,
        PowerOfAttorney,
        Contract
    }
}

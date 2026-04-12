using System.IO.Compression;
using System.Text;
using AssistantApi.Services.DocumentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AssistantApi.Tests.Services;

public class DocumentValidationServiceTests
{
    private readonly DocumentValidationService _service;

    public DocumentValidationServiceTests()
    {
        var options = Options.Create(new DocumentValidationOptions());
        _service = new DocumentValidationService(options, NullLogger<DocumentValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsBadRequest_ForUnsupportedFileFormat()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text"));

        var result = await _service.ValidateAsync(stream, "note.txt", null);

        Assert.Equal("bad_request", result.Status);
        Assert.Contains("Поддерживаются только форматы DOCX и PDF.", result.Remarks);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsOk_WhenAllRequiredKeywordsPresent()
    {
        await using var stream = CreateDocx(
            "Договор номер 1 дата 01.01.2026 предмет поставки стороны согласовали срок и подпись.");

        var result = await _service.ValidateAsync(stream, "dogovor.docx", "договор");

        Assert.Equal("ok", result.Status);
        Assert.Equal("договор", result.DocumentType);
        Assert.Empty(result.Remarks);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsNeedsFix_WhenRequiredKeywordMissing()
    {
        await using var stream = CreateDocx(
            "Договор номер 1 дата 01.01.2026 предмет поставки стороны согласовали подпись.");

        var result = await _service.ValidateAsync(stream, "dogovor.docx", "договор");

        Assert.Equal("needs_fix", result.Status);
        Assert.Contains(result.Remarks, r => r.Contains("срок", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsTemplateNotFound_WhenTypeCannotBeResolved()
    {
        await using var stream = CreateDocx("Служебная записка на согласование.");

        var result = await _service.ValidateAsync(stream, "note.docx", null);

        Assert.Equal("template_not_found", result.Status);
        Assert.Contains("Эталонный шаблон не найден", string.Join(" ", result.Remarks));
    }

    [Fact]
    public async Task ValidateAsync_AddsOcrRecommendation_ForPdfWithoutExtractedText()
    {
        await using var stream = new MemoryStream(Encoding.Latin1.GetBytes("%PDF-1.4 empty content"));

        var result = await _service.ValidateAsync(stream, "scan.pdf", "договор");

        Assert.Equal("needs_fix", result.Status);
        Assert.Contains(result.Remarks, r => r.Contains("OCR", StringComparison.OrdinalIgnoreCase));
    }

    private static MemoryStream CreateDocx(string text)
    {
        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write($"""<?xml version="1.0" encoding="UTF-8"?><w:document><w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body></w:document>""");
        }

        memory.Position = 0;
        return memory;
    }
}

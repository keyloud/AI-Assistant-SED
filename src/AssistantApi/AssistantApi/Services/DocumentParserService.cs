using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using AssistantApi.Services.Interfaces;

namespace AssistantApi.Services;

/// <summary>
/// Извлекает текст из загруженных документов.
/// Поддерживает форматы: .txt, .docx
/// </summary>
public class DocumentParserService : IDocumentParserService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".docx" };

    public async Task<string> ExtractTextAsync(
        Stream fileStream,
        string fileName,
        CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        if (!SupportedExtensions.Contains(extension))
            throw new NotSupportedException(
                $"Формат файла '{extension}' не поддерживается. Поддерживаемые форматы: .txt, .docx");

        return extension.ToLowerInvariant() switch
        {
            ".txt"  => await ExtractFromTxtAsync(fileStream, ct),
            ".docx" => await ExtractFromDocxAsync(fileStream),
            _       => throw new InvalidOperationException($"Unexpected extension '{extension}'")
        };
    }

    private static async Task<string> ExtractFromTxtAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static Task<string> ExtractFromDocxAsync(Stream stream)
    {
        // DOCX — это ZIP-архив, основной документ находится в word/document.xml
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var documentEntry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidOperationException("Некорректный DOCX файл: 'word/document.xml' не найден");

        using var entryStream = documentEntry.Open();
        var doc = XDocument.Load(entryStream);

        // Пространство имён WordprocessingML
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var sb = new StringBuilder();
        foreach (var paragraph in doc.Descendants(w + "p"))
        {
            var paragraphText = string.Concat(paragraph.Descendants(w + "t").Select(t => t.Value));
            if (!string.IsNullOrWhiteSpace(paragraphText))
            {
                sb.AppendLine(paragraphText);
            }
        }

        return Task.FromResult(sb.ToString());
    }
}

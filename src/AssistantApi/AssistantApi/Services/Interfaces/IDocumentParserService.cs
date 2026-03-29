namespace AssistantApi.Services.Interfaces;

/// <summary>
/// Сервис для извлечения текстового содержимого из загруженных файлов документов.
/// Поддерживаемые форматы: .txt, .docx
/// </summary>
public interface IDocumentParserService
{
    /// <summary>
    /// Извлекает текст из потока файла.
    /// </summary>
    /// <param name="fileStream">Поток данных файла.</param>
    /// <param name="fileName">Имя файла (используется для определения формата по расширению).</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Строка с извлечённым текстом.</returns>
    Task<string> ExtractTextAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.DocumentValidation;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private const int ValidationTimeoutSeconds = 240;

    private readonly IDocumentValidationService _documentValidationService;
    private readonly DocumentValidationOptions _documentValidationOptions;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentValidationService documentValidationService,
        IOptions<DocumentValidationOptions> documentValidationOptions,
        ILogger<DocumentsController> logger)
    {
        _documentValidationService = documentValidationService;
        _documentValidationOptions = documentValidationOptions.Value;
        _logger = logger;
    }

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Validate([FromForm] DocumentValidateRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (request.File is null || request.File.Length == 0)
        {
            _logger.LogWarning("Проверка документа отклонена: передан пустой файл");
            return BadRequest(new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = { _documentValidationOptions.EmptyFileMessage }
            });
        }

        _logger.LogInformation(
            "Запущена проверка документа {FileName} ({FileSize} байт), подсказка типа={DocumentTypeHint}",
            request.File.FileName,
            request.File.Length,
            request.DocumentTypeHint ?? "<нет>");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(ValidationTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        DocumentValidationResponse response;

        try
        {
            await using var stream = request.File.OpenReadStream();
            response = await _documentValidationService.ValidateAsync(
                stream,
                request.File.FileName,
                request.DocumentTypeHint,
                request.SummaryOnly,
                linkedCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Проверка документа прервана по таймауту: файл={FileName}, лимит={TimeoutSeconds}с, elapsed={ElapsedMs}мс",
                request.File.FileName,
                ValidationTimeoutSeconds,
                sw.ElapsedMilliseconds);

            return StatusCode(StatusCodes.Status504GatewayTimeout, new DocumentValidationResponse
            {
                Status = "timeout",
                Summary = "Проверка документа заняла слишком много времени и была остановлена по таймауту.",
                Remarks = { "Сервис не завершил обработку документа в отведенное время. Попробуйте повторить позже." }
            });
        }

        _logger.LogInformation(
            "Проверка документа завершена для {FileName}: статус={Status}, тип={DocumentType}, замечаний={RemarksCount}, длина извлеченного текста={ExtractedTextLength}, время={ElapsedMs}мс",
            request.File.FileName,
            response.Status,
            response.DocumentType ?? "<неизвестно>",
            response.Remarks.Count,
            response.ExtractedTextLength,
            sw.ElapsedMilliseconds);

        return response.Status == "bad_request" ? BadRequest(response) : Ok(response);
    }
}

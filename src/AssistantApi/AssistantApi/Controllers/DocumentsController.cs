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
            _logger.LogWarning("Document validation rejected: empty file payload");
            return BadRequest(new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = { _documentValidationOptions.EmptyFileMessage }
            });
        }

        _logger.LogInformation(
            "Document validation started for {FileName} ({FileSize} bytes), hint={DocumentTypeHint}",
            request.File.FileName,
            request.File.Length,
            request.DocumentTypeHint ?? "<none>");

        await using var stream = request.File.OpenReadStream();
        var response = await _documentValidationService.ValidateAsync(
            stream,
            request.File.FileName,
            request.DocumentTypeHint,
            ct);

        _logger.LogInformation(
            "Document validation finished for {FileName}: status={Status}, docType={DocumentType}, remarks={RemarksCount}, extractedTextLength={ExtractedTextLength}, elapsedMs={ElapsedMs}",
            request.File.FileName,
            response.Status,
            response.DocumentType ?? "<unknown>",
            response.Remarks.Count,
            response.ExtractedTextLength,
            sw.ElapsedMilliseconds);

        return response.Status == "bad_request" ? BadRequest(response) : Ok(response);
    }
}

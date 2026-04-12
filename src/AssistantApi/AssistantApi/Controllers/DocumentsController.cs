using AssistantApi.Models.Requests;
using AssistantApi.Models.Responses;
using AssistantApi.Services.DocumentValidation;
using AssistantApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

namespace AssistantApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentValidationService _documentValidationService;
    private readonly DocumentValidationOptions _documentValidationOptions;

    public DocumentsController(
        IDocumentValidationService documentValidationService,
        IOptions<DocumentValidationOptions> documentValidationOptions)
    {
        _documentValidationService = documentValidationService;
        _documentValidationOptions = documentValidationOptions.Value;
    }

    [HttpPost("validate")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Validate([FromForm] DocumentValidateRequest request, CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new DocumentValidationResponse
            {
                Status = "bad_request",
                Remarks = { _documentValidationOptions.EmptyFileMessage }
            });
        }

        await using var stream = request.File.OpenReadStream();
        var response = await _documentValidationService.ValidateAsync(
            stream,
            request.File.FileName,
            request.DocumentTypeHint,
            ct);

        return response.Status == "bad_request" ? BadRequest(response) : Ok(response);
    }
}

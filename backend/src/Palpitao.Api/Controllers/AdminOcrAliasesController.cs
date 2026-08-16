using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Palpitao.Api.Auth;
using Palpitao.Api.DTOs.Admin;
using Palpitao.Api.Services.Ocr;

namespace Palpitao.Api.Controllers;

/// <summary>
/// The names OCR import has been taught to read as a participant. They are learned automatically
/// when an admin confirms a batch after correcting a name; this is where they can be inspected,
/// re-pointed, removed, or taught up front.
/// </summary>
[ApiController]
[Route("admin/ocr-aliases")]
[Authorize]
[RequireGroupAdmin]
public class AdminOcrAliasesController : ControllerBase
{
    private readonly IOcrAliasService _aliases;

    public AdminOcrAliasesController(IOcrAliasService aliases)
    {
        _aliases = aliases;
    }

    /// <summary>Every alias of the current group.</summary>
    [HttpGet]
    public async Task<ActionResult<List<OcrParticipantAliasDto>>> List(CancellationToken ct)
        => Ok(await _aliases.ListAsync(ct));

    /// <summary>Teaches an alias by hand.</summary>
    [HttpPost]
    public async Task<ActionResult<OcrParticipantAliasDto>> Create(
        CreateOcrParticipantAliasRequest request, CancellationToken ct)
        => Ok(await _aliases.CreateAsync(request, User.GetUserId(), ct));

    /// <summary>Re-points an alias at another participant.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OcrParticipantAliasDto>> Update(
        Guid id, UpdateOcrParticipantAliasRequest request, CancellationToken ct)
        => Ok(await _aliases.UpdateAsync(id, request, User.GetUserId(), ct));

    /// <summary>Forgets an alias.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _aliases.DeleteAsync(id, User.GetUserId(), ct);
        return NoContent();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Services;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/thanaweya-results")]
public sealed class ThanaweyaResultsController : ControllerBase
{
    private readonly ThanaweyaResultsService _resultsService;

    public ThanaweyaResultsController(ThanaweyaResultsService resultsService) =>
        _resultsService = resultsService;

    [AllowAnonymous]
    [HttpGet("{seatingNumber}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Find(string seatingNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(seatingNumber) || seatingNumber.Length is < 4 or > 20 || seatingNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            return BadRequest(new { message = "رقم الجلوس غير صحيح." });
        }

        var result = await _resultsService.FindBySeatingNumberAsync(seatingNumber, ct);
        return result is null
            ? NotFound(new { message = "لم نعثر على نتيجة برقم الجلوس هذا." })
            : Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    public async Task<IActionResult> Import(CancellationToken ct) =>
        Ok(await _resultsService.ImportAsync(force: true, ct));
}

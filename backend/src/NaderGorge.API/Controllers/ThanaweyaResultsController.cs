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

    [AllowAnonymous]
    [HttpGet("{seatingNumber}/subjects")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> FindSubjects(string seatingNumber, [FromQuery] int system = 1, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(seatingNumber) || seatingNumber.Length is < 4 or > 20 || seatingNumber.Any(character => !char.IsAsciiDigit(character)))
        {
            return BadRequest(new { message = "رقم الجلوس غير صحيح." });
        }
        if (system is not 1 and not 2)
        {
            return BadRequest(new { message = "اختر نظامًا صحيحًا: حديث أو قديم." });
        }

        // Do not call the external publisher for a seating number our local result
        // database does not recognise.
        if (await _resultsService.FindBySeatingNumberAsync(seatingNumber, ct) is null)
        {
            return NotFound(new { message = "لم نعثر على نتيجة برقم الجلوس هذا." });
        }

        try
        {
            var result = await _resultsService.FindSubjectGradesAsync(seatingNumber, system, ct);
            return result is null
                ? NotFound(new { message = "الدرجات التفصيلية غير متاحة لهذا الرقم حاليًا." })
                : Ok(result);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "استغرق جلب الدرجات وقتًا أطول من المتوقع. حاول مرة أخرى." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "تعذر الوصول لمصدر الدرجات الآن. حاول مرة أخرى." });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("import")]
    public async Task<IActionResult> Import(CancellationToken ct) =>
        Ok(await _resultsService.ImportAsync(force: true, ct));
}

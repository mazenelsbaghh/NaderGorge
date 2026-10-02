using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;

namespace NaderGorge.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/teacher-finance-center/teachers/{teacherId:guid}/reports")]
public sealed class AdminTeacherReportsController(ITeacherDetailedReportService reports) : ControllerBase
{
    [HttpGet("detailed.pdf")]
    public async Task<IActionResult> Download(Guid teacherId, [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (!to.HasValue || from > to || to > CairoTime.GetCurrentDate())
            return BadRequest(new { success = false, message = "اختار فترة صحيحة لحد النهارده؛ البداية لازم تكون قبل النهاية." });
        var report = await reports.ExportAsync(teacherId, new(from, to.Value), ct);
        if (report is null) return NotFound(new { success = false, message = "المدرس غير موجود" });
        Response.Headers.CacheControl = "no-store";
        return File(report.Content, report.ContentType, report.FileName);
    }
}

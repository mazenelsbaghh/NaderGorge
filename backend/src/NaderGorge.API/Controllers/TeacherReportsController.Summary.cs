using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Reporting;

namespace NaderGorge.API.Controllers;

public sealed partial class TeacherReportsController
{
    [HttpPost("summary/export")]
    public async Task<IActionResult> ExportSummary([FromBody] StudentCourseSummaryFilter filter, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var teacherId = await _reports.GetReportScopeTeacherIdAsync(User.RequireUserId(), true, ct);
        try
        {
            var export = await _studentLedger.ExportCourseSummaryAsync(teacherId!.Value, User.RequireUserId(), filter, ct);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}

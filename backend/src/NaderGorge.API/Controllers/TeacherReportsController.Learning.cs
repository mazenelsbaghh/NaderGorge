using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.StudentFollowUp;

namespace NaderGorge.API.Controllers;

public sealed partial class TeacherReportsController
{
    [HttpGet("learning/options")]
    public async Task<IActionResult> LearningOptions([FromQuery] Guid? packageId,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var teacherId = await _reports.GetReportScopeTeacherIdAsync(User.RequireUserId(), true, ct);
        return Ok(ApiResponse<StudentFollowUpOptions>.Ok(await reader.OptionsAsync(teacherId, packageId, false, ct)));
    }

    [HttpPost("learning/students")]
    public async Task<IActionResult> LearningStudents([FromBody] FollowUpStudentSearch search,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var teacherId = await _reports.GetReportScopeTeacherIdAsync(User.RequireUserId(), true, ct);
        try { return Ok(ApiResponse<FollowUpStudentChoices>.Ok(await reader.SearchStudentsAsync(search with { TeacherId = teacherId!.Value }, ct))); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("learning")]
    public async Task<IActionResult> LearningReport([FromBody] StudentFollowUpFilter filter,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var teacherId = await _reports.GetReportScopeTeacherIdAsync(User.RequireUserId(), true, ct);
        try { return Ok(ApiResponse<StudentFollowUpReport>.Ok(await reader.ReadAsync(filter with { TeacherId = teacherId!.Value }, false, ct))); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("learning/export")]
    public async Task<IActionResult> ExportLearningReport([FromBody] StudentFollowUpFilter filter,
        [FromServices] IStudentFollowUpReader reader, [FromServices] IStudentFollowUpExporter exporter, CancellationToken ct)
    {
        if (!await CanAccessAsync(ct)) return Forbid();
        var teacherId = await _reports.GetReportScopeTeacherIdAsync(User.RequireUserId(), true, ct);
        try
        {
            filter = filter with { TeacherId = teacherId!.Value };
            var report = await reader.ReadAsync(filter, true, ct);
            var bytes = await exporter.ExportAsync(report, filter, User.RequireUserId(), ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "lesson-student-report.xlsx");
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}

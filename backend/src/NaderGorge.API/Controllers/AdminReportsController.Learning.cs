using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.StudentFollowUp;

namespace NaderGorge.API.Controllers;

public partial class AdminReportsController
{
    [HttpGet("learning/options")]
    public async Task<IActionResult> LearningOptions([FromQuery] Guid? teacherId, [FromQuery] Guid? packageId,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct) =>
        Ok(ApiResponse<StudentFollowUpOptions>.Ok(await reader.OptionsAsync(teacherId, packageId, true, ct)));

    [HttpPost("learning/students")]
    public async Task<IActionResult> LearningStudents([FromBody] FollowUpStudentSearch search,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct)
    {
        try { return Ok(ApiResponse<FollowUpStudentChoices>.Ok(await reader.SearchStudentsAsync(search, ct))); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("learning")]
    public async Task<IActionResult> LearningReport([FromBody] StudentFollowUpFilter filter,
        [FromServices] IStudentFollowUpReader reader, CancellationToken ct)
    {
        try { return Ok(ApiResponse<StudentFollowUpReport>.Ok(await reader.ReadAsync(filter, false, ct))); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("learning/export")]
    public async Task<IActionResult> ExportLearningReport([FromBody] StudentFollowUpFilter filter,
        [FromServices] IStudentFollowUpReader reader, [FromServices] IStudentFollowUpExporter exporter, CancellationToken ct)
    {
        try
        {
            var report = await reader.ReadAsync(filter, true, ct);
            var bytes = await exporter.ExportAsync(report, filter, User.RequireUserId(), ct);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "lesson-student-report.xlsx");
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.StudentFollowUp;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/admin/student-follow-up")]
[Authorize(Roles = "Admin,Supervisor,Staff")]
[HasPermission("users.manage")]
public sealed class AdminStudentFollowUpController(IStudentFollowUpReader reader, IStudentFollowUpExporter exporter) : ControllerBase
{
    [HttpGet("options")]
    public async Task<IActionResult> Options([FromQuery] Guid? teacherId, [FromQuery] Guid? packageId, CancellationToken ct) =>
        Ok(ApiResponse<StudentFollowUpOptions>.Ok(await reader.OptionsAsync(teacherId, packageId, true, ct)));

    [HttpPost]
    public async Task<IActionResult> Read([FromBody] StudentFollowUpFilter filter, CancellationToken ct)
    {
        try { return Ok(ApiResponse<StudentFollowUpReport>.Ok(await reader.ReadAsync(filter, false, ct))); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] StudentFollowUpFilter filter, CancellationToken ct)
    {
        try
        {
            var report = await reader.ReadAsync(filter, true, ct);
            var content = await exporter.ExportAsync(report, filter, User.RequireUserId(), ct);
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "student-follow-up.xlsx");
        }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
}

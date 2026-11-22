using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.CenterDesktop;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/center-students")]
[Authorize]
[HasPermission("center_students.view")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[HttpLogging(HttpLoggingFields.None)]
public sealed class CenterStudentsController(CenterDesktopSupportClient support) : ControllerBase
{
    [HttpGet("sources")]
    public Task<IActionResult> Sources(CancellationToken cancellationToken) =>
        Read(() => support.StudentSourcesAsync(cancellationToken));

    [HttpGet("{id}/students")]
    public Task<IActionResult> Students(string id, [FromQuery] string? q = null,
        [FromQuery] string? studentId = null, [FromQuery] string? groupId = null,
        [FromQuery] string? centerId = null, [FromQuery] string? gradeId = null,
        CancellationToken cancellationToken = default) => Read(async () =>
        {
            var sources = await support.StudentSourcesAsync(cancellationToken);
            if (!Guid.TryParse(id, out var sourceId) || sources.All(source => source.UploadId != sourceId))
                throw new DesktopSupportException(404, "مصدر بيانات الطلاب غير متاح. حدّث بيانات الطلاب.");
            return await support.StudentsAsync(id,
                new DesktopStudentQuery(q, studentId, groupId, centerId, gradeId), cancellationToken);
        });

    private async Task<IActionResult> Read<T>(Func<Task<T>> action)
    {
        try { return Ok(ApiResponse<T>.Ok(await action())); }
        catch (DesktopSupportException error) { return StatusCode(error.StatusCode, ApiResponse.Fail(error.Message)); }
    }
}

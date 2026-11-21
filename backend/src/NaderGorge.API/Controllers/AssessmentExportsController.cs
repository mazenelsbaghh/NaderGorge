using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.API.Controllers;

[ApiController, Authorize]
public sealed class AssessmentExportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("api/admin/exams/{id:guid}/export"), HasPermission("exams.manage")]
    public Task<IActionResult> AdminExam(Guid id, [FromQuery] string selection = "grades", CancellationToken ct = default) =>
        Export(AssessmentKind.Exam, id, selection, ct);

    [HttpGet("api/admin/homework/{id:guid}/export"), HasPermission("content.manage")]
    public Task<IActionResult> AdminHomework(Guid id, [FromQuery] string selection = "grades", CancellationToken ct = default) =>
        Export(AssessmentKind.Homework, id, selection, ct);

    [HttpGet("api/teacher/exams/{id:guid}/export"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> TeacherExam(Guid id, [FromQuery] string selection = "grades", CancellationToken ct = default) =>
        Export(AssessmentKind.Exam, id, selection, ct);

    [HttpGet("api/teacher/homework/{id:guid}/export"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> TeacherHomework(Guid id, [FromQuery] string selection = "grades", CancellationToken ct = default) =>
        Export(AssessmentKind.Homework, id, selection, ct);

    private async Task<IActionResult> Export(AssessmentKind kind, Guid id, string selection, CancellationToken ct)
    {
        if (selection is not ("grades" or "absent")) return BadRequest(new { message = "اختر كشف الدرجات أو الغياب." });
        var target = new AssessmentTarget(kind, id, Guid.Empty, User.RequireUserId());
        var response = await mediator.Send(new GetAssessmentExportQuery(target,
            selection == "grades" ? AssessmentExportSelection.Grades : AssessmentExportSelection.Absent), ct);
        if (response.Errors?.Contains("ASSESSMENT_EXPORT_FORBIDDEN") == true) return Forbid();
        if (response.Errors?.Contains("ASSESSMENT_EXPORT_NOT_FOUND") == true) return NotFound(response);
        if (!response.Success) return BadRequest(response);
        var bytes = AssessmentExportWorkbook.Build(response.Data!, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{kind.ToString().ToLowerInvariant()}-{id}-{selection}.xlsx");
    }
}

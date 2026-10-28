using Microsoft.AspNetCore.Mvc;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;

namespace NaderGorge.API.Controllers;

public partial class TeacherController
{
    [HttpGet("students/{studentId:guid}/profile")]
    public async Task<IActionResult> StudentProfile(Guid studentId, CancellationToken ct)
    {
        var context = await ResolveTeacherContextAsync(ct);
        if (MissingPermission(context, TeacherStaffPermissions.Students)) return Forbid();
        var canViewFinance = Can(context!, TeacherStaffPermissions.Finance);
        try
        {
            var profile = await _mediator.Send(new GetStudentProfileDetailQuery(studentId)
            { TeacherId = context!.TeacherId, IncludeTeacherFinance = canViewFinance }, ct);
            return Ok(ApiResponse<TeacherStudentProfileResponse>.Ok(new(profile, canViewFinance, Can(context!, TeacherStaffPermissions.Reports))));
        }
        catch (KeyNotFoundException) { return NotFound(new { message = "الطالب غير موجود ضمن طلابك." }); }
    }

    public sealed record TeacherStudentProfileResponse(StudentProfileExtendedDto Profile, bool CanViewFinance, bool CanViewReports);
}

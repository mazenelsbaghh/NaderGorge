using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public enum AssessmentExportSelection { Grades, Absent }
public sealed record AssessmentExportStudent(Guid Id, string Name, string Phone, string? Code,
    string? ParentPhone, string? MotherPhone);
public sealed record AssessmentExportRow(AssessmentExportStudent Student, Guid? AttemptId,
    decimal? Score, decimal? Maximum, string Status, string? Evaluation,
    DateTime? StartedAt, DateTime? SubmittedAt, bool IsFinal);
public sealed record AssessmentExportReport(Guid AssessmentId, AssessmentKind Kind, string Title,
    string InternalCode, AssessmentExportSelection Selection, IReadOnlyList<AssessmentExportRow> Rows,
    DateTime GeneratedAtUtc);
public sealed record GetAssessmentExportQuery(AssessmentTarget Target, AssessmentExportSelection Selection)
    : IRequest<ApiResponse<AssessmentExportReport>>;
internal sealed record AssessmentExportDefinition(Guid Id, string Title, string Code, Guid? LessonId);

public sealed class GetAssessmentExportQueryHandler(IAppDbContext db, TeacherAuthorizationService authorization,
    IAccessCheckService access) : IRequestHandler<GetAssessmentExportQuery, ApiResponse<AssessmentExportReport>>
{
    internal const int RowLimit = 50_000;

    public async Task<ApiResponse<AssessmentExportReport>> Handle(GetAssessmentExportQuery request, CancellationToken ct)
    {
        var target = request.Target;
        var actorAllowed = await db.Users.AnyAsync(u => u.Id == target.ActorId && u.IsActive &&
            u.UserRoles.Any(r => r.Role.Type != RoleType.Student), ct);
        if (!actorAllowed || !await AssessmentAccess.Allowed(db, authorization, target, ct) ||
            !await authorization.CanAccessTeacherWorkspacePermissionAsync(target.ActorId, "students", ct))
            return ApiResponse<AssessmentExportReport>.Fail("غير مصرح بتنزيل بيانات هذا الامتحان أو الواجب.", ["ASSESSMENT_EXPORT_FORBIDDEN"]);
        var definition = await DefinitionAsync(target, ct);
        if (definition is null)
            return ApiResponse<AssessmentExportReport>.Fail("الامتحان أو الواجب غير موجود.", ["ASSESSMENT_EXPORT_NOT_FOUND"]);
        try
        {
            var attempts = await new AssessmentExportAttempts(db).ReadAsync(target, ct);
            var exportRows = request.Selection == AssessmentExportSelection.Grades ? attempts
                : await new AssessmentExportRoster(db, access).AbsentAsync(target, definition, attempts, ct);
            return ApiResponse<AssessmentExportReport>.Ok(new(definition.Id, target.Kind, definition.Title,
                definition.Code, request.Selection, exportRows, DateTime.UtcNow));
        }
        catch (ArgumentException error)
        {
            return ApiResponse<AssessmentExportReport>.Fail(error.Message);
        }
    }

    private Task<AssessmentExportDefinition?> DefinitionAsync(AssessmentTarget target, CancellationToken ct) =>
        target.Kind == AssessmentKind.Exam
            ? db.Exams.AsNoTracking().Where(e => e.Id == target.AssessmentId)
                .Select(e => new AssessmentExportDefinition(e.Id, e.Title, e.InternalCode, null)).SingleOrDefaultAsync(ct)
            : db.Homeworks.AsNoTracking().Where(h => h.Id == target.AssessmentId)
                .Select(h => new AssessmentExportDefinition(h.Id, h.Title, h.Id.ToString(), h.LessonId)).SingleOrDefaultAsync(ct);
}

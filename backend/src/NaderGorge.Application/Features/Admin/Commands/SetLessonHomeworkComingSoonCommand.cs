using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Homework;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record SetLessonHomeworkComingSoonCommand(
    Guid LessonId,
    DateOnly? ExpectedOn,
    Guid? CurrentUserId = null) : IRequest<ApiResponse>;

public sealed class SetLessonHomeworkComingSoonCommandHandler
    : IRequestHandler<SetLessonHomeworkComingSoonCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _authorization;

    public SetLessonHomeworkComingSoonCommandHandler(
        IAppDbContext db,
        TeacherAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }

    public async Task<ApiResponse> Handle(
        SetLessonHomeworkComingSoonCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CurrentUserId.HasValue &&
            !await _authorization.CanAccessLessonAsync(
                request.CurrentUserId.Value,
                request.LessonId,
                cancellationToken))
        {
            return ApiResponse.Fail("Unauthorized access to this lesson.");
        }

        var lesson = await _db.Lessons
            .FirstOrDefaultAsync(item => item.Id == request.LessonId, cancellationToken);
        if (lesson is null)
            return ApiResponse.Fail("Lesson not found");

        if (request.ExpectedOn.HasValue && request.ExpectedOn.Value < CairoTime.GetCurrentDate())
        {
            return ApiResponse.Fail(
                "اختر اليوم أو تاريخًا قادمًا لظهور إعلان الواجب.",
                ["HOMEWORK_COMING_SOON_DATE_PAST"]);
        }

        if (request.ExpectedOn.HasValue && await _db.Homeworks
                .ReadyForStudents()
                .AnyAsync(item => item.LessonId == request.LessonId, cancellationToken))
        {
            return ApiResponse.Fail(
                "الواجب منشور بالفعل، لذلك لا يمكن إظهار إعلان أنه سيظهر لاحقًا.",
                ["HOMEWORK_ALREADY_AVAILABLE"]);
        }

        lesson.HomeworkComingSoonOn = request.ExpectedOn;
        await _db.SaveChangesAsync(cancellationToken);
        return ApiResponse.Ok();
    }
}

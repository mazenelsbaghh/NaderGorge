using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Features.Exams.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record GetStaffExamAttemptQuery(Guid ExamId, Guid AttemptId, Guid ActorId) : IRequest<ApiResponse<ExamResultDto>>;

public class GetStaffExamAttemptQueryHandler(IAppDbContext db, IMediator mediator, TeacherAuthorizationService authorization)
    : IRequestHandler<GetStaffExamAttemptQuery, ApiResponse<ExamResultDto>>
{
    public async Task<ApiResponse<ExamResultDto>> Handle(GetStaffExamAttemptQuery request, CancellationToken ct)
    {
        if (!await authorization.CanAccessExamAsync(request.ActorId, request.ExamId, ct))
            return ApiResponse<ExamResultDto>.Fail("غير مصرح بعرض هذه المحاولة.");
        var studentId = await db.StudentExamAttempts.AsNoTracking()
            .Where(a => a.Id == request.AttemptId && a.ExamId == request.ExamId)
            .Select(a => (Guid?)a.UserId).SingleOrDefaultAsync(ct);
        if (studentId is null) return ApiResponse<ExamResultDto>.Fail("المحاولة غير موجودة.");
        return await mediator.Send(new GetExamAttemptResultQuery(request.AttemptId, studentId.Value), ct);
    }
}

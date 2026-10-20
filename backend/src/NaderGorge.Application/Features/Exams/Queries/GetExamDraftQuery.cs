using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

public sealed record GetExamDraftQuery(ExamDraftScope Scope) : IRequest<ApiResponse<ExamDraftDto>>;

public sealed class GetExamDraftQueryHandler(IAppDbContext db) : IRequestHandler<GetExamDraftQuery, ApiResponse<ExamDraftDto>>
{
    public async Task<ApiResponse<ExamDraftDto>> Handle(GetExamDraftQuery request, CancellationToken ct)
    {
        var state = await ExamDraftState.Read(db, request.Scope, ct);
        if (state is null) return ApiResponse<ExamDraftDto>.Fail("المحاولة غير موجودة.", ["NOT_FOUND"]);
        if (state.RevisionId != (request.Scope.RevisionId ?? Guid.Empty))
            return ApiResponse<ExamDraftDto>.Fail("تغيّرت أسئلة المحاولة؛ أعد فتح الامتحان.", ["EXAM_DRAFT_REVISION_CHANGED"]);
        var draft = await db.StudentExamDrafts.AsNoTracking().SingleOrDefaultAsync(d =>
            d.AttemptId == request.Scope.AttemptId && d.RevisionId == state.RevisionId, ct);
        return ApiResponse<ExamDraftDto>.Ok(state.View(draft));
    }
}

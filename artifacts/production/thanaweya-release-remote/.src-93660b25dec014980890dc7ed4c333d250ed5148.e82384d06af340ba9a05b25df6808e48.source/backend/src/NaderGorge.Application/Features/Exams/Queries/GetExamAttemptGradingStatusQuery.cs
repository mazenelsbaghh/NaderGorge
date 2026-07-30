using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

public record EssayGradingStatusItemDto(
    Guid EssaySubmissionId,
    Guid QuestionId,
    string Status,
    decimal? AiInitialScore,
    decimal? TeacherFinalScore
);

public record ExamAttemptGradingStatusDto(
    Guid AttemptId,
    string ResultState,
    List<EssayGradingStatusItemDto> Essays
);

public record GetExamAttemptGradingStatusQuery(Guid AttemptId, Guid UserId)
    : IRequest<ApiResponse<ExamAttemptGradingStatusDto>>;

public class GetExamAttemptGradingStatusQueryHandler
    : IRequestHandler<GetExamAttemptGradingStatusQuery, ApiResponse<ExamAttemptGradingStatusDto>>
{
    private readonly IAppDbContext _db;

    public GetExamAttemptGradingStatusQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<ExamAttemptGradingStatusDto>> Handle(GetExamAttemptGradingStatusQuery request, CancellationToken ct)
    {
        var attempt = await _db.StudentExamAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId, ct);

        if (attempt == null)
        {
            return ApiResponse<ExamAttemptGradingStatusDto>.Fail("Attempt not found", new List<string> { "NOT_FOUND" });
        }

        var essays = await _db.EssaySubmissions
            .AsNoTracking()
            .Where(e => e.StudentExamAttemptId == request.AttemptId)
            .OrderBy(e => e.CreatedAt)
            .Select(e => new EssayGradingStatusItemDto(
                e.Id,
                e.QuestionId,
                e.Status.ToString(),
                e.AiInitialScore,
                e.TeacherFinalScore
            ))
            .ToListAsync(ct);

        var resultState = "Completed";
        if (essays.Any(e => e.Status == EssaySubmissionStatus.WaitAI.ToString()))
        {
            resultState = "Pending";
        }
        else if (essays.Any(e => e.Status != EssaySubmissionStatus.TeacherGraded.ToString()))
        {
            resultState = "PartiallyGraded";
        }

        return ApiResponse<ExamAttemptGradingStatusDto>.Ok(
            new ExamAttemptGradingStatusDto(request.AttemptId, resultState, essays));
    }
}

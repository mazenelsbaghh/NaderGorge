using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Queries;

/// <summary>
/// Returns the student's most recent submitted attempt, including one still being
/// evaluated by the AI. This lets a student safely leave and resume the result screen.
/// </summary>
public record GetLatestExamAttemptResultQuery(Guid ExamId, Guid UserId) : IRequest<ApiResponse<ExamResultDto>>;

public class GetLatestExamAttemptResultQueryHandler
    : IRequestHandler<GetLatestExamAttemptResultQuery, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public GetLatestExamAttemptResultQueryHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(GetLatestExamAttemptResultQuery request, CancellationToken ct)
    {
        if (!await _access.HasAccessToExamAsync(request.UserId, request.ExamId, ct))
        {
            return ApiResponse<ExamResultDto>.Fail("You do not have access to this exam.");
        }

        var attemptId = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.UserId == request.UserId
                && attempt.ExamId == request.ExamId
                && (attempt.Evaluation != null
                    || _db.EssaySubmissions.Any(essay => essay.StudentExamAttemptId == attempt.Id)
                    || _db.StudentAnswers.Any(answer => answer.StudentExamAttemptId == attempt.Id
                        && (answer.SelectedOptionId != null || !string.IsNullOrWhiteSpace(answer.SubmittedText)))))
            .OrderByDescending(attempt => attempt.UpdatedAt ?? attempt.CreatedAt)
            .Select(attempt => (Guid?)attempt.Id)
            .FirstOrDefaultAsync(ct);

        if (!attemptId.HasValue)
        {
            return ApiResponse<ExamResultDto>.Fail("No submitted attempt found", ["NOT_FOUND"]);
        }

        return await new GetExamAttemptResultQueryHandler(_db)
            .Handle(new GetExamAttemptResultQuery(attemptId.Value, request.UserId), ct);
    }
}

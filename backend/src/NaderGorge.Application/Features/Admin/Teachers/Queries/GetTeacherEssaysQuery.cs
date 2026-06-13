using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Teachers.Queries;

public record GetTeacherEssaysQuery(
    Guid TeacherId,
    int Page = 1,
    int PageSize = 20
) : IRequest<ApiResponse<TeacherEssaysPagedResult>>;

public record TeacherEssayDto(
    Guid Id,
    string StudentName,
    string? ExamTitle,
    decimal? AiScore,
    decimal? TeacherScore,
    string? TeacherFeedback,
    string Status,
    DateTime CreatedAt,
    DateTime? GradedAt
);

public record TeacherEssaysPagedResult(
    List<TeacherEssayDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public class GetTeacherEssaysQueryHandler : IRequestHandler<GetTeacherEssaysQuery, ApiResponse<TeacherEssaysPagedResult>>
{
    private readonly IAppDbContext _db;

    public GetTeacherEssaysQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<TeacherEssaysPagedResult>> Handle(GetTeacherEssaysQuery request, CancellationToken ct)
    {
        // Essays graded by this teacher OR essays belonging to exams created by this teacher
        var query = _db.EssaySubmissions
            .Where(e => e.GradedByTeacherId == request.TeacherId
                || e.Question.CreatedByTeacherId == request.TeacherId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new TeacherEssayDto(
                e.Id,
                e.Student.FullName,
                e.Attempt.Exam.Title,
                e.AiInitialScore,
                e.TeacherFinalScore,
                e.TeacherFeedback,
                e.Status.ToString(),
                e.CreatedAt,
                e.Status == EssaySubmissionStatus.TeacherGraded ? e.UpdatedAt : null
            ))
            .ToListAsync(ct);

        return ApiResponse<TeacherEssaysPagedResult>.Ok(
            new TeacherEssaysPagedResult(items, totalCount, request.Page, request.PageSize));
    }
}

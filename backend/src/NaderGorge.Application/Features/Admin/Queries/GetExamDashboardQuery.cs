using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record StudentExamResultSummaryDto(
    Guid StudentId,
    string StudentName,
    string StudentPhone, // Added for better tracking
    DateTime? StartedAt, // Added to show exactly when they started
    DateTime? SubmittedAt,
    decimal ScoreAchieved,
    string Evaluation,
    bool IsPassed,
    bool IsTimeExpired
);

public record ExamDashboardDto(
    Guid ExamId,
    string Title,
    string Description,
    int QuestionCount,
    decimal TotalScore,
    decimal PassingScore,
    int? DurationMinutes,
    int? TimePerQuestionSeconds,
    List<StudentExamResultSummaryDto> Attempts
);

public record GetExamDashboardQuery(Guid ExamId) : IRequest<ApiResponse<ExamDashboardDto>>;

public class GetExamDashboardQueryHandler : IRequestHandler<GetExamDashboardQuery, ApiResponse<ExamDashboardDto>>
{
    private readonly IAppDbContext _context;

    public GetExamDashboardQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ExamDashboardDto>> Handle(GetExamDashboardQuery request, CancellationToken cancellationToken)
    {
        var exam = await _context.Exams
            .Include(e => e.ExamQuestions)
            .Include(e => e.Attempts)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam == null)
            return ApiResponse<ExamDashboardDto>.Fail("Exam not found");

        var attemptsDto = exam.Attempts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new StudentExamResultSummaryDto(
                a.UserId,
                a.User.FullName,
                a.User.PhoneNumber,
                a.StartedAt,
                a.CreatedAt, // usually when attempt is inserted/finished, or a specific submitted time
                a.ScoreAchieved,
                a.Evaluation ?? "لم يقيّم",
                a.IsPassed,
                a.IsTimeExpired
            )).ToList();

        var dto = new ExamDashboardDto(
            exam.Id,
            exam.Title,
            exam.Description,
            exam.ExamQuestions.Count,
            exam.TotalScore,
            exam.PassingScore,
            exam.DurationMinutes,
            exam.TimePerQuestionSeconds,
            attemptsDto
        );

        return ApiResponse<ExamDashboardDto>.Ok(dto);
    }
}

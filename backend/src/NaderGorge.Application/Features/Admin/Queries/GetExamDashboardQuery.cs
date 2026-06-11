using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
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

public record ExamQuestionSummaryDto(
    Guid ExamQuestionId,
    string Text,
    string Type,
    decimal Points,
    string? BaseText
);

public record ExamDashboardDto(
    Guid ExamId,
    string Title,
    string Description,
    int QuestionCount,
    decimal TotalScore,
    decimal PassingScore,
    int? DurationMinutes,
    List<StudentExamResultSummaryDto> Attempts,
    List<ExamQuestionSummaryDto> Questions
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
                .ThenInclude(eq => eq.Question)
            .Include(e => e.Attempts)
                .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam == null)
            return ApiResponse<ExamDashboardDto>.Fail("Exam not found");

        var attemptsDto = exam.Attempts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a =>
            {
                var isExpired = a.IsTimeExpired;
                var eval = a.Evaluation ?? "لم يقيّم";
                var score = a.ScoreAchieved;
                var isPassed = a.IsPassed;

                if (a.Evaluation == null && exam.DurationMinutes.HasValue && a.StartedAt.HasValue)
                {
                    var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60));
                    var timeTaken = DateTime.UtcNow - a.StartedAt.Value;
                    if (timeTaken > timeAllowed)
                    {
                        isExpired = true;
                        eval = "انتهى الوقت";
                        score = 0;
                        isPassed = false;
                    }
                }

                return new StudentExamResultSummaryDto(
                    a.UserId,
                    a.User?.FullName ?? "طالب محذوف",
                    a.User?.PhoneNumber ?? "غير متوفر",
                    a.StartedAt,
                    a.CreatedAt,
                    score,
                    eval,
                    isPassed,
                    isExpired
                );
            }).ToList();

        var questionsDto = exam.ExamQuestions
            .OrderBy(eq => eq.Order)
            .Select(eq => new ExamQuestionSummaryDto(
                eq.Id,
                eq.Question?.Text ?? "سؤال محذوف",
                eq.Question?.Type.ToString() ?? "Essay",
                eq.Points,
                eq.Question is FindTheMistakeQuestion ftm ? ftm.BaseText : null
            )).ToList();

        var dto = new ExamDashboardDto(
            exam.Id,
            exam.Title,
            exam.Description,
            exam.ExamQuestions.Count,
            exam.TotalScore,
            exam.PassingScore,
            exam.DurationMinutes,
            attemptsDto,
            questionsDto
        );

        return ApiResponse<ExamDashboardDto>.Ok(dto);
    }
}

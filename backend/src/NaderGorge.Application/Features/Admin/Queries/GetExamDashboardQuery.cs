using MediatR;
using NaderGorge.Application.Features.Assessments;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record StudentExamResultSummaryDto(
    Guid AttemptId,
    Guid StudentId,
    string StudentName,
    string StudentPhone, // Added for better tracking
    DateTime? StartedAt, // Added to show exactly when they started
    DateTime? SubmittedAt,
    decimal ScoreAchieved,
    string Evaluation,
    bool IsPassed,
    bool IsTimeExpired,
    decimal TotalScore
);

public record ExamQuestionOptionDto(
    Guid Id,
    string Text,
    bool IsCorrect
);

public record ExamQuestionSummaryDto(
    Guid ExamQuestionId,
    Guid QuestionBankItemId,
    string Text,
    string Type,
    decimal Points,
    string? BaseText,
    int TotalAttempts,
    int CorrectCount,
    int WrongCount,
    decimal CorrectPercentage,
    string? AudioUrl = null,
    string? ImageUrl = null,
    string? WrittenCorrection = null,
    string? HintText = null,
    int? MistakeStartIndex = null,
    int? MistakeEndIndex = null,
    List<ExamQuestionOptionDto>? Options = null
);

public record ExamDashboardDto(
    Guid ExamId,
    string InternalCode,
    string Title,
    string Description,
    int QuestionCount,
    decimal TotalScore,
    decimal PassingScore,
    int? DurationMinutes,
    bool IsActive,
    List<StudentExamResultSummaryDto> Attempts,
    List<ExamQuestionSummaryDto> Questions,
    ContentArchiveMode ArchiveMode,
    DateTime? ArchivedAt
);

public record GetExamDashboardQuery(Guid ExamId, Guid ActorId) : IRequest<ApiResponse<ExamDashboardDto>>;

public class GetExamDashboardQueryHandler : IRequestHandler<GetExamDashboardQuery, ApiResponse<ExamDashboardDto>>
{
    private readonly IAppDbContext _context;

    public GetExamDashboardQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<ExamDashboardDto>> Handle(GetExamDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!await new NaderGorge.Application.Services.TeacherAuthorizationService(_context)
            .CanAccessExamAsync(request.ActorId, request.ExamId, cancellationToken))
            return ApiResponse<ExamDashboardDto>.Fail("غير مصرح بعرض هذا الامتحان.");
        var exam = await _context.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions.Where(q => !q.IsRetired))
                .ThenInclude(eq => eq.Question)
                    .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, cancellationToken);

        if (exam == null)
            return ApiResponse<ExamDashboardDto>.Fail("Exam not found");

        var attempts = await _context.StudentExamAttempts
            .AsNoTracking()
            .Where(attempt => attempt.ExamId == request.ExamId)
            .Select(attempt => new
            {
                attempt.Id,
                attempt.UserId,
                StudentName = attempt.User.FullName,
                StudentPhone = attempt.User.PhoneNumber,
                attempt.StartedAt,
                attempt.CreatedAt,
                attempt.ScoreAchieved,
                attempt.Evaluation,
                attempt.IsPassed,
                attempt.IsTimeExpired,
                attempt.DefinitionSnapshotJson,
                AwardedPoints = attempt.Answers.Sum(answer => answer.PointsAwarded)
            })
            .ToListAsync(cancellationToken);
        var attemptsDto = attempts
            .OrderByDescending(a => a.CreatedAt)
            .Select(a =>
            {
                var isExpired = a.IsTimeExpired;
                var eval = a.Evaluation ?? "لم يقيّم";
                var score = a.ScoreAchieved;
                var isPassed = a.IsPassed;
                AssessmentAttemptScaleProjection? scale = null;
                if (a.DefinitionSnapshotJson is null)
                    eval = "يحتاج تسوية يدوية";
                else
                {
                    scale = AssessmentAttemptScaleNormalizer.Project(
                        a.DefinitionSnapshotJson, exam.Id, a.ScoreAchieved, a.IsPassed,
                        a.IsTimeExpired, a.AwardedPoints);
                    score = scale.ScoreAchieved;
                    isPassed = scale.IsPassed;
                }

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
                    a.Id,
                    a.UserId,
                    a.StudentName ?? "طالب محذوف",
                    a.StudentPhone ?? "غير متوفر",
                    a.StartedAt,
                    a.CreatedAt,
                    score,
                    eval,
                    isPassed,
                    isExpired,
                    scale?.Definition.TotalScore ?? 0
                );
            }).ToList();

        var answerCounts = await _context.StudentAnswers
            .Where(sa => sa.ExamQuestion.ExamId == request.ExamId)
            .GroupBy(answer => answer.ExamQuestionId)
            .Select(group => new
            {
                ExamQuestionId = group.Key,
                Total = group.Count(),
                Correct = group.Count(answer => answer.IsCorrect)
            })
            .ToDictionaryAsync(count => count.ExamQuestionId, cancellationToken);

        var questionsDto = exam.ExamQuestions
            .OrderBy(eq => eq.Order)
            .Select(eq =>
            {
                var counts = answerCounts.GetValueOrDefault(eq.Id);
                var total = counts?.Total ?? 0;
                var correct = counts?.Correct ?? 0;
                var wrong = total - correct;
                var pct = total > 0 ? Math.Round((decimal)correct / total * 100, 2) : 0m;

                return new ExamQuestionSummaryDto(
                    eq.Id,
                    eq.QuestionBankItemId,
                    eq.Question?.Text ?? "سؤال محذوف",
                    eq.Question?.Type.ToString() ?? "Essay",
                    eq.Points,
                    eq.Question is FindTheMistakeQuestion ftm ? ftm.BaseText : null,
                    total,
                    correct,
                    wrong,
                    pct,
                    eq.Question?.AudioUrl,
                    eq.Question?.ImageUrl,
                    eq.Question?.WrittenCorrection,
                    eq.Question?.HintText,
                    eq.Question is FindTheMistakeQuestion ftm2 ? ftm2.MistakeStartIndex : null,
                    eq.Question is FindTheMistakeQuestion ftm3 ? ftm3.MistakeEndIndex : null,
                    eq.Question?.Options?.Select(o => new ExamQuestionOptionDto(o.Id, o.Text, o.IsCorrect)).ToList() ?? new List<ExamQuestionOptionDto>()
                );
            }).ToList();

        var dto = new ExamDashboardDto(
            exam.Id,
            exam.InternalCode,
            exam.Title,
            exam.Description,
            exam.ExamQuestions.Count,
            exam.TotalScore,
            exam.PassingScore,
            exam.DurationMinutes,
            exam.IsActive,
            attemptsDto,
            questionsDto,
            exam.ArchiveMode,
            exam.ArchivedAt
        );

        return ApiResponse<ExamDashboardDto>.Ok(dto);
    }
}

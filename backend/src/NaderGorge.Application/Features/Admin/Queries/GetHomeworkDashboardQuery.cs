using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Queries;

public record StudentHomeworkSubmissionSummaryDto(
    Guid StudentId,
    string StudentName,
    string StudentPhone,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    decimal ScoreAchieved,
    string Status,
    string Evaluation
);

public record HomeworkQuestionSummaryDto(
    Guid HomeworkQuestionId,
    string Text,
    string Type,
    int Points,
    string? BaseText,
    string[]? PossibleAnswers = null,
    string? CorrectAnswerKey = null,
    string? AudioUrl = null,
    string? ImageUrl = null,
    string? WrittenCorrection = null,
    string? HintText = null,
    int? MistakeStartIndex = null,
    int? MistakeEndIndex = null
);

public record HomeworkDashboardDto(
    Guid HomeworkId,
    Guid LessonId,
    string Title,
    string? Description,
    int QuestionCount,
    decimal TotalScore,
    decimal? PassingScore,
    bool IsMandatory,
    bool IsRandomized,
    List<StudentHomeworkSubmissionSummaryDto> Submissions,
    List<HomeworkQuestionSummaryDto> Questions
);

public record GetHomeworkDashboardQuery(Guid HomeworkId) : IRequest<ApiResponse<HomeworkDashboardDto>>;

public class GetHomeworkDashboardQueryHandler : IRequestHandler<GetHomeworkDashboardQuery, ApiResponse<HomeworkDashboardDto>>
{
    private readonly IAppDbContext _context;

    public GetHomeworkDashboardQueryHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<HomeworkDashboardDto>> Handle(GetHomeworkDashboardQuery request, CancellationToken cancellationToken)
    {
        var homework = await _context.Homeworks
            .Include(h => h.Questions)
            .Include(h => h.Submissions)
                .ThenInclude(s => s.Student)
            .FirstOrDefaultAsync(h => h.Id == request.HomeworkId, cancellationToken);

        if (homework == null)
            return ApiResponse<HomeworkDashboardDto>.Fail("Homework not found");

        var submissionsDto = homework.Submissions
            .OrderByDescending(s => s.SubmittedAt ?? s.StartedAt)
            .Select(s => new StudentHomeworkSubmissionSummaryDto(
                s.StudentId,
                s.Student?.FullName ?? "طالب محذوف",
                s.Student?.PhoneNumber ?? "غير متوفر",
                s.StartedAt,
                s.SubmittedAt,
                s.OverallScore,
                s.Status.ToString(),
                s.Evaluation ?? "لم يقيّم"
            )).ToList();

        var questionsDto = homework.Questions
            .OrderBy(q => q.Order)
            .Select(q => new HomeworkQuestionSummaryDto(
                q.Id,
                q.BodyText,
                q.QuestionType.ToString(),
                q.PointsActive,
                q.BaseText,
                q.PossibleAnswers,
                q.CorrectAnswerKey,
                q.AudioUrl,
                q.ImageUrl,
                q.WrittenCorrection,
                q.HintText,
                q.MistakeStartIndex,
                q.MistakeEndIndex
            )).ToList();

        var dto = new HomeworkDashboardDto(
            homework.Id,
            homework.LessonId,
            homework.Title,
            homework.Description,
            homework.Questions.Count,
            homework.TotalScore,
            homework.PassingScoreThreshold,
            homework.IsMandatory,
            homework.IsRandomized,
            submissionsDto,
            questionsDto
        );

        return ApiResponse<HomeworkDashboardDto>.Ok(dto);
    }
}

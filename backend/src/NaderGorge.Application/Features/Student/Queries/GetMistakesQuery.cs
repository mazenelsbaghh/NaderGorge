using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetMistakesQuery(Guid UserId) : IRequest<ApiResponse<StudentMistakesDto>>;

public record StudentMistakesDto(
    int TotalExamMistakes,
    int ExamsWithMistakes,
    int WeakHomeworkCount,
    List<ExamMistakeGroupDto> ExamMistakes,
    List<HomeworkWeaknessDto> HomeworkWeaknesses
);

public record ExamMistakeGroupDto(
    Guid ExamId,
    string ExamTitle,
    Guid? PackageId,
    string PackageName,
    Guid? LessonId,
    string LessonTitle,
    bool PassedEventually,
    DateTime LastMistakeAt,
    int MistakesCount,
    decimal? LatestScore,
    decimal? LatestTotalScore,
    string? LatestEvaluation,
    List<ExamMistakeItemDto> Items
);

public record ExamMistakeItemDto(
    Guid ExamQuestionId,
    int QuestionOrder,
    string QuestionText,
    string? YourAnswer,
    string? CorrectAnswer,
    int TimesMissed,
    DateTime LastMissedAt,
    bool CanRevealCorrectAnswer
);

public record HomeworkWeaknessDto(
    Guid HomeworkId,
    string HomeworkTitle,
    Guid? PackageId,
    string PackageName,
    Guid LessonId,
    string LessonTitle,
    decimal Score,
    decimal? PassingScore,
    string Status,
    string? AssistantNotes
);

public class GetMistakesQueryHandler : IRequestHandler<GetMistakesQuery, ApiResponse<StudentMistakesDto>>
{
    private readonly IAppDbContext _db;

    public GetMistakesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentMistakesDto>> Handle(GetMistakesQuery request, CancellationToken ct)
    {
        var attempts = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId)
            .ToListAsync(ct);

        var examIds = attempts.Select(a => a.ExamId).Distinct().ToList();
        var attemptIds = attempts.Select(a => a.Id).ToList();

        var exams = await _db.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .Where(e => examIds.Contains(e.Id))
            .ToListAsync(ct);

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .ThenInclude(t => t.Package)
            .Where(l => l.ExamId.HasValue && examIds.Contains(l.ExamId.Value))
            .ToListAsync(ct);

        var answers = attemptIds.Count == 0
            ? new List<NaderGorge.Domain.Entities.StudentAnswer>()
            : await _db.StudentAnswers
                .AsNoTracking()
                .Where(a => attemptIds.Contains(a.StudentExamAttemptId))
                .ToListAsync(ct);

        var examById = exams.ToDictionary(e => e.Id);
        var lessonByExamId = lessons
            .Where(l => l.ExamId.HasValue)
            .GroupBy(l => l.ExamId!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        var attemptsById = attempts.ToDictionary(a => a.Id);
        var passedExamIds = attempts.Where(a => a.IsPassed).Select(a => a.ExamId).ToHashSet();

        var examMistakeGroups = answers
            .Where(a => !a.IsCorrect && attemptsById.ContainsKey(a.StudentExamAttemptId))
            .GroupBy(a => attemptsById[a.StudentExamAttemptId].ExamId)
            .Select(group =>
            {
                if (!examById.TryGetValue(group.Key, out var exam))
                    return null;

                lessonByExamId.TryGetValue(group.Key, out var lesson);
                var latestAttempt = attempts
                    .Where(a => a.ExamId == group.Key)
                    .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                    .FirstOrDefault();

                var items = group
                    .GroupBy(answer => answer.ExamQuestionId)
                    .Select(questionGroup =>
                    {
                        var examQuestion = exam.ExamQuestions.FirstOrDefault(q => q.Id == questionGroup.Key);
                        if (examQuestion == null)
                            return null;

                        var latestWrongAnswer = questionGroup
                            .OrderByDescending(answer => attemptsById[answer.StudentExamAttemptId].UpdatedAt ?? attemptsById[answer.StudentExamAttemptId].CreatedAt)
                            .First();

                        var selectedOption = examQuestion.Question.Options.FirstOrDefault(option => option.Id == latestWrongAnswer.SelectedOptionId);
                        var correctOption = examQuestion.Question.Options.FirstOrDefault(option => option.IsCorrect);
                        var lastMissedAt = attemptsById[latestWrongAnswer.StudentExamAttemptId].UpdatedAt
                            ?? attemptsById[latestWrongAnswer.StudentExamAttemptId].CreatedAt;

                        return new ExamMistakeItemDto(
                            examQuestion.Id,
                            examQuestion.Order,
                            examQuestion.Question.Text,
                            selectedOption?.Text,
                            passedExamIds.Contains(group.Key) ? correctOption?.Text : null,
                            questionGroup.Count(),
                            lastMissedAt,
                            passedExamIds.Contains(group.Key)
                        );
                    })
                    .Where(item => item != null)
                    .Cast<ExamMistakeItemDto>()
                    .OrderByDescending(item => item.LastMissedAt)
                    .ThenBy(item => item.QuestionOrder)
                    .ToList();

                if (items.Count == 0)
                    return null;

                return new ExamMistakeGroupDto(
                    group.Key,
                    exam.Title,
                    lesson?.ContentSection?.Term?.PackageId,
                    lesson?.ContentSection?.Term?.Package?.Name ?? "بدون باقة",
                    lesson?.Id,
                    lesson?.Title ?? "بدون درس",
                    passedExamIds.Contains(group.Key),
                    items.Max(item => item.LastMissedAt),
                    items.Sum(item => item.TimesMissed),
                    latestAttempt?.ScoreAchieved,
                    latestAttempt == null ? null : exam.TotalScore,
                    latestAttempt?.Evaluation,
                    items
                );
            })
            .Where(group => group != null)
            .Cast<ExamMistakeGroupDto>()
            .OrderByDescending(group => group.LastMistakeAt)
            .ToList();

        var homeworkSubmissions = await _db.HomeworkSubmissions
            .AsNoTracking()
            .Include(s => s.Homework)
            .Where(s => s.StudentId == request.UserId)
            .ToListAsync(ct);

        var homeworkLessonIds = homeworkSubmissions.Select(s => s.Homework.LessonId).Distinct().ToList();
        var homeworkLessons = homeworkLessonIds.Count == 0
            ? new List<NaderGorge.Domain.Entities.Lesson>()
            : await _db.Lessons
                .AsNoTracking()
                .Include(l => l.ContentSection)
                .ThenInclude(cs => cs.Term)
                .ThenInclude(t => t.Package)
                .Where(l => homeworkLessonIds.Contains(l.Id))
                .ToListAsync(ct);

        var homeworkLessonMap = homeworkLessons.ToDictionary(l => l.Id);

        var weakHomework = homeworkSubmissions
            .Where(s =>
                s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Missed ||
                (s.Homework.PassingScoreThreshold.HasValue &&
                 s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded &&
                 s.OverallScore < s.Homework.PassingScoreThreshold.Value))
            .Select(s =>
            {
                homeworkLessonMap.TryGetValue(s.Homework.LessonId, out var lesson);

                return new HomeworkWeaknessDto(
                    s.HomeworkId,
                    s.Homework.Title,
                    lesson?.ContentSection?.Term?.PackageId,
                    lesson?.ContentSection?.Term?.Package?.Name ?? "بدون باقة",
                    s.Homework.LessonId,
                    lesson?.Title ?? "بدون درس",
                    s.OverallScore,
                    s.Homework.PassingScoreThreshold,
                    s.Status.ToString(),
                    s.AssistantNotes
                );
            })
            .OrderByDescending(item => item.Status == "Missed")
            .ThenByDescending(item => item.Score)
            .ToList();

        return ApiResponse<StudentMistakesDto>.Ok(new StudentMistakesDto(
            examMistakeGroups.Sum(group => group.MistakesCount),
            examMistakeGroups.Count,
            weakHomework.Count,
            examMistakeGroups,
            weakHomework
        ));
    }
}

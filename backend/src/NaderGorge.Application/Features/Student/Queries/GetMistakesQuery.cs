using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetMistakesQuery(Guid UserId, int Skip = 0, int Take = 10) : IRequest<ApiResponse<StudentMistakesDto>>;

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
        // 1. Direct counts on DB
        var totalExamMistakes = await _db.StudentAnswers
            .AsNoTracking()
            .CountAsync(sa => sa.Attempt.UserId == request.UserId && !sa.IsCorrect, ct);

        var examsWithMistakes = await _db.StudentAnswers
            .AsNoTracking()
            .Where(sa => sa.Attempt.UserId == request.UserId && !sa.IsCorrect)
            .Select(sa => sa.Attempt.ExamId)
            .Distinct()
            .CountAsync(ct);

        var weakHomeworkCount = await _db.HomeworkSubmissions
            .AsNoTracking()
            .CountAsync(s => s.StudentId == request.UserId &&
                (s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Missed ||
                (s.Homework.PassingScoreThreshold.HasValue &&
                 s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded &&
                 s.OverallScore < s.Homework.PassingScoreThreshold.Value)), ct);

        // 2. Fetch only paged exam IDs that have mistakes
        var pagedExamIds = await _db.StudentAnswers
            .AsNoTracking()
            .Where(sa => sa.Attempt.UserId == request.UserId && !sa.IsCorrect)
            .Select(sa => sa.Attempt.ExamId)
            .Distinct()
            .OrderByDescending(id => id)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(ct);

        var attempts = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && pagedExamIds.Contains(a.ExamId))
            .ToListAsync(ct);

        var attemptIds = attempts.Select(a => a.Id).ToList();

        var exams = await _db.Exams
            .AsNoTracking()
            .Where(e => pagedExamIds.Contains(e.Id))
            .Select(e => new {
                e.Id,
                e.Title,
                e.TotalScore,
                Questions = e.ExamQuestions.Select(eq => new {
                    eq.Id,
                    eq.Order,
                    QuestionText = eq.Question.Text,
                    Options = eq.Question.Options.Select(o => new {
                        o.Id,
                        o.Text,
                        o.IsCorrect
                    }).ToList()
                }).ToList()
            })
            .ToListAsync(ct);

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Where(l => l.ExamId.HasValue && pagedExamIds.Contains(l.ExamId.Value))
            .Select(l => new {
                l.Id,
                l.Title,
                l.ExamId,
                PackageId = l.ContentSection.Term.PackageId,
                PackageName = l.ContentSection.Term.Package != null ? l.ContentSection.Term.Package.Name : "بدون باقة"
            })
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
        var passedExamIds = await _db.StudentExamAttempts
            .AsNoTracking()
            .Where(a => a.UserId == request.UserId && a.IsPassed && pagedExamIds.Contains(a.ExamId))
            .Select(a => a.ExamId)
            .Distinct()
            .ToListAsync(ct);
        var passedExamsSet = passedExamIds.ToHashSet();

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
                        var examQuestion = exam.Questions.FirstOrDefault(q => q.Id == questionGroup.Key);
                        if (examQuestion == null)
                            return null;

                        var latestWrongAnswer = questionGroup
                            .OrderByDescending(answer => attemptsById[answer.StudentExamAttemptId].UpdatedAt ?? attemptsById[answer.StudentExamAttemptId].CreatedAt)
                            .First();

                        var selectedOption = examQuestion.Options.FirstOrDefault(option => option.Id == latestWrongAnswer.SelectedOptionId);
                        var correctOption = examQuestion.Options.FirstOrDefault(option => option.IsCorrect);
                        var lastMissedAt = attemptsById[latestWrongAnswer.StudentExamAttemptId].UpdatedAt
                            ?? attemptsById[latestWrongAnswer.StudentExamAttemptId].CreatedAt;

                        return new ExamMistakeItemDto(
                            examQuestion.Id,
                            examQuestion.Order,
                            examQuestion.QuestionText,
                            selectedOption?.Text,
                            passedExamsSet.Contains(group.Key) ? correctOption?.Text : null,
                            questionGroup.Count(),
                            lastMissedAt,
                            passedExamsSet.Contains(group.Key)
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
                    lesson?.PackageId,
                    lesson?.PackageName ?? "بدون باقة",
                    lesson?.Id,
                    lesson?.Title ?? "بدون درس",
                    passedExamsSet.Contains(group.Key),
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

        // 3. Fetch homework weaknesses with pagination using projections
        var homeworkSubmissions = await _db.HomeworkSubmissions
            .AsNoTracking()
            .Where(s => s.StudentId == request.UserId)
            .Select(s => new {
                s.HomeworkId,
                HomeworkTitle = s.Homework.Title,
                HomeworkLessonId = s.Homework.LessonId,
                HomeworkPassingScoreThreshold = s.Homework.PassingScoreThreshold,
                s.OverallScore,
                s.Status,
                s.AssistantNotes
            })
            .ToListAsync(ct);

        var homeworkLessonIds = homeworkSubmissions.Select(s => s.HomeworkLessonId).Distinct().ToList();
        var homeworkLessons = homeworkLessonIds.Count == 0
            ? new Dictionary<Guid, (Guid? PackageId, string PackageName, string LessonTitle)>()
            : (await _db.Lessons
                .AsNoTracking()
                .Where(l => homeworkLessonIds.Contains(l.Id))
                .Select(l => new {
                    l.Id,
                    l.Title,
                    PackageId = l.ContentSection.Term.PackageId,
                    PackageName = l.ContentSection.Term.Package != null ? l.ContentSection.Term.Package.Name : "بدون باقة"
                })
                .ToListAsync(ct))
                .ToDictionary(l => l.Id, l => (PackageId: (Guid?)l.PackageId, PackageName: l.PackageName, LessonTitle: l.Title));

        var weakHomework = homeworkSubmissions
            .Where(s =>
                s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Missed ||
                (s.HomeworkPassingScoreThreshold.HasValue &&
                 s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded &&
                 s.OverallScore < s.HomeworkPassingScoreThreshold.Value))
            .Select(s =>
            {
                homeworkLessons.TryGetValue(s.HomeworkLessonId, out var lessonInfo);

                return new HomeworkWeaknessDto(
                    s.HomeworkId,
                    s.HomeworkTitle,
                    lessonInfo.PackageId,
                    lessonInfo.PackageName ?? "بدون باقة",
                    s.HomeworkLessonId,
                    lessonInfo.LessonTitle ?? "بدون درس",
                    s.OverallScore,
                    s.HomeworkPassingScoreThreshold,
                    s.Status.ToString(),
                    s.AssistantNotes
                );
            })
            .OrderByDescending(item => item.Status == "Missed")
            .ThenByDescending(item => item.Score)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToList();

        return ApiResponse<StudentMistakesDto>.Ok(new StudentMistakesDto(
            totalExamMistakes,
            examsWithMistakes,
            weakHomeworkCount,
            examMistakeGroups,
            weakHomework
        ));
    }
}

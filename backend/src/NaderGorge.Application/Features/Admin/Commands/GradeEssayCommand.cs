using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record GradeEssayCommand(Guid EssaySubmissionId, decimal TeacherScore, string? TeacherFeedback, Guid? CurrentUserId = null) : IRequest<ApiResponse<bool>>;

public class GradeEssayCommandHandler : IRequestHandler<GradeEssayCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly TeacherAuthorizationService _auth;

    public GradeEssayCommandHandler(IAppDbContext db, TeacherAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public async Task<ApiResponse<bool>> Handle(GradeEssayCommand request, CancellationToken ct)
        => await SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            _db.ClearTrackedChanges();
            await using var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, retryCt);
            var response = await GradeOnce(request, retryCt);
            if (response.Success) await transaction.CommitAsync(retryCt);
            return response;
        }, ct);

    private async Task<ApiResponse<bool>> GradeOnce(GradeEssayCommand request, CancellationToken ct)
    {
        if (request.CurrentUserId.HasValue)
        {
            var canAccess = await _auth.CanAccessEssaySubmissionAsync(request.CurrentUserId.Value, request.EssaySubmissionId, ct);
            if (!canAccess) return ApiResponse<bool>.Fail("Unauthorized access to grade this essay submission.");
        }

        var submission = await _db.EssaySubmissions.Include(s => s.Attempt)
            .FirstOrDefaultAsync(s => s.Id == request.EssaySubmissionId, ct);
        if (submission == null) return ApiResponse<bool>.Fail("Essay submission not found.");

        var attempt = submission.Attempt;
        var currentExam = await _db.Exams.Include(e => e.ExamQuestions).ThenInclude(q => q.Question)
            .FirstOrDefaultAsync(e => e.Id == attempt.ExamId, ct);
        if (currentExam is null) return ApiResponse<bool>.Fail("Exam not found.");
        var exam = AssessmentDefinitionSnapshot.ResolveExam(currentExam, attempt.DefinitionSnapshotJson);
        var question = exam.ExamQuestions.FirstOrDefault(q => q.QuestionBankItemId == submission.QuestionId);
        if (question == null || request.TeacherScore < 0 || request.TeacherScore > question.Points)
            return ApiResponse<bool>.Fail("الدرجة يجب أن تكون بين صفر ودرجة السؤال.");
        if (request.TeacherFeedback?.Length > 4000)
            return ApiResponse<bool>.Fail("ملاحظات التصحيح أطول من المسموح.");
        var previousScore = submission.TeacherFinalScore;
        var revisedDefinition = attempt.DefinitionSnapshotJson is null ? null
            : AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId)
                .WithGrades(new Dictionary<Guid, (decimal, bool)> { [question.Id] = (request.TeacherScore, true) });
        if (revisedDefinition is not null) attempt.DefinitionSnapshotJson = revisedDefinition.ToJson();

        Guid? teacherId = null;
        if (request.CurrentUserId.HasValue)
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.TeacherProfile)
                .FirstOrDefaultAsync(u => u.Id == request.CurrentUserId.Value, ct);

            if (user != null && user.UserRoles.Any(ur => ur.Role.Type == RoleType.Teacher))
            {
                if (user.TeacherProfile != null)
                {
                    teacherId = user.TeacherProfile.Id;
                }
            }
        }

        submission.TeacherFinalScore = request.TeacherScore;
        submission.TeacherFeedback = request.TeacherFeedback;
        submission.Status = EssaySubmissionStatus.TeacherGraded;
        submission.GradedByTeacherId = teacherId;

        var answer = await _db.StudentAnswers.FirstOrDefaultAsync(a =>
            a.StudentExamAttemptId == submission.StudentExamAttemptId && a.ExamQuestionId == question.Id, ct);
        if (answer != null)
        {
            answer.PointsAwarded = request.TeacherScore;
            answer.IsCorrect = request.TeacherScore >= question.Points;
        }
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "EssayManuallyGraded", EntityType = nameof(EssaySubmission), EntityId = submission.Id,
            PerformedByUserId = request.CurrentUserId,
            OldValues = System.Text.Json.JsonSerializer.Serialize(new { score = previousScore }),
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { score = request.TeacherScore })
        });

        var essayQuestionIds = exam.ExamQuestions.Where(q => q.Question.Type == QuestionType.Essay)
            .Select(q => q.Id).ToArray();
        var objectiveAnswers = await _db.StudentAnswers
            .Where(a => a.StudentExamAttemptId == attempt.Id && !essayQuestionIds.Contains(a.ExamQuestionId))
            .ToListAsync(ct);
        var essaySubmissions = await _db.EssaySubmissions
            .Where(e => e.StudentExamAttemptId == attempt.Id).ToListAsync(ct);
        var latestEssaySubmissions = essaySubmissions.GroupBy(e => e.QuestionId)
            .Select(g => g.OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt).First()).ToList();
        var rawPointsEarned = objectiveAnswers.Sum(a => a.PointsAwarded)
            + latestEssaySubmissions.Sum(e => e.Id == submission.Id ? request.TeacherScore : e.TeacherFinalScore ?? 0m);
        var assignedIds = await _db.StudentAnswers.Where(a => a.StudentExamAttemptId == attempt.Id)
            .Select(a => a.ExamQuestionId).ToListAsync(ct);
        var rawPointsPossible = exam.ExamQuestions.Where(q => assignedIds.Contains(q.Id)).Sum(q => q.Points);
        var allTeacherGraded = latestEssaySubmissions.Where(e => e.QuestionId != submission.QuestionId)
            .All(e => e.Status == EssaySubmissionStatus.TeacherGraded);
        allTeacherGraded = allTeacherGraded && revisedDefinition?.Revision?.RequiresCompletion != true
            && revisedDefinition?.Revision?.RequiresReview != true;
        if (allTeacherGraded)
        {
            var scaledScore = revisedDefinition?.Revision?.ScaledScore(exam.TotalScore)
                ?? GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);
            attempt.ScoreAchieved = scaledScore;
            attempt.IsPassed = !attempt.IsTimeExpired && scaledScore >= exam.PassingScore;
            attempt.Evaluation = GradingEvaluationService.DetermineEvaluation(scaledScore, exam.PassingScore, exam.TotalScore);
        }

        var homeworkGradedEvent = new OutboxEvent
        {
            Type = "HomeworkGraded",
            TargetUserId = submission.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                submissionId = submission.Id,
                studentId = submission.StudentId,
                examAttemptId = submission.StudentExamAttemptId,
                teacherScore = submission.TeacherFinalScore,
                feedback = submission.TeacherFeedback
            })
        };
        _db.OutboxEvents.Add(homeworkGradedEvent);

        if (allTeacherGraded)
        {
            var examGradedEvent = new OutboxEvent
            {
                Type = "ExamGraded",
                TargetUserId = submission.StudentId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    examId = attempt.ExamId,
                    attemptId = attempt.Id,
                    isPassed = attempt.IsPassed,
                    score = attempt.ScoreAchieved,
                    evaluation = attempt.Evaluation
                })
            };
            _db.OutboxEvents.Add(examGradedEvent);

            var examResultReadyEvent = new OutboxEvent
            {
                Type = "ExamResultReady",
                TargetUserId = submission.StudentId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    examId = attempt.ExamId,
                    attemptId = attempt.Id,
                    isPassed = attempt.IsPassed,
                    score = attempt.ScoreAchieved
                })
            };
            _db.OutboxEvents.Add(examResultReadyEvent);

            var lesson = await _db.Lessons
                .Include(l => l.ContentSection).ThenInclude(cs => cs.Term)
                .FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);

            if (lesson != null)
            {
                var nextLesson = await _db.Lessons
                    .Where(l => l.ContentSectionId == lesson.ContentSectionId && l.Order > lesson.Order)
                    .OrderBy(l => l.Order)
                    .FirstOrDefaultAsync(ct);

                if (nextLesson == null)
                {
                    var nextSection = await _db.ContentSections
                        .Where(s => s.TermId == lesson.ContentSection.TermId && s.Order > lesson.ContentSection.Order)
                        .OrderBy(s => s.Order)
                        .FirstOrDefaultAsync(ct);

                    if (nextSection != null)
                    {
                        nextLesson = await _db.Lessons
                            .Where(l => l.ContentSectionId == nextSection.Id)
                            .OrderBy(l => l.Order)
                            .FirstOrDefaultAsync(ct);
                    }
                }

                if (nextLesson != null)
                {
                    var nextLessonProgress = await _db.LessonProgresses
                        .FirstOrDefaultAsync(lp => lp.UserId == submission.StudentId && lp.LessonId == nextLesson.Id, ct);

                    bool nextIsLocked = !attempt.IsPassed && (nextLessonProgress == null || !nextLessonProgress.IsManuallyUnlocked);

                    var lockEvent = new OutboxEvent
                    {
                        Type = nextIsLocked ? "LessonLocked" : "LessonUnlocked",
                        TargetUserId = submission.StudentId.ToString(),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            lessonId = nextLesson.Id,
                            reason = nextIsLocked ? $"يجب اجتياز امتحان '{exam.Title}' بنجاح." : null
                        })
                    };
                    _db.OutboxEvents.Add(lockEvent);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record StartExamAttemptCommand(Guid ExamId, Guid UserId) : IRequest<ApiResponse<ActiveExamAttemptDto>>;

public record ActiveExamAttemptDto(
    Guid AttemptId,
    string Title,
    string Description,
    DateTime StartedAt,
    int? DurationMinutes,
    int? RemainingSeconds,
    decimal TotalScore,
    Guid? LessonId,
    Guid? PackageId,
    List<ExamQuestionViewDto> Questions
);

public record ExamQuestionViewDto(Guid Id, string Text, string Type, decimal Points, string? HintText, string? BaseText, int? MistakeStartIndex, int? MistakeEndIndex, List<QuestionOptionViewDto> Options);
public record QuestionOptionViewDto(Guid Id, string Text);

public class StartExamAttemptCommandHandler : IRequestHandler<StartExamAttemptCommand, ApiResponse<ActiveExamAttemptDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAccessCheckService _access;

    public StartExamAttemptCommandHandler(IAppDbContext db, IAccessCheckService access)
    {
        _db = db;
        _access = access;
    }

    public async Task<ApiResponse<ActiveExamAttemptDto>> Handle(StartExamAttemptCommand request, CancellationToken ct)
    {
        var hasAccess = await _access.HasAccessToExamAsync(request.UserId, request.ExamId, ct);
        if (!hasAccess)
        {
            return ApiResponse<ActiveExamAttemptDto>.Fail("You do not have access to this exam.");
        }

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.ExamId == request.ExamId, ct);

        if (lesson == null)
        {
            var video = await _db.LessonVideos.FirstOrDefaultAsync(v => v.ExamId == request.ExamId, ct);
            if (video != null)
            {
                lesson = await _db.Lessons
                    .Include(l => l.ContentSection)
                    .ThenInclude(cs => cs.Term)
                    .FirstOrDefaultAsync(l => l.Id == video.LessonId, ct);
            }
        }

        if (lesson != null)
        {
            var homework = await _db.Homeworks.FirstOrDefaultAsync(h => h.LessonId == lesson.Id, ct);
            if (homework != null && homework.IsMandatory)
            {
                var homeworkPassed = await _db.HomeworkSubmissions
                    .AnyAsync(s => s.StudentId == request.UserId 
                                && s.HomeworkId == homework.Id 
                                && s.Status == NaderGorge.Domain.Entities.Homework.SubmissionStatus.Graded 
                                && s.OverallScore >= (homework.PassingScoreThreshold ?? 0), ct);

                if (!homeworkPassed)
                {
                    return ApiResponse<ActiveExamAttemptDto>.Fail($"يجب اجتياز واجب '{homework.Title}' أولاً قبل بدء هذا الاختبار.");
                }
            }
        }

        var exam = await _db.Exams
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
            return ApiResponse<ActiveExamAttemptDto>.Fail("Exam not found");

        var hasPassedExam = await _db.StudentExamAttempts
            .AnyAsync(a => a.UserId == request.UserId && a.ExamId == request.ExamId && a.IsPassed, ct);

        if (hasPassedExam)
            return ApiResponse<ActiveExamAttemptDto>.Fail("لقد اجتزت هذا الامتحان بالفعل.");

        // ── Idempotency: reuse an existing in-progress attempt ───────────────
        // An attempt with Evaluation != null was already submitted (see SubmitExamCommand)
        var existingAttempt = await _db.StudentExamAttempts
            .FirstOrDefaultAsync(a => a.UserId == request.UserId
                                   && a.ExamId == request.ExamId
                                   && !a.IsPassed
                                   && a.Evaluation == null, ct);

        StudentExamAttempt attempt;
        var random = new Random();
        List<ExamQuestion> selectedQuestions;

        if (existingAttempt != null)
        {
            if (exam.DurationMinutes.HasValue && existingAttempt.StartedAt.HasValue)
            {
                var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60));
                var timeTaken = DateTime.UtcNow - existingAttempt.StartedAt.Value;
                if (timeTaken > timeAllowed)
                {
                    existingAttempt.IsTimeExpired = true;
                    existingAttempt.ScoreAchieved = 0;
                    existingAttempt.IsPassed = false;
                    existingAttempt.Evaluation = "انتهى الوقت";
                    await _db.SaveChangesAsync(ct);

                    return ApiResponse<ActiveExamAttemptDto>.Fail("لقد انتهى وقت المحاولة السابقة وتعتبر غير مجتازة.");
                }
            }

            attempt = existingAttempt;
            // Load previously assigned questions from placeholders
            await _db.Entry(attempt).Collection(a => a.Answers).LoadAsync(ct);
            var assignedQuestionIds = attempt.Answers.Select(a => a.ExamQuestionId).ToHashSet();
            selectedQuestions = exam.ExamQuestions.Where(eq => assignedQuestionIds.Contains(eq.Id)).ToList();
        }
        else
        {
            // Pick subset
            var allQuestions = exam.ExamQuestions.AsEnumerable();
            if (exam.IsRandomized)
            {
                allQuestions = allQuestions.OrderBy(x => random.Next());
            }
            else
            {
                allQuestions = allQuestions.OrderBy(q => q.Order);
            }

            if (exam.DisplayQuestionCount.HasValue && exam.DisplayQuestionCount.Value > 0)
            {
                // Note: we take the full set randomly shuffled, then take N
                allQuestions = allQuestions.Take(exam.DisplayQuestionCount.Value);
            }

            selectedQuestions = allQuestions.ToList();

            // Create a fresh attempt
            attempt = new StudentExamAttempt
            {
                UserId = request.UserId,
                ExamId = request.ExamId,
                StartedAt = DateTime.UtcNow,
                ScoreAchieved = 0,
                IsTimeExpired = false,
                Answers = new List<StudentAnswer>()
            };

            _db.StudentExamAttempts.Add(attempt);
            await _db.SaveChangesAsync(ct);

            var answerPlaceholders = selectedQuestions
                .Select(examQuestion => new StudentAnswer
                {
                    Id = Guid.NewGuid(),
                    StudentExamAttemptId = attempt.Id,
                    ExamQuestionId = examQuestion.Id,
                    HintUsed = false,
                    IsCorrect = false,
                    PointsAwarded = 0
                })
                .ToList();

            _db.StudentAnswers.AddRange(answerPlaceholders);
            await _db.SaveChangesAsync(ct);
        }

        // Return the active subset
        var baseQuery = selectedQuestions.AsEnumerable();
        if (exam.IsRandomized)
        {
            baseQuery = baseQuery.OrderBy(x => random.Next());
        }
        else
        {
            baseQuery = baseQuery.OrderBy(q => q.Order);
        }

        var questionDtos = baseQuery.Select(eq =>
        {
            var options = eq.Question.Options
                .OrderBy(x => random.Next()) // Shuffle options
                .Select(o => new QuestionOptionViewDto(o.Id, o.Text))
                .ToList();

            string? baseText = null;
            int? mistakeStartIndex = null;
            int? mistakeEndIndex = null;
            if (eq.Question is FindTheMistakeQuestion ftm)
            {
                baseText = ftm.BaseText;
                mistakeStartIndex = ftm.MistakeStartIndex;
                mistakeEndIndex = ftm.MistakeEndIndex;
            }

            return new ExamQuestionViewDto(eq.Id, eq.Question.Text, eq.Question.Type.ToString(), eq.Points, eq.Question.HintText, baseText, mistakeStartIndex, mistakeEndIndex, options);
        }).ToList();


        var startedAt = attempt.StartedAt ?? DateTime.UtcNow;

        int? remainingSeconds = null;
        if (exam.DurationMinutes.HasValue)
        {
            var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value);
            var timeTaken = DateTime.UtcNow - startedAt;
            remainingSeconds = (int)Math.Max(0, (timeAllowed - timeTaken).TotalSeconds);
        }

        var dto = new ActiveExamAttemptDto(
            attempt.Id,
            exam.Title,
            exam.Description,
            startedAt,
            exam.DurationMinutes,
            remainingSeconds,
            exam.TotalScore,
            lesson?.Id,
            lesson?.ContentSection?.Term?.PackageId,
            questionDtos
        );

        return ApiResponse<ActiveExamAttemptDto>.Ok(dto);
    }
}

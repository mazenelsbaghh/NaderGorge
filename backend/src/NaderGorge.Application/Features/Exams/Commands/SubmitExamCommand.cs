using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record SubmitExamCommand(Guid ExamId, Guid AttemptId, Guid UserId, List<AnswerSubmissionDto> Answers) : IRequest<ApiResponse<ExamResultDto>>;

public record AnswerSubmissionDto(Guid ExamQuestionId, Guid? SelectedOptionId, string? AnswerText, string? SelectedText = null, string? AudioUrl = null);

public record ExamResultDto(Guid AttemptId, decimal ScoreAchieved, decimal TotalScore, bool IsPassed, bool BlocksNextLesson, string Evaluation, bool IsTimeExpired, string ResultState, Guid? LessonId, Guid? PackageId, List<ExamQuestionReviewDto> Questions);

public class SubmitExamCommandHandler : IRequestHandler<SubmitExamCommand, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPublisher _publisher;
    private readonly NaderGorge.Application.Interfaces.IJobEnqueuer _jobEnqueuer;
    private readonly ICachedPlatformSettingsReader _cachedPlatformSettingsReader;

    public SubmitExamCommandHandler(IAppDbContext db, IPublisher publisher, NaderGorge.Application.Interfaces.IJobEnqueuer jobEnqueuer)
        : this(db, publisher, jobEnqueuer, new DefaultCachedPlatformSettingsReader())
    {
    }

    public SubmitExamCommandHandler(
        IAppDbContext db,
        IPublisher publisher,
        NaderGorge.Application.Interfaces.IJobEnqueuer jobEnqueuer,
        ICachedPlatformSettingsReader cachedPlatformSettingsReader)
    {
        _db = db;
        _publisher = publisher;
        _jobEnqueuer = jobEnqueuer;
        _cachedPlatformSettingsReader = cachedPlatformSettingsReader;
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(SubmitExamCommand request, CancellationToken ct)
    {
        var exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions)
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Exam not found");
        }

        var attempt = await _db.StudentExamAttempts
            .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, ct);

        if (attempt == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Attempt not found or invalid.");
        }

        var alreadySubmitted = attempt.Evaluation != null
            || await _db.EssaySubmissions.AnyAsync(e => e.StudentExamAttemptId == attempt.Id, ct)
            || await _db.StudentAnswers.AnyAsync(
                a => a.StudentExamAttemptId == attempt.Id &&
                     (a.SelectedOptionId != null || !string.IsNullOrWhiteSpace(a.SubmittedText)),
                ct);

        if (alreadySubmitted)
        {
            return ApiResponse<ExamResultDto>.Fail("This attempt has already been submitted.");
        }

        var platformSettings = await _cachedPlatformSettingsReader.GetAsync(ct);
        var evaluationTimestamp = DateTime.UtcNow;

        attempt.ScoreAchieved = 0;

        if (exam.DurationMinutes.HasValue && attempt.StartedAt.HasValue)
        {
            var timeAllowed = TimeSpan.FromMinutes(exam.DurationMinutes.Value).Add(TimeSpan.FromSeconds(60));
            var timeTaken = evaluationTimestamp - attempt.StartedAt.Value;
            if (timeTaken > timeAllowed)
            {
                attempt.IsTimeExpired = true;
            }
        }

        var existingAnswers = await _db.StudentAnswers
            .Where(answer => answer.StudentExamAttemptId == attempt.Id)
            .ToDictionaryAsync(answer => answer.ExamQuestionId, ct);

        decimal rawPointsEarned = 0;
        decimal rawPointsPossible = exam.ExamQuestions
            .Where(q => existingAnswers.ContainsKey(q.Id))
            .Sum(q => q.Points);

        var submittedAnswers = request.Answers ?? new List<AnswerSubmissionDto>();
        var questionSnapshotsByQuestion = new Dictionary<Guid, QuestionReviewSnapshot>();
        var hasEssayQuestions = false;

        foreach (var submission in submittedAnswers)
        {
            var examQuestion = exam.ExamQuestions.FirstOrDefault(x => x.Id == submission.ExamQuestionId);
            if (examQuestion == null)
            {
                continue;
            }

            var answer = GetOrCreateAnswer(existingAnswers, attempt, examQuestion.Id);

            switch (examQuestion.Question.Type)
            {
                case QuestionType.Essay:
                    hasEssayQuestions = true;
                    HandleEssaySubmission(questionSnapshotsByQuestion, request, examQuestion, submission, answer);
                    break;

                case QuestionType.FindTheMistake:
                    rawPointsEarned += HandleFindTheMistakeSubmission(
                        questionSnapshotsByQuestion,
                        examQuestion,
                        submission,
                        answer,
                        0m); // User explicitly requested 0 penalty for any lifelines
                    break;

                default:
                    rawPointsEarned += HandleMcqSubmission(
                        questionSnapshotsByQuestion,
                        examQuestion,
                        submission,
                        answer,
                        0m); // User explicitly requested 0 penalty for any lifelines
                    break;
            }
        }

        var scaledScore = GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);
        attempt.ScoreAchieved = attempt.IsTimeExpired ? 0 : scaledScore;
        attempt.IsPassed = !attempt.IsTimeExpired && (hasEssayQuestions ? false : scaledScore >= exam.PassingScore);
        attempt.Evaluation = attempt.IsTimeExpired
            ? "انتهى الوقت"
            : (hasEssayQuestions
                ? "قيد التصحيح"
                : GradingEvaluationService.DetermineEvaluation(scaledScore, exam.PassingScore, exam.TotalScore));

        var lesson = await _db.Lessons
            .Include(l => l.ContentSection)
            .ThenInclude(cs => cs.Term)
            .FirstOrDefaultAsync(l => l.ExamId == exam.Id, ct);

        bool blocksNextLesson = false;
        if (lesson != null)
        {
            var progress = await _db.LessonProgresses
                .FirstOrDefaultAsync(lp => lp.UserId == request.UserId && lp.LessonId == lesson.Id, ct);

            if (progress == null)
            {
                progress = new LessonProgress
                {
                    UserId = request.UserId,
                    LessonId = lesson.Id,
                    IsCompleted = attempt.IsPassed,
                    IsManuallyUnlocked = false
                };
                _db.LessonProgresses.Add(progress);
            }
            else if (attempt.IsPassed)
            {
                progress.IsCompleted = true;
            }

            blocksNextLesson = !attempt.IsPassed && !progress.IsManuallyUnlocked;
        }

        try
        {
            await _db.SaveChangesAsync(ct);

            var pendingEssays = _db.EssaySubmissions.Local
                .Where(essay => essay.StudentExamAttemptId == attempt.Id)
                .ToList();

            foreach (var essay in pendingEssays)
            {
                var examQuestion = exam.ExamQuestions.FirstOrDefault(x => x.Question.Id == essay.QuestionId);
                var expectedAnswer = examQuestion?.Question?.WrittenCorrection ?? string.Empty;

                await _jobEnqueuer.EnqueueJobAsync("bullmq-bridge-ingest", "evaluateEssay", new
                {
                    essaySubmissionId = essay.Id,
                    questionId = essay.QuestionId,
                    studentId = essay.StudentId,
                    answerText = essay.AnswerText,
                    expectedAnswer
                });
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            var persistedAttempt = await _db.StudentExamAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, ct);

            if (persistedAttempt != null)
            {
                var persistedLesson = await _db.Lessons
                    .AsNoTracking()
                    .Include(l => l.ContentSection)
                    .ThenInclude(cs => cs.Term)
                    .FirstOrDefaultAsync(l => l.ExamId == request.ExamId, ct);

                var persistedProgress = persistedLesson == null
                    ? null
                    : await _db.LessonProgresses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(lp => lp.UserId == request.UserId && lp.LessonId == persistedLesson.Id, ct);

                var persistedBlocksNextLesson = !persistedAttempt.IsPassed && !(persistedProgress?.IsManuallyUnlocked ?? false);

                var persistedAnswers = await _db.StudentAnswers
                    .AsNoTracking()
                    .Include(a => a.SelectedOption)
                    .Where(a => a.StudentExamAttemptId == persistedAttempt.Id)
                    .ToListAsync(ct);

                var persistedEssays = await _db.EssaySubmissions
                    .AsNoTracking()
                    .Where(e => e.StudentExamAttemptId == persistedAttempt.Id)
                    .ToListAsync(ct);

                return ApiResponse<ExamResultDto>.Ok(
                    ExamResultBuilder.Build(
                        exam,
                        persistedAttempt,
                        persistedBlocksNextLesson,
                        persistedLesson?.Id,
                        persistedLesson?.ContentSection?.Term?.PackageId,
                        BuildQuestionReviewSnapshots(persistedAnswers, persistedEssays, exam),
                        revealCorrectAnswers: true,
                        resultState: DetermineResultState(persistedEssays)),
                    persistedAttempt.IsPassed ? "Exam passed!" : "Exam failed.");
            }

            return ApiResponse<ExamResultDto>.Fail("تعذر حفظ تسليم الامتحان. حاول مرة أخرى.");
        }

        if (attempt.IsPassed)
        {
            var basePoints = (int)exam.TotalScore > 0 ? (int)exam.TotalScore : 50;
            await _publisher.Publish(
                new NaderGorge.Application.Features.Gamification.Commands.AcademicTaskCompletedEvent(
                    request.UserId,
                    NaderGorge.Domain.Entities.Gamification.GamificationEventType.PerfectExam,
                    basePoints),
                ct);
        }

        var result = ExamResultBuilder.Build(
            exam,
            attempt,
            blocksNextLesson,
            lesson?.Id,
            lesson?.ContentSection?.Term?.PackageId,
            questionSnapshotsByQuestion,
            revealCorrectAnswers: true,
            resultState: hasEssayQuestions ? "Pending" : "Completed");

        return ApiResponse<ExamResultDto>.Ok(result, attempt.IsPassed ? "Exam passed!" : "Exam failed.");
    }

    private void HandleEssaySubmission(
        IDictionary<Guid, QuestionReviewSnapshot> questionSnapshotsByQuestion,
        SubmitExamCommand request,
        ExamQuestion examQuestion,
        AnswerSubmissionDto submission,
        StudentAnswer answer)
    {
        var answerText = submission.AnswerText?.Trim();
        answer.SubmittedText = answerText;
        answer.SelectedOptionId = null;
        answer.IsCorrect = false;
        answer.PointsAwarded = 0;

        var essaySubmission = new EssaySubmission
        {
            StudentId = request.UserId,
            QuestionId = examQuestion.Question.Id,
            StudentExamAttemptId = answer.StudentExamAttemptId,
            AnswerText = answerText ?? string.Empty,
            AudioUrl = string.IsNullOrWhiteSpace(submission.AudioUrl) ? null : submission.AudioUrl.Trim(),
            Status = EssaySubmissionStatus.WaitAI
        };

        _db.EssaySubmissions.Add(essaySubmission);

        questionSnapshotsByQuestion[examQuestion.Id] = new QuestionReviewSnapshot(
            answerText,
            !string.IsNullOrWhiteSpace(answerText),
            false,
            0);
    }

    private static decimal HandleFindTheMistakeSubmission(
        IDictionary<Guid, QuestionReviewSnapshot> questionSnapshotsByQuestion,
        ExamQuestion examQuestion,
        AnswerSubmissionDto submission,
        StudentAnswer answer,
        decimal hintPenaltyPercentage)
    {
        var submittedText = submission.SelectedText?.Trim();
        var correctText = ExamResultBuilder.GetCorrectReviewText(examQuestion.Question)?.Trim();
        var correctOption = examQuestion.Question.Options.FirstOrDefault(option => option.IsCorrect);
        var fallbackOption = examQuestion.Question.Options.FirstOrDefault(option => !option.IsCorrect) ?? correctOption;

        var isCorrect = !string.IsNullOrWhiteSpace(submittedText) &&
            !string.IsNullOrWhiteSpace(correctText) &&
            string.Equals(submittedText, correctText, StringComparison.Ordinal);

        var selectedOptionId = isCorrect ? correctOption?.Id : fallbackOption?.Id;
        if (selectedOptionId == null)
        {
            return 0;
        }

        var pointsAwarded = isCorrect
            ? ExamResultBuilder.ApplyHintPenalty(examQuestion.Points, answer.HintUsed, hintPenaltyPercentage)
            : 0;

        answer.SelectedOptionId = selectedOptionId;
        answer.SubmittedText = submittedText;
        answer.IsCorrect = isCorrect;
        answer.PointsAwarded = pointsAwarded;

        questionSnapshotsByQuestion[examQuestion.Id] = new QuestionReviewSnapshot(
            submittedText,
            !string.IsNullOrWhiteSpace(submittedText),
            isCorrect,
            pointsAwarded);

        return pointsAwarded;
    }

    private static decimal HandleMcqSubmission(
        IDictionary<Guid, QuestionReviewSnapshot> questionSnapshotsByQuestion,
        ExamQuestion examQuestion,
        AnswerSubmissionDto submission,
        StudentAnswer answer,
        decimal hintPenaltyPercentage)
    {
        if (submission.SelectedOptionId == null)
        {
            return 0;
        }

        var selectedOption = examQuestion.Question.Options.FirstOrDefault(option => option.Id == submission.SelectedOptionId);
        if (selectedOption == null)
        {
            return 0;
        }

        var isCorrect = selectedOption.IsCorrect;
        var pointsAwarded = isCorrect
            ? ExamResultBuilder.ApplyHintPenalty(examQuestion.Points, answer.HintUsed, hintPenaltyPercentage)
            : 0;

        answer.SelectedOptionId = selectedOption.Id;
        answer.SubmittedText = selectedOption.Text;
        answer.IsCorrect = isCorrect;
        answer.PointsAwarded = pointsAwarded;

        questionSnapshotsByQuestion[examQuestion.Id] = new QuestionReviewSnapshot(
            selectedOption.Text,
            true,
            isCorrect,
            pointsAwarded);

        return pointsAwarded;
    }

    private StudentAnswer GetOrCreateAnswer(
        IDictionary<Guid, StudentAnswer> existingAnswers,
        StudentExamAttempt attempt,
        Guid examQuestionId)
    {
        if (existingAnswers.TryGetValue(examQuestionId, out var answer))
        {
            return answer;
        }

        var created = new StudentAnswer
        {
            Id = Guid.NewGuid(),
            StudentExamAttemptId = attempt.Id,
            ExamQuestionId = examQuestionId,
            HintUsed = false,
            IsCorrect = false,
            PointsAwarded = 0
        };

        existingAnswers[examQuestionId] = created;
        _db.StudentAnswers.Add(created);
        return created;
    }

    private static Dictionary<Guid, QuestionReviewSnapshot> BuildQuestionReviewSnapshots(
        IEnumerable<StudentAnswer> answers,
        IEnumerable<EssaySubmission> essays,
        Exam exam)
    {
        var snapshots = ExamResultBuilder.BuildQuestionReviewSnapshots(answers);
        var questionIdToExamQuestionId = exam.ExamQuestions.ToDictionary(eq => eq.Question.Id, eq => eq.Id);

        foreach (var essay in essays)
        {
            if (!questionIdToExamQuestionId.TryGetValue(essay.QuestionId, out var examQuestionId))
            {
                continue;
            }

            var isTeacherGraded = essay.Status == EssaySubmissionStatus.TeacherGraded;
            snapshots[examQuestionId] = new QuestionReviewSnapshot(
                essay.AnswerText,
                !string.IsNullOrWhiteSpace(essay.AnswerText),
                isTeacherGraded,
                isTeacherGraded ? essay.TeacherFinalScore ?? 0 : 0);
        }

        return snapshots;
    }

    private static string DetermineResultState(IEnumerable<EssaySubmission> essays)
    {
        var essayList = essays.ToList();
        if (essayList.Count == 0)
        {
            return "Completed";
        }

        if (essayList.Any(e => e.Status == EssaySubmissionStatus.WaitAI))
        {
            return "Pending";
        }

        if (essayList.Any(e => e.Status != EssaySubmissionStatus.TeacherGraded))
        {
            return "PartiallyGraded";
        }

        return "Completed";
    }

    private sealed class DefaultCachedPlatformSettingsReader : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(CachedPlatformSettings.Default);
        }

        public void Invalidate()
        {
        }
    }
}

using MediatR;
using NaderGorge.Application.Features.Assessments;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public record SubmitExamCommand(Guid ExamId, Guid AttemptId, Guid UserId, List<AnswerSubmissionDto> Answers, Guid? RevisionId = null, long? DraftVersion = null) : IRequest<ApiResponse<ExamResultDto>>;

public record AnswerSubmissionDto(Guid ExamQuestionId, Guid? SelectedOptionId, string? AnswerText, string? SelectedText = null, string? AudioUrl = null);

public record ExamResultDto(Guid AttemptId, decimal ScoreAchieved, decimal TotalScore, bool IsPassed, bool BlocksNextLesson, string Evaluation, bool IsTimeExpired, string ResultState, Guid? LessonId, Guid? PackageId, List<ExamQuestionReviewDto> Questions, PartialExamResultDto? PartialResult = null, bool AllowRetake = true, bool AnswersVisible = true, Guid? ExamId = null, string EssayScoreStatus = "NotApplicable");

public class SubmitExamCommandHandler : IRequestHandler<SubmitExamCommand, ApiResponse<ExamResultDto>>
{
    private readonly IAppDbContext _db;

    public SubmitExamCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public SubmitExamCommandHandler(IAppDbContext db, IPublisher publisher, NaderGorge.Application.Interfaces.IJobEnqueuer jobEnqueuer)
        : this(db)
    {
    }

    public SubmitExamCommandHandler(
        IAppDbContext db,
        IPublisher publisher,
        NaderGorge.Application.Interfaces.IJobEnqueuer jobEnqueuer,
        ICachedPlatformSettingsReader cachedPlatformSettingsReader)
        : this(db)
    {
    }

    public async Task<ApiResponse<ExamResultDto>> Handle(SubmitExamCommand request, CancellationToken ct)
    {
        if (_db is not DbContext context
            || context.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
            return await HandleOnce(request, ct, progressConflictRetries: 0);

        ApiResponse<ExamResultDto>? submitted = null;
        await using (var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct))
        {
            // The attempt row also protects its answer placeholders and effect events from overlapping submissions.
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                SELECT "Id" FROM student_exam_attempts
                WHERE "Id" = {request.AttemptId} AND "UserId" = {request.UserId} AND "ExamId" = {request.ExamId}
                FOR UPDATE
                """, ct);
            _db.ClearTrackedChanges();
            if (!await RequiresRevisionCompletionAsync(request, ct))
            {
                submitted = await HandleOnce(request, ct, progressConflictRetries: 0);
                await transaction.CommitAsync(ct);
            }
        }
        // Revision completion owns its serializable transaction and rereads the pending revision after acquiring it.
        return submitted ?? await HandleOnce(request, ct, progressConflictRetries: 0);
    }

    private async Task<bool> RequiresRevisionCompletionAsync(SubmitExamCommand request, CancellationToken ct)
    {
        var snapshot = await _db.StudentExamAttempts.Where(attempt => attempt.Id == request.AttemptId
            && attempt.UserId == request.UserId && attempt.ExamId == request.ExamId)
            .Select(attempt => attempt.DefinitionSnapshotJson).SingleOrDefaultAsync(ct);
        return snapshot is not null
            && AssessmentDefinitionSnapshot.Read(snapshot, "exam", request.ExamId).Revision?.RequiresCompletion == true;
    }

    private async Task<ApiResponse<ExamResultDto>> HandleOnce(
        SubmitExamCommand request, CancellationToken ct, int progressConflictRetries)
    {
        if (request.Answers?.Any(answer => !string.IsNullOrWhiteSpace(answer.AudioUrl)) == true)
            return ApiResponse<ExamResultDto>.Fail("الإجابات الصوتية غير مسموح بها في الامتحان. اكتب إجابتك ثم أرسلها.");

        var exam = await _db.Exams
            .AsNoTracking()
            .Include(e => e.ExamQuestions.Where(q => !q.IsRetired))
            .ThenInclude(eq => eq.Question)
            .ThenInclude(q => q.Options.Where(o => !o.IsRetired))
            .FirstOrDefaultAsync(e => e.Id == request.ExamId, ct);

        if (exam == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Exam not found");
        }

        var attempt = await _db.StudentExamAttempts
            .Include(a => a.Answers).FirstOrDefaultAsync(a => a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, ct);

        if (attempt == null)
        {
            return ApiResponse<ExamResultDto>.Fail("Attempt not found or invalid.");
        }

        try
        {
            if (AssessmentAttemptScaleNormalizer.Normalize(attempt))
                await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException error)
        {
            return ApiResponse<ExamResultDto>.Fail(error.Message);
        }

        var allowRetake = !exam.SingleAttemptOnly;
        exam = AssessmentDefinitionSnapshot.ResolveExam(exam, attempt.DefinitionSnapshotJson);

        if (attempt.DefinitionSnapshotJson is not null
            && AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId).Revision?.RequiresCompletion == true)
        {
            var completed = await new ExamRevisionCompletion(_db).Submit(request, ct);
            if (!completed.Success) return ApiResponse<ExamResultDto>.Fail(completed.Message ?? "تعذر حفظ الاستكمال.");
            return await new Queries.GetExamAttemptResultQueryHandler(_db).Handle(new(attempt.Id, request.UserId), ct);
        }

        var alreadySubmitted = attempt.Evaluation != null
            || await _db.EssaySubmissions.AnyAsync(e => e.StudentExamAttemptId == attempt.Id, ct)
            || await _db.StudentAnswers.AnyAsync(
                a => a.StudentExamAttemptId == attempt.Id &&
                     (a.SelectedOptionId != null || !string.IsNullOrWhiteSpace(a.SubmittedText)),
                ct);

        if (alreadySubmitted)
        {
            return await new Queries.GetExamAttemptResultQueryHandler(_db)
                .Handle(new(request.AttemptId, request.UserId), ct);
        }

        if (request.DraftVersion.HasValue)
        {
            var saved = await ExamDraftState.Submission(_db, request,
                AssessmentDefinitionSnapshot.FromExam(exam), ct);
            if (!saved.Success) return ApiResponse<ExamResultDto>.Fail(saved.Message!, saved.Errors);
            request = request with { Answers = saved.Data! };
        }

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
        var hasEssayQuestions = exam.EssayGradingMode != ExamEssayGradingMode.ModelAnswerOnly
            && exam.ExamQuestions.Any(q => q.Question.Type == QuestionType.Essay);

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
                    answer.IsExcludedFromScore = exam.EssayGradingMode == ExamEssayGradingMode.ModelAnswerOnly;
                    SaveEssayAnswer(questionSnapshotsByQuestion, submission, answer);
                    if (exam.EssayGradingMode != ExamEssayGradingMode.ModelAnswerOnly)
                    {
                        AddEssaySubmission(request, examQuestion, answer, exam.EssayGradingMode);
                    }
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

        foreach (var missingEssay in exam.ExamQuestions.Where(q => q.Question.Type == QuestionType.Essay
            && !questionSnapshotsByQuestion.ContainsKey(q.Id)))
        {
            var answer = GetOrCreateAnswer(existingAnswers, attempt, missingEssay.Id);
            answer.IsExcludedFromScore = exam.EssayGradingMode == ExamEssayGradingMode.ModelAnswerOnly;
            SaveEssayAnswer(questionSnapshotsByQuestion, new(missingEssay.Id, null, null), answer);
            if (exam.EssayGradingMode != ExamEssayGradingMode.ModelAnswerOnly)
                AddEssaySubmission(request, missingEssay, answer, exam.EssayGradingMode);
        }

        var scaledScore = GradingEvaluationService.CalculateScaledScore(rawPointsEarned, rawPointsPossible, exam.TotalScore);
        attempt.SubmittedAt ??= DateTime.UtcNow;
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

            blocksNextLesson = !attempt.IsPassed && attempt.Evaluation != ExamAccessPolicy.PendingReviewEvaluation && !progress.IsManuallyUnlocked;
        }

        try
        {
            var examSubmittedEvent = new OutboxEvent
            {
                Type = "ExamSubmitted",
                TargetUserId = request.UserId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    examId = request.ExamId,
                    attemptId = request.AttemptId,
                    isPassed = attempt.IsPassed,
                    score = attempt.ScoreAchieved
                })
            };
            _db.OutboxEvents.Add(examSubmittedEvent);
            ExamSubmissionEffects.Enqueue(_db, attempt, exam);

            // Persist the AI work alongside the submitted attempt. The outbox worker
            // dispatches it independently of this HTTP request, so closing the page
            // cannot cancel or lose essay grading.
            var submittedEssays = _db.EssaySubmissions.Local
                .Where(essay => essay.StudentExamAttemptId == attempt.Id)
                .ToList();
            foreach (var essay in submittedEssays)
            {
                var examQuestion = exam.ExamQuestions.FirstOrDefault(question => question.Question.Id == essay.QuestionId);
                if (examQuestion is not null)
                    EssayEvaluationQueue.Enqueue(_db, essay, examQuestion.Question.Text, examQuestion.Question.WrittenCorrection);
            }

            if (!hasEssayQuestions)
            {
                var examGradedEvent = new OutboxEvent
                {
                    Type = "ExamGraded",
                    TargetUserId = request.UserId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        examId = request.ExamId,
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
                    TargetUserId = request.UserId.ToString(),
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        examId = request.ExamId,
                        attemptId = attempt.Id,
                        isPassed = attempt.IsPassed,
                        score = attempt.ScoreAchieved
                    })
                };
                _db.OutboxEvents.Add(examResultReadyEvent);
            }

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
                        .FirstOrDefaultAsync(lp => lp.UserId == request.UserId && lp.LessonId == nextLesson.Id, ct);

                    bool nextIsLocked = !attempt.IsPassed
                        && attempt.Evaluation != ExamAccessPolicy.PendingReviewEvaluation
                        && (nextLessonProgress == null || !nextLessonProgress.IsManuallyUnlocked);

                    var lockEvent = new OutboxEvent
                    {
                        Type = nextIsLocked ? "LessonLocked" : "LessonUnlocked",
                        TargetUserId = request.UserId.ToString(),
                        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            lessonId = nextLesson.Id,
                            reason = nextIsLocked ? $"يجب اجتياز امتحان '{exam.Title}' بنجاح." : null
                        })
                    };
                    _db.OutboxEvents.Add(lockEvent);
                }
            }

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException error) when (
                progressConflictRetries < 2 && IsLessonProgressInsertConflict(error))
            {
                // SaveChanges rolled back the entire submission. Reload the winner's
                // progress/attempt before rebuilding events; no external job was sent yet.
                _db.ClearTrackedChanges();
                await Task.Delay(25 * (progressConflictRetries + 1), ct);
                return await HandleOnce(request, ct, progressConflictRetries + 1);
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

                var persistedBlocksNextLesson = !persistedAttempt.IsPassed
                    && persistedAttempt.Evaluation != ExamAccessPolicy.PendingReviewEvaluation
                    && !(persistedProgress?.IsManuallyUnlocked ?? false);

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
                        resultState: DetermineResultState(persistedEssays)) with { AllowRetake = allowRetake },
                    persistedAttempt.IsPassed ? "Exam passed!" : "Exam failed.");
            }

            return ApiResponse<ExamResultDto>.Fail("تعذر حفظ تسليم الامتحان. حاول مرة أخرى.");
        }

        var result = ExamResultBuilder.Build(
            exam,
            attempt,
            blocksNextLesson,
            lesson?.Id,
            lesson?.ContentSection?.Term?.PackageId,
            questionSnapshotsByQuestion,
            revealCorrectAnswers: true,
            resultState: DetermineResultState(_db.EssaySubmissions.Local.Where(e => e.StudentExamAttemptId == attempt.Id))) with { AllowRetake = allowRetake };

        return ApiResponse<ExamResultDto>.Ok(result, attempt.IsPassed ? "Exam passed!" : "Exam failed.");
    }

    private static bool IsLessonProgressInsertConflict(DbUpdateException error)
    {
        var databaseError = error.InnerException;
        return databaseError?.GetType().GetProperty("SqlState")?.GetValue(databaseError)?.ToString() == "23505"
            && databaseError.GetType().GetProperty("ConstraintName")?.GetValue(databaseError)?.ToString()
                == "IX_lesson_progress_UserId_LessonId";
    }

    private static void SaveEssayAnswer(IDictionary<Guid, QuestionReviewSnapshot> snapshots,
        AnswerSubmissionDto submission, StudentAnswer answer)
    {
        answer.SubmittedText = submission.AnswerText?.Trim();
        answer.SelectedOptionId = null;
        answer.IsCorrect = false;
        answer.PointsAwarded = 0;
        snapshots[answer.ExamQuestionId] = new(answer.SubmittedText,
            !string.IsNullOrWhiteSpace(answer.SubmittedText), false, 0, IsPendingGrading: !answer.IsExcludedFromScore);
    }

    private void AddEssaySubmission(SubmitExamCommand request, ExamQuestion question,
        StudentAnswer answer, ExamEssayGradingMode mode)
    {
        _db.EssaySubmissions.Add(new EssaySubmission
        {
            StudentId = request.UserId, QuestionId = question.Question.Id,
            StudentExamAttemptId = answer.StudentExamAttemptId,
            AnswerText = answer.SubmittedText ?? string.Empty,
            Status = mode == ExamEssayGradingMode.Manual
                ? EssaySubmissionStatus.WaitTeacher : EssaySubmissionStatus.WaitAI
        });
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
            pointsAwarded,
            null);

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
            pointsAwarded,
            null);

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
        var snapshots = ExamResultBuilder.BuildQuestionReviewSnapshots(answers, exam);
        var questionIdToExamQuestionId = exam.ExamQuestions.ToDictionary(eq => eq.Question.Id, eq => eq.Id);

        foreach (var essay in essays)
        {
            if (!questionIdToExamQuestionId.TryGetValue(essay.QuestionId, out var examQuestionId))
            {
                continue;
            }

            var examQuestion = exam.ExamQuestions.First(eq => eq.Id == examQuestionId);
            snapshots[examQuestionId] = ExamResultBuilder.BuildEssayReviewSnapshot(essay, examQuestion.Points);
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

}

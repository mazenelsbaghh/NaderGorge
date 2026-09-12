using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Exams.Commands;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Assessments;

public sealed class ExamRevisionCompletion(IAppDbContext db)
{
    public Task<ApiResponse<AssessmentDefinitionSnapshot>> Start(Guid attemptId, Guid studentId, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var attempt = await db.StudentExamAttempts.SingleOrDefaultAsync(a => a.Id == attemptId && a.UserId == studentId, retryCt);
            var definition = ReadPending(attempt);
            if (definition is null) return ApiResponse<AssessmentDefinitionSnapshot>.Fail("تغيّرت المحاولة. أعد فتح الامتحان.");
            if (definition.CompletionStartedAt is null)
            {
                attempt!.StartedAt = DateTime.UtcNow;
                definition = definition with { CompletionStartedAt = attempt.StartedAt };
                attempt.DefinitionSnapshotJson = definition.ToJson();
                await db.SaveChangesAsync(retryCt);
            }
            await transaction.CommitAsync(retryCt);
            return ApiResponse<AssessmentDefinitionSnapshot>.Ok(definition);
        }, ct);

    public Task<ApiResponse<bool>> Submit(SubmitExamCommand request, CancellationToken ct) =>
        SerializationRetryHelper.ExecuteAsync(async retryCt =>
        {
            db.ClearTrackedChanges();
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, retryCt);
            var attempt = await db.StudentExamAttempts.Include(a => a.Answers).SingleOrDefaultAsync(a =>
                a.Id == request.AttemptId && a.UserId == request.UserId && a.ExamId == request.ExamId, retryCt);
            var definition = ReadPending(attempt);
            if (definition is null || definition.CompletionStartedAt is null || request.RevisionId is null
                || request.RevisionId != definition.RevisionId)
                return ApiResponse<bool>.Fail("أعد فتح الامتحان لتحميل الأسئلة المطلوبة للاستكمال.");
            var required = definition.Revision!.Answers.Where(a => a.RequiresCompletion && !a.Excluded)
                .Select(a => a.QuestionId).ToHashSet();
            if (request.Answers.Select(a => a.ExamQuestionId).Distinct().Count() != request.Answers.Count
                || request.Answers.Any(a => !required.Contains(a.ExamQuestionId)))
                return ApiResponse<bool>.Fail("يمكن إرسال الأسئلة المضافة فقط؛ إجاباتك السابقة محفوظة.");
            var previousJson = attempt!.DefinitionSnapshotJson;
            CompleteAnswers(attempt, definition, request);
            db.AuditLogs.Add(new AuditLog
            {
                Action = "ExamRevisionCompleted", EntityType = "Exam", EntityId = attempt.Id,
                PerformedByUserId = request.UserId, CorrelationId = request.RevisionId.ToString(),
                OldValues = previousJson, NewValues = attempt.DefinitionSnapshotJson
            });
            NotifyAttempt(attempt);
            await db.SaveChangesAsync(retryCt);
            await transaction.CommitAsync(retryCt);
            return ApiResponse<bool>.Ok(true);
        }, ct);

    private static AssessmentDefinitionSnapshot? ReadPending(StudentExamAttempt? attempt)
    {
        if (attempt?.DefinitionSnapshotJson is null || attempt.Evaluation is not null) return null;
        var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson, "exam", attempt.ExamId);
        return definition.Revision?.RequiresCompletion == true ? definition : null;
    }

    private void CompleteAnswers(StudentExamAttempt attempt, AssessmentDefinitionSnapshot definition, SubmitExamCommand request)
    {
        var provided = request.Answers.ToDictionary(a => a.ExamQuestionId);
        var questions = definition.Questions.ToDictionary(q => q.Id);
        var expired = definition.DurationMinutes is int minutes
            && DateTime.UtcNow - definition.CompletionStartedAt!.Value > TimeSpan.FromMinutes(minutes).Add(TimeSpan.FromSeconds(60));
        var grades = definition.Revision!.Answers.Select(grade => !grade.RequiresCompletion || grade.Excluded ? grade
            : CompleteAnswer(attempt, questions[grade.QuestionId], provided.GetValueOrDefault(grade.QuestionId), expired)).ToArray();
        var revision = definition.Revision! with { Answers = grades };
        attempt.DefinitionSnapshotJson = (definition with { Revision = revision }).ToJson();
        attempt.ScoreAchieved = revision.ScaledScore(definition.TotalScore);
        attempt.IsTimeExpired |= expired;
        attempt.IsPassed = !attempt.IsTimeExpired && !revision.RequiresReview && attempt.ScoreAchieved >= (definition.PassingScore ?? 0);
        attempt.Evaluation = revision.RequiresReview ? "قيد التصحيح" : attempt.IsTimeExpired ? "انتهى الوقت"
            : GradingEvaluationService.DetermineEvaluation(attempt.ScoreAchieved, definition.PassingScore ?? 0, definition.TotalScore);
    }

    private RevisionAnswer CompleteAnswer(StudentExamAttempt attempt, AssessmentQuestionSnapshot question,
        AnswerSubmissionDto? provided, bool expired)
    {
        var answer = attempt.Answers.Single(a => a.ExamQuestionId == question.Id);
        var option = question.Options.SingleOrDefault(o => o.Id == provided?.SelectedOptionId);
        answer.SelectedOptionId = question.Type == (int)QuestionType.MCQ ? option?.Id : null;
        answer.SubmittedText = question.Type == (int)QuestionType.MCQ ? option?.Text
            : question.Type == (int)QuestionType.FindTheMistake ? provided?.SelectedText?.Trim() : provided?.AnswerText?.Trim();
        var hasAnswer = !string.IsNullOrWhiteSpace(answer.SubmittedText)
            || (question.Type == (int)QuestionType.Essay && !string.IsNullOrWhiteSpace(provided?.AudioUrl));
        var points = expired || !hasAnswer ? 0 : AssessmentAttemptRegrader.AutomaticGrade(question,
            new(question.Id, answer.SubmittedText, answer.SelectedOptionId, null, false), "exam");
        answer.PointsAwarded = points ?? 0;
        answer.IsCorrect = points >= question.Points;
        if (question.Type == (int)QuestionType.Essay) AddEssay(attempt, question, provided, points);
        return new(question.Id, question.Points, points, false);
    }

    private void AddEssay(StudentExamAttempt attempt, AssessmentQuestionSnapshot question, AnswerSubmissionDto? provided, decimal? points)
    {
        var essay = new EssaySubmission
        {
            StudentExamAttemptId = attempt.Id, StudentId = attempt.UserId, QuestionId = question.BankQuestionId,
            AnswerText = provided?.AnswerText?.Trim() ?? string.Empty, AudioUrl = provided?.AudioUrl?.Trim(),
            Status = points is null ? EssaySubmissionStatus.WaitAI : EssaySubmissionStatus.TeacherGraded,
            TeacherFinalScore = points
        };
        db.EssaySubmissions.Add(essay);
        if (points is not null) return;
        EssayEvaluationQueue.Enqueue(db, essay, question.Text, question.WrittenCorrection);
    }

    private void NotifyAttempt(StudentExamAttempt attempt)
    {
        var payload = JsonSerializer.Serialize(new { examId = attempt.ExamId, attemptId = attempt.Id,
            isPassed = attempt.IsPassed, score = attempt.ScoreAchieved, evaluation = attempt.Evaluation });
        db.OutboxEvents.Add(new OutboxEvent { Type = "ExamSubmitted", TargetUserId = attempt.UserId.ToString(), PayloadJson = payload });
        var definition = AssessmentDefinitionSnapshot.Read(attempt.DefinitionSnapshotJson!, "exam", attempt.ExamId);
        if (definition.Revision!.RequiresReview) return;
        db.OutboxEvents.Add(new OutboxEvent { Type = "ExamGraded", TargetUserId = attempt.UserId.ToString(), PayloadJson = payload });
        db.OutboxEvents.Add(new OutboxEvent { Type = "ExamResultReady", TargetUserId = attempt.UserId.ToString(), PayloadJson = payload });
    }
}

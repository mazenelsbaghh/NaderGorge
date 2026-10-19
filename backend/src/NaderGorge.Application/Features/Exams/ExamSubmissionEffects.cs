using System.Text.Json;
using NaderGorge.Application.Features.Gamification.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams;

public static class ExamSubmissionEffects
{
    public const string ParentPushEventType = "ExamParentPushQueued";
    public const string AcademicRewardEventType = "ExamAcademicRewardQueued";

    public static void Enqueue(IAppDbContext db, StudentExamAttempt attempt, Exam exam)
    {
        db.OutboxEvents.Add(new OutboxEvent
        {
            Type = ParentPushEventType,
            TargetUserId = attempt.UserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new ExamParentPushPayload(attempt.UserId, exam.Title))
        });
        if (!attempt.IsPassed) return;
        var reward = new OutboxEvent { Type = AcademicRewardEventType };
        reward.PayloadJson = JsonSerializer.Serialize(new AcademicTaskCompletedEvent(attempt.UserId,
            GamificationEventType.PerfectExam, (int)exam.TotalScore > 0 ? (int)exam.TotalScore : 50, reward.Id));
        db.OutboxEvents.Add(reward);
    }
}

public record ExamParentPushPayload(Guid StudentId, string ExamTitle);

using System.Data;
using System.Text.Json;
using NaderGorge.Application.Features.Exams;
using NaderGorge.Application.Features.Gamification.Commands;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.BackgroundServices;

public static class ExamSubmissionOutboxDispatcher
{
    public static bool IsSubmissionEffect(OutboxEvent submissionEvent) =>
        submissionEvent.Type is ExamSubmissionEffects.ParentPushEventType or ExamSubmissionEffects.AcademicRewardEventType;

    public static async Task DispatchAsync(OutboxEvent submissionEvent, IServiceProvider services, CancellationToken ct)
    {
        if (submissionEvent.Type == ExamSubmissionEffects.ParentPushEventType)
        {
            await DispatchParentPushAsync(submissionEvent, services.GetRequiredService<IJobEnqueuer>());
            return;
        }
        await DispatchAcademicRewardAsync(submissionEvent, services.GetRequiredService<IAppDbContext>(), ct);
    }

    private static async Task DispatchAcademicRewardAsync(OutboxEvent submissionEvent, IAppDbContext db, CancellationToken ct)
    {
        var reward = JsonSerializer.Deserialize<AcademicTaskCompletedEvent>(submissionEvent.PayloadJson)
            ?? throw new InvalidOperationException("Exam academic reward payload is empty.");
        if (reward.SourceEventId != submissionEvent.Id)
            throw new InvalidOperationException("Exam academic reward requires its outbox identity.");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new AcademicTaskCompletedEventHandler(db).Handle(reward, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task DispatchParentPushAsync(OutboxEvent submissionEvent, IJobEnqueuer queue)
    {
        var payload = JsonSerializer.Deserialize<ExamParentPushPayload>(submissionEvent.PayloadJson)
            ?? throw new InvalidOperationException("Exam parent push payload is empty.");
        await queue.EnqueueJobAsync("notifications", "parent-push", new
        {
            outboxEventId = submissionEvent.Id,
            payload.StudentId,
            Title = "حل اختبار جديد",
            Body = $"تم حل اختبار {payload.ExamTitle} بنجاح.",
            Category = "Exam",
            ParentPush = true
        });
    }
}

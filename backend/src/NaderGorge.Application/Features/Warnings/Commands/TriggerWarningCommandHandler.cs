using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Warnings.Commands;

public class TriggerWarningCommandHandler : IRequestHandler<TriggerWarningCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _dbContext;
    private readonly IJobEnqueuer _jobEnqueuer;

    public TriggerWarningCommandHandler(IAppDbContext dbContext, IJobEnqueuer jobEnqueuer)
    {
        _dbContext = dbContext;
        _jobEnqueuer = jobEnqueuer;
    }

    public async Task<ApiResponse<Guid>> Handle(TriggerWarningCommand request, CancellationToken cancellationToken)
    {
        var studentExists = await _dbContext.Users.AnyAsync(u => u.Id == request.StudentId, cancellationToken);
        if (!studentExists)
            return ApiResponse<Guid>.Fail("Student not found.");

        var warning = new WarningEvent
        {
            StudentId = request.StudentId,
            Severity = request.Severity,
            TriggerReason = request.TriggerReason
        };

        _dbContext.WarningEvents.Add(warning);

        // Update Tracker
        var tracker = await _dbContext.StudentStatusTrackers
            .FirstOrDefaultAsync(t => t.StudentId == request.StudentId, cancellationToken);

        if (tracker == null)
        {
            tracker = new StudentStatusTracker
            {
                StudentId = request.StudentId,
                CurrentStatus = StudentCommitmentStatus.AtRisk
            };
            _dbContext.StudentStatusTrackers.Add(tracker);
        }
        else
        {
            tracker.CurrentStatus = StudentCommitmentStatus.AtRisk;
            tracker.LastEvaluatedAt = DateTime.UtcNow;
            _dbContext.StudentStatusTrackers.Update(tracker);
        }

        // If Severity is Critical, we might want to automatically create an Assistant Task
        if (request.Severity == WarningSeverity.Critical)
        {
            var assistantTask = new AssistantTaskQueue
            {
                TaskType = AssistantTaskType.FollowUpAtRisk,
                StudentId = request.StudentId,
                ReferenceEntityId = warning.Id
            };
            _dbContext.AssistantTasks.Add(assistantTask);
        }

        // Also create Application IN-APP Notification
        var notification = new NotificationEvent
        {
            UserId = request.StudentId,
            ChannelType = NotificationChannelType.InApp,
            Title = "Academic Warning",
            Body = $"You received a {request.Severity} warning: {request.TriggerReason}"
        };
        _dbContext.NotificationEvents.Add(notification);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue an SMS or Push Notification job via BullMQ
        await _jobEnqueuer.EnqueueJobAsync("notifications", "send-warning", new
        {
            WarningId = warning.Id,
            StudentId = request.StudentId,
            Severity = request.Severity.ToString(),
            Message = request.TriggerReason
        });

        return ApiResponse<Guid>.Ok(warning.Id, "Warning triggered successfully.");
    }
}

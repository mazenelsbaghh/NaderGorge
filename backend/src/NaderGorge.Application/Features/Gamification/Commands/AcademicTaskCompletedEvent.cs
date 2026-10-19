using MediatR;

using NaderGorge.Domain.Entities.Gamification;

namespace NaderGorge.Application.Features.Gamification.Commands;

public record AcademicTaskCompletedEvent(Guid StudentId, GamificationEventType TaskType, int BasePoints, Guid? SourceEventId = null) : INotification;

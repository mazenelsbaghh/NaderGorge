using System;
using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class ExtraWatchRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid LessonVideoId { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? RejectionReason { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual LessonVideo LessonVideo { get; set; } = null!;
}

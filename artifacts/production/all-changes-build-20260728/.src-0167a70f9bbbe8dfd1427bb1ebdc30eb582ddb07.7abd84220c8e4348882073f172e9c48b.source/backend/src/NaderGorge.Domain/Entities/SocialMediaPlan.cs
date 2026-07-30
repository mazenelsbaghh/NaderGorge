using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class SocialMediaPlan : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Script { get; set; }

    public SocialPlatform Platform { get; set; }
    public SocialPlanStatus Status { get; set; } = SocialPlanStatus.Draft;

    public DateTime ScheduledDate { get; set; }

    public Guid? MediaProductionPipelineId { get; set; }
    public MediaProductionPipeline? MediaProductionPipeline { get; set; }
}

using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class MediaProductionPipeline : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public MediaStage Stage { get; set; } = MediaStage.Preparation;

    public Guid? AssignedAgentId { get; set; }
    public User? AssignedAgent { get; set; }

    public string? AssetFolderUrl { get; set; }
    public int EditingErrorCount { get; set; } = 0;

    public DateTime? PublishedAt { get; set; }

    public ICollection<SocialMediaPlan> SocialMediaPlans { get; set; } = new List<SocialMediaPlan>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

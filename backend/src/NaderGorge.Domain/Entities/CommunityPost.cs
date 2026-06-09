using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class CommunityPost : BaseEntity
{
    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public CommunityPostStatus Status { get; set; } = CommunityPostStatus.Pending;

    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }

    public ICollection<CommunityPostComment> Comments { get; set; } = new List<CommunityPostComment>();
    public ICollection<CommunityPostLike> Likes { get; set; } = new List<CommunityPostLike>();

    public bool IsPoll { get; set; }
    public ICollection<CommunityPostPollOption> PollOptions { get; set; } = new List<CommunityPostPollOption>();
    public ICollection<CommunityPostPollVote> PollVotes { get; set; } = new List<CommunityPostPollVote>();
}

public class CommunityPostComment : BaseEntity
{
    public Guid PostId { get; set; }
    public CommunityPost Post { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public CommunityCommentStatus Status { get; set; } = CommunityCommentStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}

public class CommunityPostLike : BaseEntity
{
    public Guid PostId { get; set; }
    public CommunityPost Post { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

public class CommunityPostPollOption : BaseEntity
{
    public Guid PostId { get; set; }
    public CommunityPost Post { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
}

public class CommunityPostPollVote : BaseEntity
{
    public Guid PostId { get; set; }
    public CommunityPost Post { get; set; } = null!;

    public Guid PollOptionId { get; set; }
    public CommunityPostPollOption PollOption { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}

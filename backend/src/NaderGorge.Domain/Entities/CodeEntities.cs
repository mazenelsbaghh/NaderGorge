using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class CodeGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int TotalCodes { get; set; }
    public Guid? PackageId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    // Navigation
    public ICollection<AccessCode> AccessCodes { get; set; } = new List<AccessCode>();
}

public class AccessCode : BaseEntity
{
    public string CodeHash { get; set; } = string.Empty;
    public string CodePlaintext { get; set; } = string.Empty; // stored only during generation, cleared after export
    public Guid CodeGroupId { get; set; }
    public CodeGroup CodeGroup { get; set; } = null!;

    public bool IsConsumed { get; set; } = false;
    public Guid? ConsumedByUserId { get; set; }
    public User? ConsumedByUser { get; set; }
    public DateTime? ConsumedAt { get; set; }
}

public class StudentAccessGrant : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid? PackageId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid AccessCodeId { get; set; }
    public AccessCode AccessCode { get; set; } = null!;

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}

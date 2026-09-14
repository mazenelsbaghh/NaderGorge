using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class SharedTeacherPackage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public bool IsPublished { get; set; }
    public EducationStage? EducationStage { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableUntil { get; set; }
    public SharedPackageDistributionMode DistributionMode { get; set; } = SharedPackageDistributionMode.Percentage;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public ICollection<SharedTeacherPackageTeacher> Teachers { get; set; } = new List<SharedTeacherPackageTeacher>();
    public ICollection<SharedTeacherPackageItem> Items { get; set; } = new List<SharedTeacherPackageItem>();
}

public class SharedTeacherPackageTeacher : BaseEntity
{
    public Guid SharedTeacherPackageId { get; set; }
    public SharedTeacherPackage SharedTeacherPackage { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public TeacherAllocationMode AllocationMode { get; set; } = TeacherAllocationMode.Percentage;
    public decimal AllocationValue { get; set; }
    public int DisplayOrder { get; set; }
}

public class SharedTeacherPackageItem : BaseEntity
{
    public Guid SharedTeacherPackageId { get; set; }
    public SharedTeacherPackage SharedTeacherPackage { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;
    public SalesTargetType ContentType { get; set; }
    public Guid ContentId { get; set; }
    public decimal Price { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public bool IsIncluded { get; set; } = true;
}

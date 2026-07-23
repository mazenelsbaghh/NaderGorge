using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class SalesRule : BaseEntity
{
    public SalesTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string? GradeLevel { get; set; }
    public Guid? VideoTypeId { get; set; }
    public VideoType? VideoType { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class DiscountStackingPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public StackingMode Mode { get; set; } = StackingMode.SingleOnly;
    public decimal? MaxDiscountPercentage { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public string PriorityJson { get; set; } = "[]";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}

public class SalesCoupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public SalesTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public SalesOwnerType OwnerType { get; set; } = SalesOwnerType.Platform;
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public Guid? StackingPolicyId { get; set; }
    public DiscountStackingPolicy? StackingPolicy { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? GlobalUsageLimit { get; set; }
    public int? PerStudentUsageLimit { get; set; }
    public int UsedCount { get; set; }
    public SalesStatus Status { get; set; } = SalesStatus.Draft;
    public string? DisableReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<SalesCouponUsage> Usages { get; set; } = new List<SalesCouponUsage>();
}

public class SalesCouponUsage : BaseEntity
{
    public Guid CouponId { get; set; }
    public SalesCoupon Coupon { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public Guid PurchaseOperationId { get; set; }
    public SalesTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class PrintableCodeBatch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public PrintableCodeBehavior Behavior { get; set; } = PrintableCodeBehavior.Discount;
    public DiscountType? DiscountType { get; set; }
    public decimal? DiscountValue { get; set; }
    public decimal? CreditAmount { get; set; }
    public SalesTargetType TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public SalesOwnerType OwnerType { get; set; } = SalesOwnerType.Platform;
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public Guid? TemplateId { get; set; }
    public PrintableCodeTemplate? Template { get; set; }
    public Guid? StackingPolicyId { get; set; }
    public DiscountStackingPolicy? StackingPolicy { get; set; }
    public int TotalCodes { get; set; }
    public int UsedCount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public SalesStatus Status { get; set; } = SalesStatus.Draft;
    public string? DisableReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<PrintableSalesCode> Codes { get; set; } = new List<PrintableSalesCode>();
}

public class PrintableSalesCode : BaseEntity
{
    public Guid BatchId { get; set; }
    public PrintableCodeBatch Batch { get; set; } = null!;
    public string CodeHash { get; set; } = string.Empty;
    public string? CodePlaintext { get; set; }
    public long SerialNumber { get; set; }
    public string QrPayload { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public int UsageLimit { get; set; } = 1;
    public Guid? ConsumedByUserId { get; set; }
    public User? ConsumedByUser { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public SalesStatus Status { get; set; } = SalesStatus.Active;
    public ICollection<PrintableCodeRedemption> Redemptions { get; set; } = new List<PrintableCodeRedemption>();
}

public class PrintableCodeRedemption : BaseEntity
{
    public Guid PrintableCodeId { get; set; }
    public PrintableSalesCode PrintableCode { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public Guid RequestId { get; set; }
    public Guid? PurchaseOperationId { get; set; }
    public SalesTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public decimal AppliedAmount { get; set; }
}

public class PrintableCodeTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BackgroundImageUrl { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<PrintableCodeBatch> Batches { get; set; } = new List<PrintableCodeBatch>();
}

public class PublicExamProduct : BaseEntity
{
    public Guid ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public string Slug { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public bool IsPaid { get; set; }
    public decimal Price { get; set; }
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string? GradeLevel { get; set; }
    public bool IsPlatformWide { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableUntil { get; set; }
    public DateTime? DisabledAt { get; set; }
    public Guid? DisabledByUserId { get; set; }
    public User? DisabledByUser { get; set; }
    public string? DisableReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}

public class SalesFinancialEffect : BaseEntity
{
    public Guid PurchaseOperationId { get; set; }
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public SalesTargetType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CouponDiscountAmount { get; set; }
    public decimal PrintableCodeDiscountAmount { get; set; }
    public decimal PromotionalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public Guid? TeacherId { get; set; }
    public TeacherProfile? Teacher { get; set; }
    public decimal TeacherShareImpact { get; set; }
    public decimal PlatformShareImpact { get; set; }
    public string DetailsJson { get; set; } = "{}";
}

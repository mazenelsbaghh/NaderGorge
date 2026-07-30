using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Sales;

public sealed record SalesRuleRequest(
    SalesTargetType TargetType,
    Guid? TargetId,
    Guid? TeacherId,
    Guid? SubjectId,
    string? GradeLevel,
    Guid? VideoTypeId,
    bool IsActive);

public sealed record SalesCouponRequest(
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    SalesTargetType TargetType,
    Guid? TargetId,
    SalesOwnerType OwnerType,
    Guid? TeacherId,
    Guid? StackingPolicyId,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    int? GlobalUsageLimit,
    int? PerStudentUsageLimit,
    SalesStatus Status,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null);

public sealed record StackingPolicyRequest(
    string Name,
    StackingMode Mode,
    decimal? MaxDiscountPercentage,
    decimal? MaxDiscountAmount,
    string PriorityJson,
    bool IsDefault,
    bool IsActive);

public sealed record PrintableTemplateRequest(
    Guid? Id,
    string Name,
    decimal WidthMm,
    decimal HeightMm,
    string? BackgroundColor,
    string? BackgroundImageUrl,
    string LayoutJson,
    bool IsActive);

public sealed record PrintableBatchRequest(
    string Name,
    PrintableCodeBehavior Behavior,
    DiscountType? DiscountType,
    decimal? DiscountValue,
    decimal? CreditAmount,
    SalesTargetType TargetType,
    Guid? TargetId,
    SalesOwnerType OwnerType,
    Guid? TeacherId,
    Guid? TemplateId,
    Guid? StackingPolicyId,
    int TotalCodes,
    int UsageLimit,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    SalesStatus Status,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null);

public sealed record PublicExamProductRequest(
    Guid ExamId,
    string Slug,
    bool IsPublished,
    bool IsPaid,
    decimal Price,
    Guid? TeacherId,
    Guid? SubjectId,
    string? GradeLevel,
    bool IsPlatformWide,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null);

public sealed record CreatePublicExamRequest(
    string Title,
    string? Description,
    string Slug,
    Guid? TeacherId,
    Guid SubjectId,
    string? GradeLevel,
    bool IsPublished,
    bool IsPaid,
    decimal Price,
    decimal PassingScore,
    decimal TotalScore,
    int? DurationMinutes,
    bool IsRandomized,
    DateTime? AvailableFrom,
    DateTime? AvailableUntil,
    IReadOnlyList<AcademicScopeDto>? AcademicScopes = null);

public sealed record DisableRequest(string? Reason);

public sealed record SalesRuleDto(Guid Id, SalesTargetType TargetType, Guid? TargetId, Guid? TeacherId, Guid? SubjectId, string? GradeLevel, Guid? VideoTypeId, bool IsActive);
public sealed record SalesCouponUsageDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    SalesTargetType TargetType,
    Guid TargetId,
    decimal GrossAmount,
    decimal DiscountAmount,
    DateTime CreatedAt);

public sealed record SalesCouponDto(
    Guid Id,
    string Code,
    string Name,
    DiscountType DiscountType,
    decimal DiscountValue,
    SalesTargetType TargetType,
    Guid? TargetId,
    SalesOwnerType OwnerType,
    Guid? TeacherId,
    SalesStatus Status,
    int UsedCount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    Guid? StackingPolicyId,
    int? GlobalUsageLimit,
    int? PerStudentUsageLimit,
    string? DisableReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<SalesCouponUsageDto> RecentUsages);
public sealed record StackingPolicyDto(Guid Id, string Name, StackingMode Mode, decimal? MaxDiscountPercentage, decimal? MaxDiscountAmount, bool IsDefault, bool IsActive);
public sealed record PrintableTemplateDto(Guid Id, string Name, decimal WidthMm, decimal HeightMm, string? BackgroundColor, string? BackgroundImageUrl, string LayoutJson, bool IsActive);
public sealed record PrintableCodeDto(Guid Id, string Code, long SerialNumber, string QrPayload, SalesStatus Status);
public sealed record PrintableBatchDto(Guid Id, string Name, PrintableCodeBehavior Behavior, SalesTargetType TargetType, Guid? TargetId, SalesOwnerType OwnerType, Guid? TeacherId, int TotalCodes, int UsedCount, SalesStatus Status, IReadOnlyList<PrintableCodeDto> SampleCodes);
public sealed record PublicExamProductDto(Guid Id, Guid ExamId, string ExamTitle, string Slug, bool IsPublished, bool IsPaid, decimal Price, Guid? TeacherId, Guid? SubjectId, string? GradeLevel, bool IsPlatformWide, DateTime? AvailableFrom, DateTime? AvailableUntil, DateTime? DisabledAt, IReadOnlyList<AcademicScopeSummaryDto>? AcademicScopes = null);

using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Gifts.Models;

public sealed record IssueGiftRequest(
    Guid RequestId,
    GiftTargetType TargetType,
    Guid? TargetId,
    Guid? TeacherId,
    decimal? Amount,
    DateTime? ExpiresAt,
    int? MaxUses,
    IReadOnlyCollection<Guid> StudentIds,
    string Reason);

public sealed record GiftRecipientResultDto(
    Guid StudentId,
    string StudentName,
    GiftRecipientStatus Status,
    string OutcomeCode,
    string? OutcomeMessage,
    int UsesConsumed,
    int? MaxUses);

public sealed record IssueGiftResultDto(
    Guid Id,
    Guid RequestId,
    GiftTargetType TargetType,
    GiftIssuanceStatus Status,
    string TargetName,
    decimal? Amount,
    DateTime? ExpiresAt,
    int? MaxUses,
    string Reason,
    DateTime IssuedAt,
    bool IsReplay,
    IReadOnlyList<AcademicScopeSummaryDto>? AcademicScopes,
    IReadOnlyList<GiftRecipientResultDto> Recipients);

public sealed record GiftLookupDto(Guid Id, string Name, string? Context = null, IReadOnlyList<AcademicScopeSummaryDto>? AcademicScopes = null);

public sealed record GiftListItemDto(
    Guid Id,
    GiftTargetType TargetType,
    string TargetName,
    GiftIssuanceStatus Status,
    string IssuerName,
    int RecipientCount,
    int SuccessfulCount,
    decimal? OriginalValue,
    decimal? AvailableValue,
    DateTime? ExpiresAt,
    DateTime IssuedAt);

public sealed record GiftPageDto(
    IReadOnlyList<GiftListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GiftDetailsDto(
    Guid Id,
    Guid RequestId,
    GiftTargetType TargetType,
    string TargetName,
    GiftIssuanceStatus Status,
    string IssuerName,
    string Reason,
    decimal? Amount,
    decimal AvailableAmount,
    decimal ConsumedAmount,
    decimal ExpiredAmount,
    decimal RevokedAmount,
    DateTime? ExpiresAt,
    int? MaxUses,
    DateTime IssuedAt,
    IReadOnlyList<AcademicScopeSummaryDto>? AcademicScopes,
    IReadOnlyList<GiftRecipientResultDto> Recipients);

public sealed record RevokeGiftResultDto(Guid Id, bool Changed, GiftIssuanceStatus Status, decimal RevokedAmount);

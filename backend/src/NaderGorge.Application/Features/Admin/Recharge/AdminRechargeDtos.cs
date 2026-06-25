using System;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Recharge;

public record AdminRechargeRequestDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentPhoneNumber { get; set; } = string.Empty;
    public Guid WalletId { get; set; }
    public string WalletLabel { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string? ScreenshotUrl { get; set; }
    public RechargeRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByUserName { get; set; }
    public string? RejectionReason { get; set; }
    public Guid? MatchedSmsLogId { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }
}

public record AdminIncomingSmsLogDto
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public string WalletLabel { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public decimal? ParsedAmount { get; set; }
    public string? ParsedSenderPhone { get; set; }
    public bool IsMatched { get; set; }
    public Guid? MatchedRechargeRequestId { get; set; }
    public string DeduplicationHash { get; set; } = string.Empty;
}

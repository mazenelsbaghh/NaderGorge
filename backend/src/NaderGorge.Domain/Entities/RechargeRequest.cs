using System;
using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class RechargeRequest : BaseEntity
{
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public Guid WalletId { get; set; }
    public virtual DigitalWallet Wallet { get; set; } = null!;

    public decimal Amount { get; set; }
    /// <summary>When set, this recharge is available only for purchases from this teacher.</summary>
    public Guid? TeacherId { get; set; }
    public virtual TeacherProfile? Teacher { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string? OriginalSenderPhoneNumber { get; set; }
    public bool RequiresSenderPhoneConfirmation { get; set; }
    public DateTime? SenderPhoneConfirmedAt { get; set; }
    public string? ScreenshotUrl { get; set; }
    
    public RechargeRequestStatus Status { get; set; } = RechargeRequestStatus.Pending;

    public DateTime? ResolvedAt { get; set; }
    
    public Guid? ResolvedByUserId { get; set; }
    public virtual User? ResolvedByUser { get; set; }

    public string? RejectionReason { get; set; }

    public Guid? MatchedSmsLogId { get; set; }
    public virtual IncomingSmsLog? MatchedSmsLog { get; set; }

    public DateTime? ReservationExpiresAt { get; set; }
}

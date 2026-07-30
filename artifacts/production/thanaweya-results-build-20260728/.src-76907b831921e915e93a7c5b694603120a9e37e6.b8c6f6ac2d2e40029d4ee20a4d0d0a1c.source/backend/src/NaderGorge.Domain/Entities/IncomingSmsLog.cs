using System;
using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class IncomingSmsLog : BaseEntity
{
    public Guid WalletId { get; set; }
    public virtual DigitalWallet Wallet { get; set; } = null!;

    public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    
    public DateTime ReceivedAt { get; set; }

    public decimal? ParsedAmount { get; set; }
    public string? ParsedSenderPhone { get; set; }

    public bool IsMatched { get; set; } = false;
    
    public Guid? MatchedRechargeRequestId { get; set; }
    public virtual RechargeRequest? MatchedRechargeRequest { get; set; }

    public string DeduplicationHash { get; set; } = string.Empty;
}

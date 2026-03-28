using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class StudentBalance : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public decimal CurrentBalance { get; set; } = 0m;

    // Navigation
    public ICollection<BalanceTransaction> Transactions { get; set; } = new List<BalanceTransaction>();
}

public class BalanceTransaction : BaseEntity
{
    public Guid StudentBalanceId { get; set; }
    public StudentBalance StudentBalance { get; set; } = null!;

    /// <summary>Positive = credit, Negative = debit</summary>
    public decimal Amount { get; set; }

    /// <summary>Balance snapshot after this transaction</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>CodeRedemption, ContentPurchase, AdminAdjustment</summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>Optional FK to AccessCode or StudentAccessGrant</summary>
    public Guid? ReferenceId { get; set; }

    public string Description { get; set; } = string.Empty;
}

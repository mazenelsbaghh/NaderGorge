using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Services;

/// <summary>
/// Manages student balance operations: credit from code redemption, debit for purchases.
/// All balance mutations are atomic and guarded by the Balance >= 0 invariant.
/// </summary>
public class BalanceService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<BalanceService> _logger;

    public BalanceService(IAppDbContext db, ILogger<BalanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns existing StudentBalance or creates one with 0 balance.
    /// </summary>
    public async Task<StudentBalance> GetOrCreateBalance(Guid userId, CancellationToken ct = default)
    {
        var balance = await _db.StudentBalances
            .Include(b => b.Transactions)
            .FirstOrDefaultAsync(b => b.UserId == userId, ct);

        if (balance != null) return balance;

        balance = new StudentBalance
        {
            UserId = userId,
            CurrentBalance = 0m
        };
        _db.StudentBalances.Add(balance);
        await _db.SaveChangesAsync(ct);

        return balance;
    }

    /// <summary>
    /// Add credit to student balance (e.g., from code redemption).
    /// </summary>
    public async Task<BalanceTransaction> AddCredit(
        Guid userId,
        decimal amount,
        string description,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("Credit amount must be positive", nameof(amount));

        var balance = await GetOrCreateBalance(userId, ct);
        var now = DateTime.UtcNow;
        var affectedRows = await _db.StudentBalances
            .Where(b => b.Id == balance.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.CurrentBalance, b => b.CurrentBalance + amount)
                .SetProperty(b => b.UpdatedAt, now), ct);

        if (affectedRows != 1)
            throw new InvalidOperationException("Unable to update student balance.");

        await _db.Entry(balance).ReloadAsync(ct);

        var tx = new BalanceTransaction
        {
            StudentBalanceId = balance.Id,
            Amount = amount,
            BalanceAfter = balance.CurrentBalance,
            TransactionType = "CodeRedemption",
            ReferenceId = referenceId,
            Description = description
        };

        _db.BalanceTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Added {Amount} credit to user {UserId}. New Balance: {BalanceAfter}. Reason: {Description}",
            amount, userId, balance.CurrentBalance, description);

        return tx;
    }

    /// <summary>
    /// Deduct balance for a purchase. Throws if insufficient balance.
    /// </summary>
    public async Task<BalanceTransaction> DeductBalance(
        Guid userId,
        decimal amount,
        string description,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("Deduction amount must be positive", nameof(amount));

        var balance = await GetOrCreateBalance(userId, ct);

        var now = DateTime.UtcNow;
        var affectedRows = await _db.StudentBalances
            .Where(b => b.Id == balance.Id && b.CurrentBalance >= amount)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(b => b.CurrentBalance, b => b.CurrentBalance - amount)
                .SetProperty(b => b.UpdatedAt, now), ct);

        if (affectedRows != 1)
            throw new InvalidOperationException($"Insufficient balance. Current: {balance.CurrentBalance}, Required: {amount}");

        await _db.Entry(balance).ReloadAsync(ct);

        var tx = new BalanceTransaction
        {
            StudentBalanceId = balance.Id,
            Amount = -amount, // Negative for debi
            BalanceAfter = balance.CurrentBalance,
            TransactionType = "ContentPurchase",
            ReferenceId = referenceId,
            Description = description
        };

        _db.BalanceTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deducted {Amount} from user {UserId}. New Balance: {BalanceAfter}. Reason: {Description}",
            amount, userId, balance.CurrentBalance, description);

        return tx;
    }

    /// <summary>
    /// Get current balance and recent transactions.
    /// </summary>
    public async Task<(decimal Balance, List<BalanceTransaction> Transactions)> GetBalanceInfo(
        Guid userId,
        int recentCount = 20,
        CancellationToken ct = default)
    {
        var balance = await GetOrCreateBalance(userId, ct);

        var transactions = await _db.BalanceTransactions
            .Where(t => t.StudentBalanceId == balance.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(recentCount)
            .ToListAsync(ct);

        return (balance.CurrentBalance, transactions);
    }
}

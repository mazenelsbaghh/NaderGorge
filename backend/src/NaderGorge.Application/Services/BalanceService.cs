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

        var transaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null
            ? null
            : await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var balance = await GetOrCreateBalance(userId, ct);
            var now = DateTime.UtcNow;
            int affectedRows;
            if (_db is DbContext efDb2 && efDb2.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                balance.CurrentBalance += amount;
                balance.UpdatedAt = now;
                _db.StudentBalances.Update(balance);
                await _db.SaveChangesAsync(ct);
                affectedRows = 1;
            }
            else
            {
                affectedRows = await _db.StudentBalances
                    .Where(b => b.Id == balance.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.CurrentBalance, b => b.CurrentBalance + amount)
                        .SetProperty(b => b.UpdatedAt, now), ct);
            }

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

            var outboxEvent = new OutboxEvent
            {
                Type = "BalanceChanged",
                TargetUserId = userId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    newBalance = balance.CurrentBalance,
                    formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
                })
            };
            _db.OutboxEvents.Add(outboxEvent);

            await _db.SaveChangesAsync(ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            _logger.LogInformation("Added {Amount} credit to user {UserId}. New Balance: {BalanceAfter}. Reason: {Description}",
                amount, userId, balance.CurrentBalance, description);

            return tx;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
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

        var transaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null
            ? null
            : await _db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var balance = await GetOrCreateBalance(userId, ct);

            var now = DateTime.UtcNow;
            int affectedRows;
            if (_db is DbContext efDb2 && efDb2.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                if (balance.CurrentBalance >= amount)
                {
                    balance.CurrentBalance -= amount;
                    balance.UpdatedAt = now;
                    _db.StudentBalances.Update(balance);
                    await _db.SaveChangesAsync(ct);
                    affectedRows = 1;
                }
                else
                {
                    affectedRows = 0;
                }
            }
            else
            {
                affectedRows = await _db.StudentBalances
                    .Where(b => b.Id == balance.Id && b.CurrentBalance >= amount)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.CurrentBalance, b => b.CurrentBalance - amount)
                        .SetProperty(b => b.UpdatedAt, now), ct);
            }

            if (affectedRows != 1)
                throw new InvalidOperationException($"Insufficient balance. Current: {balance.CurrentBalance}, Required: {amount}");

            await _db.Entry(balance).ReloadAsync(ct);

            var tx = new BalanceTransaction
            {
                StudentBalanceId = balance.Id,
                Amount = -amount, // Negative for debit
                BalanceAfter = balance.CurrentBalance,
                TransactionType = "ContentPurchase",
                ReferenceId = referenceId,
                Description = description
            };

            _db.BalanceTransactions.Add(tx);

            var outboxEvent = new OutboxEvent
            {
                Type = "BalanceChanged",
                TargetUserId = userId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    newBalance = balance.CurrentBalance,
                    formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
                })
            };
            _db.OutboxEvents.Add(outboxEvent);

            await _db.SaveChangesAsync(ct);
            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            _logger.LogInformation("Deducted {Amount} from user {UserId}. New Balance: {BalanceAfter}. Reason: {Description}",
                amount, userId, balance.CurrentBalance, description);

            return tx;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
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

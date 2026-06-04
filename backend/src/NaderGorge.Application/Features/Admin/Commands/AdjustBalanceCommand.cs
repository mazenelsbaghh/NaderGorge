using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AdjustBalanceCommand(Guid StudentId, decimal Amount, string Reason, Guid AdminId) : IRequest<ApiResponse>;

public class AdjustBalanceCommandHandler : IRequestHandler<AdjustBalanceCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AdjustBalanceCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(AdjustBalanceCommand request, CancellationToken ct)
    {
        var balance = await _db.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.StudentId, ct);

        if (balance == null)
        {
            // Create balance record if it doesn't exist
            balance = new StudentBalance
            {
                UserId = request.StudentId,
                CurrentBalance = 0m
            };
            _db.StudentBalances.Add(balance);
        }

        var oldBalance = balance.CurrentBalance;
        balance.CurrentBalance += request.Amount;
        balance.UpdatedAt = DateTime.UtcNow;

        var transaction = new BalanceTransaction
        {
            StudentBalanceId = balance.Id,
            Amount = request.Amount,
            BalanceAfter = balance.CurrentBalance,
            TransactionType = "AdminAdjustment",
            Description = request.Reason
        };
        _db.BalanceTransactions.Add(transaction);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AdjustBalance",
            EntityType = "User",
            EntityId = request.StudentId,
            PerformedByUserId = request.AdminId,
            NewValues = $"Balance adjusted from {oldBalance} to {balance.CurrentBalance} ({(request.Amount >= 0 ? "+" : "")}{request.Amount}). Reason: {request.Reason}",
            IpAddress = "System"
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Balance updated: {oldBalance} → {balance.CurrentBalance}");
    }
}

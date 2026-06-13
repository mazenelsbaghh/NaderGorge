using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CancelPackageGrantCommand(Guid AccessGrantId, bool RefundBalance, Guid AdminId, string? Reason = null) : IRequest<ApiResponse>;

public class CancelPackageGrantCommandHandler : IRequestHandler<CancelPackageGrantCommand, ApiResponse>
{
    private readonly IAppDbContext _context;

    public CancelPackageGrantCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse> Handle(CancelPackageGrantCommand request, CancellationToken cancellationToken)
    {
        var grant = await _context.StudentAccessGrants
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == request.AccessGrantId, cancellationToken);

        if (grant == null) return ApiResponse.Fail("Access grant not found.");
        if (!grant.IsActive) return ApiResponse.Fail("Subscription is already inactive/canceled.");

        grant.IsActive = false;
        grant.CancelledByUserId = request.AdminId;
        grant.CancelledAt = DateTime.UtcNow;
        grant.CancellationReason = request.Reason;

        decimal refundedAmount = 0m;
        string? packageName = null;

        if (grant.PackageId.HasValue)
        {
            var package = await _context.Packages.FindAsync(new object[] { grant.PackageId.Value }, cancellationToken);
            if (package != null)
            {
                packageName = package.Name;
                if (request.RefundBalance && package.Price > 0m)
                {
                    refundedAmount = package.Price;
                    var balance = await _context.StudentBalances
                        .FirstOrDefaultAsync(b => b.UserId == grant.UserId, cancellationToken);

                    if (balance == null)
                    {
                        balance = new StudentBalance
                        {
                            Id = Guid.NewGuid(),
                            UserId = grant.UserId,
                            CurrentBalance = 0m
                        };
                        _context.StudentBalances.Add(balance);
                    }

                    balance.CurrentBalance += refundedAmount;
                    balance.UpdatedAt = DateTime.UtcNow;

                    var transaction = new BalanceTransaction
                    {
                        Id = Guid.NewGuid(),
                        StudentBalanceId = balance.Id,
                        Amount = refundedAmount,
                        BalanceAfter = balance.CurrentBalance,
                        TransactionType = "Refund",
                        ReferenceId = grant.PackageId.Value,
                        Description = $"إرجاع رصيد باقة {package.Name} بعد إلغاء الإدارة",
                        CreatedAt = DateTime.UtcNow,
                        PerformedByUserId = request.AdminId
                    };
                    _context.BalanceTransactions.Add(transaction);
                }
            }
        }

        var audit = new AuditLog
        {
            EntityType = "StudentAccessGrant",
            EntityId = grant.Id,
            Action = "CANCEL_PACKAGE_GRANT",
            PerformedByUserId = request.AdminId,
            OldValues = JsonSerializer.Serialize(new { isActive = true }),
            NewValues = JsonSerializer.Serialize(new { isActive = false, refundBalance = request.RefundBalance, refundedAmount })
        };
        _context.AuditLogs.Add(audit);

        if (refundedAmount > 0m)
        {
            var balance = await _context.StudentBalances.FirstOrDefaultAsync(b => b.UserId == grant.UserId, cancellationToken);
            if (balance != null)
            {
                var outboxEvent = new OutboxEvent
                {
                    Type = "BalanceChanged",
                    TargetUserId = grant.UserId.ToString(),
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        newBalance = balance.CurrentBalance,
                        formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
                    })
                };
                _context.OutboxEvents.Add(outboxEvent);
            }
        }

        var accessRevokedEvent = new OutboxEvent
        {
            Type = "PackageAccessRevoked",
            TargetUserId = grant.UserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new
            {
                packageId = grant.PackageId,
                packageName = packageName,
                userId = grant.UserId
            })
        };
        _context.OutboxEvents.Add(accessRevokedEvent);

        await _context.SaveChangesAsync(cancellationToken);

        var successMessage = refundedAmount > 0m
            ? $"تم إلغاء باقة {packageName} بنجاح وإرجاع {refundedAmount} ج.م إلى رصيد الطالب."
            : $"تم إلغاء باقة {packageName} بنجاح دون إرجاع رصيد.";

        return ApiResponse.Ok(successMessage);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AdjustBalanceCommand(
    Guid StudentId,
    decimal Amount,
    string Reason,
    Guid AdminId,
    string? Scope = null,
    string? Operation = null,
    Guid? TeacherId = null) : IRequest<ApiResponse>;

public class AdjustBalanceCommandHandler : IRequestHandler<AdjustBalanceCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public AdjustBalanceCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(AdjustBalanceCommand request, CancellationToken ct)
    {
        var absoluteAmount = Math.Abs(request.Amount);
        if (absoluteAmount == 0)
            return ApiResponse.Fail("Adjustment amount must not be zero.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse.Fail("Adjustment reason is required.");

        if (absoluteAmount > 100_000)
            return ApiResponse.Fail("Adjustment amount exceeds the allowed limit.");

        var operation = (request.Operation ?? (request.Amount < 0 ? "debit" : "credit")).Trim().ToLowerInvariant();
        if (operation is not ("credit" or "debit"))
            return ApiResponse.Fail("Adjustment operation must be credit or debit.");

        var signedAmount = operation == "debit" ? -absoluteAmount : absoluteAmount;
        var scope = (request.Scope ?? "general").Trim().ToLowerInvariant();
        if (scope is "teacher" or "teacherbalance")
            return await AdjustTeacherBalanceAsync(request, absoluteAmount, signedAmount, ct);

        if (scope is not ("general" or "platform"))
            return ApiResponse.Fail("Adjustment scope must be general or teacher.");

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
        if (oldBalance + signedAmount < 0)
            return ApiResponse.Fail("Adjustment would make the student balance negative.");

        balance.CurrentBalance += signedAmount;
        balance.UpdatedAt = DateTime.UtcNow;

        var transaction = new BalanceTransaction
        {
            StudentBalanceId = balance.Id,
            Amount = signedAmount,
            BalanceAfter = balance.CurrentBalance,
            TransactionType = "AdminAdjustment",
            Description = request.Reason,
            PerformedByUserId = request.AdminId
        };
        _db.BalanceTransactions.Add(transaction);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AdjustBalance",
            EntityType = "User",
            EntityId = request.StudentId,
            PerformedByUserId = request.AdminId,
            NewValues = $"General balance adjusted from {oldBalance} to {balance.CurrentBalance} ({(signedAmount >= 0 ? "+" : "")}{signedAmount}). Reason: {request.Reason}",
            IpAddress = "System"
        });

        var outboxEvent = new OutboxEvent
        {
            Type = "BalanceChanged",
            TargetUserId = request.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                newBalance = balance.CurrentBalance,
                formattedBalance = $"{balance.CurrentBalance:F2} جنيها"
            })
        };
        _db.OutboxEvents.Add(outboxEvent);

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Balance updated: {oldBalance} → {balance.CurrentBalance}");
    }

    private async Task<ApiResponse> AdjustTeacherBalanceAsync(AdjustBalanceCommand request, decimal absoluteAmount, decimal signedAmount, CancellationToken ct)
    {
        if (!request.TeacherId.HasValue)
            return ApiResponse.Fail("Teacher is required for teacher balance adjustment.");

        var teacher = await _db.TeacherProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == request.TeacherId.Value, ct);
        if (teacher == null)
            return ApiResponse.Fail("Teacher was not found.");

        var now = DateTime.UtcNow;
        var allocations = await _db.PromotionalBalanceAllocations
            .Include(x => x.GiftRecipient)
            .ThenInclude(x => x.GiftIssuance)
            .Where(x => x.StudentId == request.StudentId &&
                        x.TeacherId == request.TeacherId.Value &&
                        x.AvailableAmount > 0 &&
                        (x.Status == PromotionalBalanceStatus.Active || x.Status == PromotionalBalanceStatus.PartiallyUsed) &&
                        (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderBy(x => x.ExpiresAt == null)
            .ThenBy(x => x.ExpiresAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var oldBalance = allocations.Sum(x => x.AvailableAmount);
        if (signedAmount < 0 && oldBalance < absoluteAmount)
            return ApiResponse.Fail("Teacher balance adjustment would make the balance negative.");

        if (signedAmount > 0)
        {
            var recipient = new GiftRecipient
            {
                StudentId = request.StudentId,
                Status = GiftRecipientStatus.Active,
                OutcomeCode = "ADMIN_ADJUSTMENT",
                OutcomeMessage = $"تمت إضافة رصيد مخصص للمدرس {teacher.User.FullName} من الإدارة"
            };
            var allocation = new PromotionalBalanceAllocation
            {
                StudentId = request.StudentId,
                TeacherId = request.TeacherId.Value,
                OriginalAmount = absoluteAmount,
                AvailableAmount = absoluteAmount,
                GiftRecipient = recipient,
                Status = PromotionalBalanceStatus.Active
            };
            var issuance = new GiftIssuance
            {
                RequestId = Guid.NewGuid(),
                TargetType = GiftTargetType.TeacherBalance,
                TeacherId = request.TeacherId.Value,
                Amount = absoluteAmount,
                Reason = request.Reason,
                IssuedByUserId = request.AdminId,
                Status = GiftIssuanceStatus.Active,
                Recipients = { recipient }
            };
            recipient.PromotionalBalanceAllocation = allocation;
            _db.GiftIssuances.Add(issuance);
        }
        else
        {
            var remaining = absoluteAmount;
            foreach (var allocation in allocations)
            {
                if (remaining <= 0)
                    break;

                var deduction = Math.Min(allocation.AvailableAmount, remaining);
                allocation.AvailableAmount -= deduction;
                allocation.RevokedAmount += deduction;
                allocation.UpdatedAt = now;
                allocation.Status = allocation.AvailableAmount == 0
                    ? PromotionalBalanceStatus.Revoked
                    : PromotionalBalanceStatus.PartiallyUsed;
                allocation.GiftRecipient.Status = allocation.AvailableAmount == 0
                    ? GiftRecipientStatus.Revoked
                    : GiftRecipientStatus.PartiallyUsed;
                allocation.GiftRecipient.UpdatedAt = now;
                allocation.GiftRecipient.RevokedAt = allocation.AvailableAmount == 0 ? now : allocation.GiftRecipient.RevokedAt;
                allocation.GiftRecipient.RevokedByUserId = request.AdminId;
                allocation.GiftRecipient.RevocationReason = request.Reason;
                remaining -= deduction;
            }
        }

        var newBalance = oldBalance + signedAmount;
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "AdjustBalance",
            EntityType = "User",
            EntityId = request.StudentId,
            PerformedByUserId = request.AdminId,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                Scope = "TeacherBalance",
                request.TeacherId,
                TeacherName = teacher.User.FullName,
                OldBalance = oldBalance,
                NewBalance = newBalance,
                Amount = signedAmount,
                request.Reason
            }),
            IpAddress = "System"
        });

        _db.OutboxEvents.Add(new OutboxEvent
        {
            Type = "BalanceChanged",
            TargetUserId = request.StudentId.ToString(),
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                scopedTeacherId = request.TeacherId,
                scopedTeacherName = teacher.User.FullName,
                promotionalAmount = newBalance,
                formattedBalance = $"{newBalance:F2} جنيها"
            })
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Teacher balance updated: {oldBalance} → {newBalance}");
    }
}

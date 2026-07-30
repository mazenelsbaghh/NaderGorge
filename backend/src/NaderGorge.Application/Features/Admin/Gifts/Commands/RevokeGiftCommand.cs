using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Gifts.Commands;

public sealed record RevokeGiftCommand(Guid Id, string Reason, Guid RevokedByUserId) : IRequest<ApiResponse<RevokeGiftResultDto>>;

public sealed class RevokeGiftCommandValidator : AbstractValidator<RevokeGiftCommand>
{
    public RevokeGiftCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public sealed class RevokeGiftCommandHandler : IRequestHandler<RevokeGiftCommand, ApiResponse<RevokeGiftResultDto>>
{
    private readonly IAppDbContext _db;
    public RevokeGiftCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<RevokeGiftResultDto>> Handle(RevokeGiftCommand request, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var issuance = await _db.GiftIssuances
            .Include(x => x.Recipients).ThenInclude(x => x.AccessGrant)
            .Include(x => x.Recipients).ThenInclude(x => x.PromotionalBalanceAllocation)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (issuance == null)
            return ApiResponse<RevokeGiftResultDto>.Fail("الهدية غير موجودة.", ["NOT_FOUND"]);

        if (issuance.Status == GiftIssuanceStatus.Revoked)
            return ApiResponse<RevokeGiftResultDto>.Ok(new RevokeGiftResultDto(issuance.Id, false, issuance.Status, 0), "الهدية ملغاة بالفعل.");

        if (issuance.ExpiresAt.HasValue && issuance.ExpiresAt <= DateTime.UtcNow)
        {
            issuance.Status = GiftIssuanceStatus.Expired;
            issuance.UpdatedAt = DateTime.UtcNow;
            foreach (var recipient in issuance.Recipients.Where(x => x.Status is GiftRecipientStatus.Active or GiftRecipientStatus.PartiallyUsed))
                recipient.Status = GiftRecipientStatus.Expired;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ApiResponse<RevokeGiftResultDto>.Ok(new RevokeGiftResultDto(issuance.Id, false, issuance.Status, 0), "الهدية منتهية ولا يوجد متبقٍ قابل للإلغاء.");
        }

        var now = DateTime.UtcNow;
        var revokedAmount = 0m;
        var changed = false;
        foreach (var recipient in issuance.Recipients)
        {
            var recipientChanged = false;
            if (recipient.AccessGrant is { IsActive: true } grant)
            {
                grant.IsActive = false;
                grant.CancelledAt = now;
                grant.CancelledByUserId = request.RevokedByUserId;
                grant.CancellationReason = request.Reason.Trim();
                grant.UpdatedAt = now;
                changed = true;
                recipientChanged = true;
            }

            var allocation = recipient.PromotionalBalanceAllocation;
            if (allocation is { AvailableAmount: > 0 })
            {
                revokedAmount += allocation.AvailableAmount;
                allocation.RevokedAmount += allocation.AvailableAmount;
                allocation.AvailableAmount = 0;
                allocation.Status = PromotionalBalanceStatus.Revoked;
                allocation.UpdatedAt = now;
                changed = true;
                recipientChanged = true;
            }

            if (recipientChanged && recipient.Status is (GiftRecipientStatus.Active or GiftRecipientStatus.Granted or GiftRecipientStatus.PartiallyUsed))
            {
                recipient.Status = GiftRecipientStatus.Revoked;
                recipient.RevokedAt = now;
                recipient.RevokedByUserId = request.RevokedByUserId;
                recipient.RevocationReason = request.Reason.Trim();
                recipient.UpdatedAt = now;
            }
        }

        if (!changed)
            return ApiResponse<RevokeGiftResultDto>.Ok(new RevokeGiftResultDto(issuance.Id, false, issuance.Status, 0), "لا يوجد متبقٍ قابل للإلغاء.");

        issuance.Status = GiftIssuanceStatus.Revoked;
        issuance.UpdatedAt = now;
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "GiftRevoked",
            EntityType = nameof(GiftIssuance),
            EntityId = issuance.Id,
            PerformedByUserId = request.RevokedByUserId,
            OldValues = JsonSerializer.Serialize(new { status = "Active" }),
            NewValues = JsonSerializer.Serialize(new { status = issuance.Status, reason = request.Reason.Trim(), revokedAmount })
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ApiResponse<RevokeGiftResultDto>.Ok(new RevokeGiftResultDto(issuance.Id, true, issuance.Status, revokedAmount), "تم إلغاء المتبقي من الهدية.");
    }
}

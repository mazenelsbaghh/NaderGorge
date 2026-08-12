using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter;

public enum TeacherFinanceCommandStatus { Success, Invalid, NotFound, Conflict }
public sealed record TeacherFinanceCommandResult(TeacherFinanceCommandStatus Status, Guid? Id = null,
    DateTime? OccurredAt = null, bool AlreadyApplied = false, string? Message = null);

public sealed record TeacherAgreementTerms(TeacherAgreementScopeType ScopeType, Guid? ScopeId,
    TeacherAgreementTrigger Trigger, TeacherAgreementAllocationMode AllocationMode, decimal AllocationValue,
    TeacherPriceBasis PriceBasis, DateTime EffectiveFrom, DateTime? EffectiveTo, string Reason);
public sealed record CreateTeacherAgreementCommand(Guid ActorUserId, Guid TeacherId, TeacherAgreementTerms Terms)
    : IRequest<TeacherFinanceCommandResult>;
public sealed record ReplaceTeacherAgreementCommand(Guid ActorUserId, Guid AgreementId, TeacherAgreementTerms Terms)
    : IRequest<TeacherFinanceCommandResult>;
public sealed record SetCodeGroupFinancialTermsCommand(Guid ActorUserId, Guid CodeGroupId,
    TeacherAgreementTrigger Trigger, Guid? AgreementId, string? Recipient) : IRequest<TeacherFinanceCommandResult>;
public sealed record ConfirmCodeGroupDeliveryCommand(Guid ActorUserId, Guid CodeGroupId, string Recipient,
    string? AttachmentUrl, DateTime? DeliveredAt) : IRequest<TeacherFinanceCommandResult>;

public sealed class CreateTeacherAgreementCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateTeacherAgreementCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(CreateTeacherAgreementCommand command, CancellationToken ct)
    {
        var validation = await TeacherAgreementAuthority.ValidateAsync(db, command.TeacherId, command.Terms, null, ct);
        if (validation is not null) return validation;
        var agreement = TeacherAgreementAuthority.Create(command.TeacherId, command.ActorUserId, command.Terms);
        db.TeacherFinancialAgreements.Add(agreement);
        await db.SaveChangesAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, agreement.Id);
    }
}

public sealed class ReplaceTeacherAgreementCommandHandler(IAppDbContext db)
    : IRequestHandler<ReplaceTeacherAgreementCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(ReplaceTeacherAgreementCommand command, CancellationToken ct)
    {
        var current = await db.TeacherFinancialAgreements.FirstOrDefaultAsync(x => x.Id == command.AgreementId, ct);
        if (current is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "الاتفاق غير موجود");
        var validation = await TeacherAgreementAuthority.ValidateAsync(db, current.TeacherId, command.Terms, current.Id, ct);
        if (validation is not null) return validation;
        var now = DateTime.UtcNow;
        current.IsActive = false;
        current.EffectiveTo = current.EffectiveTo is null || current.EffectiveTo > now ? now : current.EffectiveTo;
        current.UpdatedAt = now;
        current.UpdatedByUserId = command.ActorUserId;
        db.TeacherFinancialAgreements.Add(TeacherAgreementAuthority.Create(current.TeacherId, command.ActorUserId, command.Terms));
        await db.SaveChangesAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }
}

internal static class TeacherAgreementAuthority
{
    public static TeacherFinancialAgreement Create(Guid teacherId, Guid actorUserId, TeacherAgreementTerms terms) => new()
    {
        Id = Guid.NewGuid(), TeacherId = teacherId, ScopeType = terms.ScopeType, ScopeId = terms.ScopeId,
        Trigger = terms.Trigger, AllocationMode = terms.AllocationMode, AllocationValue = terms.AllocationValue,
        PriceBasis = terms.PriceBasis, EffectiveFrom = terms.EffectiveFrom, EffectiveTo = terms.EffectiveTo,
        Reason = terms.Reason.Trim(), CreatedByUserId = actorUserId
    };

    public static async Task<TeacherFinanceCommandResult?> ValidateAsync(IAppDbContext db, Guid teacherId,
        TeacherAgreementTerms terms, Guid? ignoredId, CancellationToken ct)
    {
        if (teacherId == Guid.Empty || !await db.TeacherProfiles.AnyAsync(x => x.Id == teacherId, ct))
            return new(TeacherFinanceCommandStatus.NotFound, Message: "المدرس غير موجود");
        if (string.IsNullOrWhiteSpace(terms.Reason) || terms.AllocationValue < 0m ||
            (terms.AllocationMode == TeacherAgreementAllocationMode.Percentage && terms.AllocationValue > 100m) ||
            (terms.EffectiveTo.HasValue && terms.EffectiveTo < terms.EffectiveFrom) ||
            (terms.ScopeType == TeacherAgreementScopeType.Default && terms.ScopeId != null))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "بيانات الاتفاق غير صالحة");
        var overlaps = await db.TeacherFinancialAgreements.AnyAsync(x => x.Id != ignoredId && x.TeacherId == teacherId && x.IsActive
            && x.ScopeType == terms.ScopeType && x.ScopeId == terms.ScopeId && x.Trigger == terms.Trigger
            && x.EffectiveFrom <= (terms.EffectiveTo ?? DateTime.MaxValue)
            && (x.EffectiveTo == null || x.EffectiveTo >= terms.EffectiveFrom), ct);
        return overlaps ? new(TeacherFinanceCommandStatus.Conflict, Message: "يوجد اتفاق نشط متداخل لنفس النطاق والتوقيت") : null;
    }
}

public sealed class SetCodeGroupFinancialTermsCommandHandler(IAppDbContext db)
    : IRequestHandler<SetCodeGroupFinancialTermsCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(SetCodeGroupFinancialTermsCommand command, CancellationToken ct)
    {
        if (command.Trigger is not (TeacherAgreementTrigger.CodeDelivery or TeacherAgreementTrigger.CodeActivation))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "توقيت الحساب غير صالح");
        var group = await db.CodeGroups.FirstOrDefaultAsync(x => x.Id == command.CodeGroupId, ct);
        if (group is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "دفعة الأكواد غير موجودة");
        if (group.CodeType == CodeType.Balance) return new(TeacherFinanceCommandStatus.Invalid, Message: "أكواد الرصيد لا تنشئ استحقاق مدرس");
        if (group.AccountingRecordedAt is not null) return new(TeacherFinanceCommandStatus.Conflict, Message: "لا يمكن تغيير شروط دفعة تم احتسابها بالفعل");
        if (command.AgreementId.HasValue && (!group.TeacherId.HasValue || !await db.TeacherFinancialAgreements.AnyAsync(x =>
                x.Id == command.AgreementId && x.TeacherId == group.TeacherId && x.IsActive && x.Trigger == command.Trigger, ct)))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "الاتفاق المحدد لا يخص مدرس الدفعة أو توقيتها");
        var terms = await db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (terms is null) { terms = new CodeGroupFinancialTerms { Id = Guid.NewGuid(), CodeGroupId = command.CodeGroupId }; db.CodeGroupFinancialTerms.Add(terms); }
        terms.Trigger = command.Trigger; terms.AgreementId = command.AgreementId; terms.Recipient = command.Recipient?.Trim();
        terms.UpdatedByUserId = command.ActorUserId; terms.UpdatedAt = DateTime.UtcNow;
        group.AccountingTiming = command.Trigger == TeacherAgreementTrigger.CodeDelivery ? CodeAccountingTiming.Immediate : CodeAccountingTiming.OnActivation;
        await db.SaveChangesAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }
}

public sealed class ConfirmCodeGroupDeliveryCommandHandler(IAppDbContext db, CodeGroupFinancialAccountingService accounting)
    : IRequestHandler<ConfirmCodeGroupDeliveryCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(ConfirmCodeGroupDeliveryCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Recipient)) return new(TeacherFinanceCommandStatus.Invalid, Message: "يجب إدخال مستلم دفعة الأكواد");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var group = await db.CodeGroups.FirstOrDefaultAsync(x => x.Id == command.CodeGroupId, ct);
        if (group is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "دفعة الأكواد غير موجودة");
        if (group.CodeType == CodeType.Balance || !group.TeacherId.HasValue)
            return new(TeacherFinanceCommandStatus.Invalid, Message: "هذه الدفعة لا تحتوي على استحقاق مدرس للتأكيد");
        var terms = await db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (terms?.Trigger != TeacherAgreementTrigger.CodeDelivery)
            return new(TeacherFinanceCommandStatus.Conflict, Message: "هذه الدفعة مضبوطة للحساب عند تفعيل كل كود");
        var existing = await db.CodeGroupDeliveryConfirmations.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (existing is not null) { await transaction.CommitAsync(ct); return new(TeacherFinanceCommandStatus.Success, existing.Id, existing.ConfirmedAt, true); }
        var occurredAt = command.DeliveredAt?.ToUniversalTime() ?? DateTime.UtcNow;
        var confirmation = new CodeGroupDeliveryConfirmation { Id = Guid.NewGuid(), CodeGroupId = command.CodeGroupId,
            Recipient = command.Recipient.Trim(), AttachmentUrl = string.IsNullOrWhiteSpace(command.AttachmentUrl) ? null : command.AttachmentUrl.Trim(),
            ConfirmedByUserId = command.ActorUserId, ConfirmedAt = occurredAt, IdempotencyKey = $"code-delivery:{command.CodeGroupId}" };
        db.CodeGroupDeliveryConfirmations.Add(confirmation);
        await accounting.RecordDeliveryAsync(group, terms, occurredAt, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, confirmation.Id, confirmation.ConfirmedAt);
    }
}

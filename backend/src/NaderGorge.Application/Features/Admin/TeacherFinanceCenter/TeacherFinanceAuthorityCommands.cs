using System.Data;
using NaderGorge.Application.Interfaces.Finance;
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
    string? AttachmentUrl, DateTime? DeliveredAt, string QuoteKey, CodeCollectionInput? Payment = null) : IRequest<TeacherFinanceCommandResult>;

public sealed class CreateTeacherAgreementCommandHandler(IAppDbContext db)
    : IRequestHandler<CreateTeacherAgreementCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(CreateTeacherAgreementCommand command, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var validation = await TeacherAgreementAuthority.ValidateAsync(db, command.TeacherId, command.Terms, null, ct);
        if (validation is not null) return validation;
        await TeacherAgreementAuthority.ClosePreviousRulesAsync(db, command.TeacherId, command.ActorUserId, command.Terms, ct);
        var agreement = TeacherAgreementAuthority.Create(command.TeacherId, command.ActorUserId, command.Terms);
        db.TeacherFinancialAgreements.Add(agreement);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, agreement.Id);
    }
}

public sealed class ReplaceTeacherAgreementCommandHandler(IAppDbContext db)
    : IRequestHandler<ReplaceTeacherAgreementCommand, TeacherFinanceCommandResult>
{
    public async Task<TeacherFinanceCommandResult> Handle(ReplaceTeacherAgreementCommand command, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.TeacherFinancialAgreements.FirstOrDefaultAsync(x => x.Id == command.AgreementId, ct);
        if (current is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "الاتفاق غير موجود");
        var validation = await TeacherAgreementAuthority.ValidateAsync(db, current.TeacherId, command.Terms, current.Id, ct);
        if (validation is not null) return validation;
        var now = DateTime.UtcNow;
        if (command.Terms.EffectiveFrom <= current.EffectiveFrom) current.IsActive = false;
        else current.EffectiveTo = command.Terms.EffectiveFrom;
        await TeacherAgreementAuthority.ClosePreviousRulesAsync(db, current.TeacherId, command.ActorUserId, command.Terms, ct);
        current.UpdatedAt = now;
        current.UpdatedByUserId = command.ActorUserId;
        db.TeacherFinancialAgreements.Add(TeacherAgreementAuthority.Create(current.TeacherId, command.ActorUserId, command.Terms));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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

    // A unified rule explicitly replaces the older source-specific rules for this scope.
    // Recorded allocations keep their immutable agreement snapshots.
    public static async Task ClosePreviousRulesAsync(IAppDbContext db, Guid teacherId, Guid actorId,
        TeacherAgreementTerms terms, CancellationToken ct)
    {
        if (terms.Trigger != TeacherAgreementTrigger.AllSources) return;
        var previous = await db.TeacherFinancialAgreements.Where(x => x.TeacherId == teacherId && x.IsActive
            && x.ScopeType == terms.ScopeType && x.ScopeId == terms.ScopeId
            && (x.EffectiveTo == null || x.EffectiveTo > terms.EffectiveFrom)).ToListAsync(ct);
        foreach (var rule in previous)
        {
            if (rule.EffectiveFrom >= terms.EffectiveFrom) rule.IsActive = false;
            else rule.EffectiveTo = terms.EffectiveFrom;
            rule.UpdatedByUserId = actorId;
            rule.UpdatedAt = DateTime.UtcNow;
        }
    }

    public static async Task<TeacherFinanceCommandResult?> ValidateAsync(IAppDbContext db, Guid teacherId,
        TeacherAgreementTerms terms, Guid? ignoredId, CancellationToken ct)
    {
        if (teacherId == Guid.Empty || !await db.TeacherProfiles.AnyAsync(x => x.Id == teacherId, ct))
            return new(TeacherFinanceCommandStatus.NotFound, Message: "المدرس غير موجود");
        if (string.IsNullOrWhiteSpace(terms.Reason) || terms.AllocationValue < 0m ||
            (terms.AllocationMode == TeacherAgreementAllocationMode.Percentage && terms.AllocationValue > 100m) ||
            (terms.EffectiveTo.HasValue && terms.EffectiveTo < terms.EffectiveFrom) ||
            !Enum.IsDefined(terms.ScopeType) ||
            !Enum.IsDefined(terms.Trigger) || !Enum.IsDefined(terms.AllocationMode) || !Enum.IsDefined(terms.PriceBasis) ||
            terms.ScopeId == Guid.Empty ||
            (terms.ScopeType == TeacherAgreementScopeType.Default && terms.ScopeId != null) ||
            (terms.AllocationMode == TeacherAgreementAllocationMode.FixedPerBatch && terms.Trigger != TeacherAgreementTrigger.CodeDelivery))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "بيانات الاتفاق غير صالحة");
        if (terms.Trigger == TeacherAgreementTrigger.AllSources)
        {
            if (terms.EffectiveTo != null) return new(TeacherFinanceCommandStatus.Invalid, Message: "الاتفاق الموحد مستمر حتى تغييره باتفاق جديد");
            return null;
        }
        var overlaps = await db.TeacherFinancialAgreements.AnyAsync(x => x.Id != ignoredId && x.TeacherId == teacherId && x.IsActive
            && x.ScopeType == terms.ScopeType && x.ScopeId == terms.ScopeId && (x.Trigger == terms.Trigger || x.Trigger == TeacherAgreementTrigger.AllSources)
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
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (command.Trigger is not (TeacherAgreementTrigger.CodeDelivery or TeacherAgreementTrigger.CodeActivation))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "توقيت الحساب غير صالح");
        var group = await db.CodeGroups.FirstOrDefaultAsync(x => x.Id == command.CodeGroupId, ct);
        if (group is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "دفعة الأكواد غير موجودة");
        if (command.Trigger == TeacherAgreementTrigger.CodeDelivery && await db.TeacherProfiles.AnyAsync(x => x.Id == group.TeacherId && x.FinancePreset == TeacherFinancePreset.Nader, ct))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "أكواد نادر تُحسب عند الاستخدام فقط");
        if (group.CodeType == CodeType.Balance) return new(TeacherFinanceCommandStatus.Invalid, Message: "أكواد الرصيد لا تنشئ استحقاق مدرس");
        if (await CodeGroupAccountingGuard.HasStartedAsync(db, group, ct)) return new(TeacherFinanceCommandStatus.Conflict, Message: "لا يمكن تغيير شروط دفعة تم احتسابها بالفعل");
        if (command.AgreementId.HasValue && (!group.TeacherId.HasValue || !await db.TeacherFinancialAgreements.AnyAsync(x =>
                x.Id == command.AgreementId && x.TeacherId == group.TeacherId && x.IsActive && (x.Trigger == command.Trigger || x.Trigger == TeacherAgreementTrigger.AllSources), ct)))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "الاتفاق المحدد لا يخص مدرس الدفعة أو توقيتها");
        var terms = await db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (terms is null) { terms = new CodeGroupFinancialTerms { Id = Guid.NewGuid(), CodeGroupId = command.CodeGroupId }; db.CodeGroupFinancialTerms.Add(terms); }
        terms.Trigger = command.Trigger; terms.AgreementId = command.AgreementId; terms.Recipient = command.Recipient?.Trim();
        terms.UpdatedByUserId = command.ActorUserId; terms.UpdatedAt = DateTime.UtcNow;
        group.AccountingTiming = command.Trigger == TeacherAgreementTrigger.CodeDelivery ? CodeAccountingTiming.Immediate : CodeAccountingTiming.OnActivation;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }
}

public sealed class ConfirmCodeGroupDeliveryCommandHandler(IAppDbContext db, CodeGroupFinancialAccountingService accounting, IFinancialPostingService posting)
    : IRequestHandler<ConfirmCodeGroupDeliveryCommand, TeacherFinanceCommandResult>
{
    public Task<TeacherFinanceCommandResult> Handle(ConfirmCodeGroupDeliveryCommand command, CancellationToken ct) =>
        CodeFinanceTransaction.ExecuteAsync(db, token => HandleOnce(command, token), ct);

    private async Task<TeacherFinanceCommandResult> HandleOnce(ConfirmCodeGroupDeliveryCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Recipient)) return new(TeacherFinanceCommandStatus.Invalid, Message: "يجب إدخال مستلم دفعة الأكواد");
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var group = await db.CodeGroups.FirstOrDefaultAsync(x => x.Id == command.CodeGroupId, ct);
        if (group is null) return new(TeacherFinanceCommandStatus.NotFound, Message: "دفعة الأكواد غير موجودة");
        if (group.CodeType == CodeType.Balance || !group.TeacherId.HasValue)
            return new(TeacherFinanceCommandStatus.Invalid, Message: "هذه الدفعة لا تحتوي على استحقاق مدرس للتأكيد");
        if (await db.TeacherProfiles.AnyAsync(x => x.Id == group.TeacherId && x.FinancePreset == TeacherFinancePreset.Nader, ct))
            return new(TeacherFinanceCommandStatus.Invalid, Message: "أكواد نادر تُحسب عند الاستخدام فقط");
        var terms = await db.CodeGroupFinancialTerms.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (terms?.Trigger != TeacherAgreementTrigger.CodeDelivery)
            return new(TeacherFinanceCommandStatus.Conflict, Message: "هذه الدفعة مضبوطة للحساب عند تفعيل كل كود");
        var existing = await db.CodeGroupDeliveryConfirmations.FirstOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (existing is not null)
        {
            if (command.Payment is not null && !await db.CodeGroupDeliveryPayments.AnyAsync(x => x.DeliveryConfirmationId == existing.Id
                && x.IdempotencyKey == command.Payment.IdempotencyKey.Trim() && x.Amount == command.Payment.Amount
                && x.TreasuryAccountId == command.Payment.TreasuryAccountId && x.Reference == command.Payment.Reference.Trim(), ct))
                return new(TeacherFinanceCommandStatus.Conflict, Message: "الدفعة مؤكدة بالفعل. سجّل أي سداد إضافي من تسجيل المبلغ المستلم");
            await transaction.CommitAsync(ct);
            return new(TeacherFinanceCommandStatus.Success, existing.Id, existing.ConfirmedAt, true);
        }
        if (await CodeGroupAccountingGuard.HasStartedAsync(db, group, ct))
            return new(TeacherFinanceCommandStatus.Conflict, Message: "بدأ استخدام أو حساب هذه الدفعة؛ لا يمكن احتسابها مرة أخرى عند التسليم");
        var occurredAt = command.DeliveredAt?.ToUniversalTime() ?? DateTime.UtcNow;
        if (occurredAt > DateTime.UtcNow.AddMinutes(1) || occurredAt < group.CreatedAt)
            return new(TeacherFinanceCommandStatus.Invalid, Message: "تاريخ التسليم لازم يكون بعد إنشاء الدفعة وحتى الوقت الحالي");
        var quote = await CodeGroupFinanceQuote.CalculateAsync(db, group, terms, occurredAt, ct);
        if (quote.Key != command.QuoteKey) return new(TeacherFinanceCommandStatus.Conflict, Message: "الحساب تغير؛ راجع المعاينة الحالية قبل تأكيد التسليم");
        if (quote.PlatformShare < 0m) return new(TeacherFinanceCommandStatus.Invalid, Message: "نصيب المدرّس أكبر من قيمة الدفعة. راجع الاتفاق قبل التسليم");
        var collection = new CodeGroupCollectionService(db, posting);
        if (command.Payment is not null)
        {
            var error = await collection.ValidateAsync(command.Payment, quote.PlatformShare, ct);
            if (error != null) return new(TeacherFinanceCommandStatus.Invalid, Message: error);
            if (await db.CodeGroupDeliveryPayments.AnyAsync(x => x.IdempotencyKey == command.Payment.IdempotencyKey, ct))
                return new(TeacherFinanceCommandStatus.Conflict, Message: "مرجع العملية مستخدم في تحصيل آخر");
        }
        var confirmation = new CodeGroupDeliveryConfirmation { Id = Guid.NewGuid(), CodeGroupId = command.CodeGroupId,
            Recipient = command.Recipient.Trim(), AttachmentUrl = string.IsNullOrWhiteSpace(command.AttachmentUrl) ? null : command.AttachmentUrl.Trim(),
            PlatformAmountDue = quote.PlatformShare, TeacherRetainedAmount = quote.TeacherShare,
            ConfirmedByUserId = command.ActorUserId, ConfirmedAt = occurredAt, IdempotencyKey = $"code-delivery:{command.CodeGroupId}" };
        db.CodeGroupDeliveryConfirmations.Add(confirmation);
        await accounting.RecordDeliveryAsync(group, terms, occurredAt, ct);
        if (quote.Net > 0m)
        {
            var lines = new List<FinancialPostingLine> { new("1200", quote.Net, 0m, TeacherId: group.TeacherId) };
            if (quote.TeacherShare > 0m) lines.Add(new("2000", 0m, quote.TeacherShare, TeacherId: group.TeacherId));
            if (quote.PlatformShare > 0m) lines.Add(new("4000", 0m, quote.PlatformShare, TeacherId: group.TeacherId));
            await posting.PostAsync(new("CodeSale", group.Id, "Sale", $"code-delivery-sale:{group.Id:N}",
                "حساب دفعة أكواد عند التسليم", occurredAt, command.ActorUserId, lines), ct);
        }
        if (quote.TeacherShare > 0m)
            await posting.PostAsync(new("TeacherRetained", group.Id, "TeacherRetained", $"code-delivery-retained:{group.Id:N}",
                "نصيب المدرّس المحتفظ به من دفعة الأكواد", occurredAt, command.ActorUserId,
                [new("2000", quote.TeacherShare, 0m, TeacherId: group.TeacherId), new("1200", 0m, quote.TeacherShare, TeacherId: group.TeacherId)]), ct);
        if (command.Payment is not null) await collection.RecordAsync(confirmation, group.TeacherId.Value, command.ActorUserId, command.Payment, ct);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success, confirmation.Id, confirmation.ConfirmedAt);
    }
}

using System.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter;

public sealed record CollectCodeGroupPaymentCommand(Guid ActorUserId, Guid CodeGroupId, CodeCollectionInput Payment)
    : IRequest<TeacherFinanceCommandResult>;

public sealed class CollectCodeGroupPaymentCommandHandler(IAppDbContext db, IFinancialPostingService posting)
    : IRequestHandler<CollectCodeGroupPaymentCommand, TeacherFinanceCommandResult>
{
    public Task<TeacherFinanceCommandResult> Handle(CollectCodeGroupPaymentCommand command, CancellationToken ct) =>
        CodeFinanceTransaction.ExecuteAsync(db, token => HandleOnce(command, token), ct);

    private async Task<TeacherFinanceCommandResult> HandleOnce(CollectCodeGroupPaymentCommand command, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var delivery = await db.CodeGroupDeliveryConfirmations.Include(x => x.CodeGroup).Include(x => x.Payments)
            .SingleOrDefaultAsync(x => x.CodeGroupId == command.CodeGroupId, ct);
        if (delivery?.PlatformAmountDue == null || !delivery.CodeGroup.TeacherId.HasValue)
            return new(TeacherFinanceCommandStatus.Conflict, Message: "هذه الدفعة ليس لها مبلغ تحصيل موثق. راجع حسابها قبل إضافة سداد");
        var input = command.Payment;
        var existing = await db.CodeGroupDeliveryPayments.SingleOrDefaultAsync(x => x.IdempotencyKey == input.IdempotencyKey, ct);
        if (existing != null)
            return existing.DeliveryConfirmationId == delivery.Id && existing.Amount == input.Amount &&
                existing.TreasuryAccountId == input.TreasuryAccountId && existing.Reference == input.Reference.Trim()
                ? new(TeacherFinanceCommandStatus.Success, existing.Id, existing.ReceivedAt, true)
                : new(TeacherFinanceCommandStatus.Conflict, Message: "مرجع العملية مستخدم ببيانات مختلفة");
        var service = new CodeGroupCollectionService(db, posting);
        var error = await service.ValidateAsync(input, delivery.PlatformAmountDue.Value - delivery.Payments.Sum(x => x.Amount), ct);
        if (error != null) return new(TeacherFinanceCommandStatus.Invalid, Message: error);
        await service.RecordAsync(delivery, delivery.CodeGroup.TeacherId.Value, command.ActorUserId, input, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(TeacherFinanceCommandStatus.Success);
    }
}

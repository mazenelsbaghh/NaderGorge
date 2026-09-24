using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

public sealed record CodeCollectionInput(decimal Amount, Guid TreasuryAccountId, string Reference, string IdempotencyKey);

/// <summary>Receipts settle the batch receivable; they never create a second sale or teacher entitlement.</summary>
public sealed class CodeGroupCollectionService(IAppDbContext db, IFinancialPostingService posting)
{
    public async Task<string?> ValidateAsync(CodeCollectionInput input, decimal outstanding, CancellationToken ct)
    {
        if (input.Amount <= 0m || input.Amount != decimal.Round(input.Amount, 2) || input.Amount > outstanding)
            return "المبلغ لازم يكون أكبر من صفر ولا يزيد عن الباقي على المدرّس";
        if (string.IsNullOrWhiteSpace(input.Reference) || input.Reference.Length > 300 ||
            string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 240)
            return "اكتب مرجعًا للتحصيل وأعد المحاولة";
        return await db.TreasuryAccounts.AnyAsync(x => x.Id == input.TreasuryAccountId && x.IsActive, ct)
            ? null : "اختر الخزينة أو المحفظة التي استلمت المبلغ";
    }

    public async Task RecordAsync(CodeGroupDeliveryConfirmation delivery, Guid teacherId, Guid actorId,
        CodeCollectionInput input, CancellationToken ct)
    {
        var treasury = await db.TreasuryAccounts.SingleAsync(x => x.Id == input.TreasuryAccountId, ct);
        var code = await db.FinancialAccounts.Where(x => x.Id == treasury.FinancialAccountId).Select(x => x.Code).SingleAsync(ct);
        var payment = new CodeGroupDeliveryPayment { DeliveryConfirmationId = delivery.Id, Amount = input.Amount,
            TreasuryAccountId = input.TreasuryAccountId, Reference = input.Reference.Trim(), ReceivedByUserId = actorId,
            ReceivedAt = DateTime.UtcNow, IdempotencyKey = input.IdempotencyKey.Trim() };
        var entry = await posting.PostAsync(new FinancialPostingRequest("TeacherCodeCollection", payment.Id,
            "Collection", $"code-collection:{payment.Id:N}", "تحصيل مبلغ دفعة أكواد من المدرّس", payment.ReceivedAt, actorId,
            [new(code, input.Amount, 0m, TeacherId: teacherId, TreasuryAccountId: treasury.Id),
             new("1200", 0m, input.Amount, TeacherId: teacherId)]), ct);
        payment.JournalEntryId = entry.Id;
        db.CodeGroupDeliveryPayments.Add(payment);
    }
}

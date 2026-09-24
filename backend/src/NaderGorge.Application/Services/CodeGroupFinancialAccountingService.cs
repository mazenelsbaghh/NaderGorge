using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Services;

/// <summary>Owns the one-off, audited financial trigger for a delivered code batch.</summary>
public sealed class CodeGroupFinancialAccountingService
{
    private readonly IAppDbContext _db;
    private readonly TeacherAccountingService _accounting;

    public CodeGroupFinancialAccountingService(IAppDbContext db, TeacherAccountingService accounting)
        => (_db, _accounting) = (db, accounting);

    public async Task RecordDeliveryAsync(CodeGroup group, CodeGroupFinancialTerms terms, DateTime occurredAt, CancellationToken ct)
    {
        if (group.CodeType == CodeType.Balance || !group.TeacherId.HasValue)
            return;

        if (group.AccountingRecordedAt.HasValue || await CodeGroupAccountingGuard.HasBatchChargeAsync(_db, group.Id, ct)) return;
        if (await CodeGroupAccountingGuard.HasActivationAsync(_db, group.Id, ct))
            throw new InvalidOperationException("لا يمكن احتساب دفعة بدأ استخدامها عند التسليم");

        var quote = await CodeGroupFinanceQuote.CalculateAsync(_db, group, terms, occurredAt, ct);
        var agreement = quote.Agreement;
        var gross = quote.Gross;
        var paid = quote.Net;
        var teacherShare = quote.TeacherShare;
        await _accounting.RecordEventAsync(new TeacherFinancialEventInput(
            TeacherFinancialSourceType.AccessCodeGeneration, group.Id, null, quote.TargetType, quote.TargetId,
            gross, gross - paid, paid, 0m, paid - teacherShare,
            $"access-code-group-delivery:{group.Id}",
            System.Text.Json.JsonSerializer.Serialize(new
            {
                codeGroupId = group.Id, group.Name, group.CodeType, group.TotalCodes,
                financialTrigger = TeacherAgreementTrigger.CodeDelivery.ToString()
            }),
            occurredAt, TeacherFinancialReviewStatus.AutoApproved,
            new[]
            {
                new TeacherFinancialAllocationInput(group.TeacherId.Value, quote.AllocationMode, agreement.AllocationValue,
                    quote.Basis, teacherShare, paid - teacherShare, null, null, quote.ContentName, null,
                    AgreementId: agreement.AgreementId, AgreementScopeType: agreement.ScopeType,
                    AgreementScopeId: agreement.ScopeId, AgreementAllocationMode: agreement.AllocationMode,
                    PriceBasis: agreement.PriceBasis, RetainedByTeacher: true)
            }), ct);

        group.AccountingRecordedAt = occurredAt;
    }

}

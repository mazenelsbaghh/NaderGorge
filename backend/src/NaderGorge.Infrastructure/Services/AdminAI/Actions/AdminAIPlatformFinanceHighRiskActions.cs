using MediatR;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Features.Admin.PlatformFinance.Periods;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Interfaces.Finance;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAICreatePlatformExpenseInput(decimal Amount, DateTime OccurredAt, Guid CategoryId, Guid? CostCenterId, Guid? VendorId, string Description, string? DocumentNumber);
public sealed record AdminAIPostPlatformExpenseInput(Guid ExpenseId, Guid? TreasuryAccountId, string? Reason);
public sealed record AdminAIPayPlatformExpenseInput(Guid ExpenseId, Guid TreasuryAccountId, decimal Amount, string PaymentReference);
public sealed record AdminAICreatePlatformRefundInput(Guid OriginalSourceId, string OriginalSourceType, Guid StudentId, Guid? TeacherId, decimal PlatformAmount, decimal TeacherAmount, int Method, Guid? TreasuryAccountId, string Reason, string? PaymentReference);
public sealed record AdminAIPostPlatformRefundInput(Guid RefundId);
public sealed record AdminAITransferTreasuryInput(Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId, decimal Amount, string Reference);
public sealed record AdminAIReconcileTreasuryInput(Guid TreasuryAccountId, DateTime AsOfDate, decimal CountedOrStatementBalance, string EvidenceNote);
public sealed record AdminAICreateFinanceBudgetInput(string Name, int PeriodKind, DateTime StartDate, DateTime EndDate, IReadOnlyCollection<FinanceBudgetLineInput> Lines);
public sealed record AdminAIAccountingPeriodInput(Guid PeriodId, string Reason);
public sealed record AdminAIFinanceHistoryBackfillInput(DateTime From, DateTime To);
public sealed record AdminAIReversePlatformRecordInput(Guid RecordId, string Reason);
public sealed record AdminAIClassifyWalletExpenseInput(Guid ReviewId, string BeneficiaryName, string Reason, Guid CategoryId, Guid? CostCenterId);
public sealed record AdminAIClassifyWalletTransferInput(Guid ReviewId, Guid DestinationTreasuryAccountId);

public sealed class AdminAICreatePlatformExpenseAction(IPlatformFinanceOperationsService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAICreatePlatformExpenseInput>(preview)
{
    public override string Key => "admin.platform-finance.expense.create";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAICreatePlatformExpenseInput i, string operationId, CancellationToken ct)
    { var row = await service.CreateExpenseAsync(new(i.Amount, i.OccurredAt, i.CategoryId, i.CostCenterId, i.VendorId, i.Description, i.DocumentNumber, actor), ct); return PlatformFinanceOutcome.Success(row.Id, ["platform-expenses", "ledger"]); }
}
public sealed class AdminAIPostPlatformExpenseAction(IPlatformFinanceOperationsService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIPostPlatformExpenseInput>(preview)
{
    public override string Key => "admin.platform-finance.expense.post";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIPostPlatformExpenseInput i, string operationId, CancellationToken ct)
    { var row = await service.PostExpenseAsync(i.ExpenseId, new(i.TreasuryAccountId, actor, operationId, i.Reason), ct); return PlatformFinanceOutcome.Success(row.Id, ["platform-expenses", "ledger", "treasury"]); }
}
public sealed class AdminAIPayPlatformExpenseAction(IPlatformFinanceOperationsService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIPayPlatformExpenseInput>(preview)
{
    public override string Key => "admin.platform-finance.expense.pay";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIPayPlatformExpenseInput i, string operationId, CancellationToken ct)
    { var row = await service.PayExpenseAsync(i.ExpenseId, new(i.TreasuryAccountId, i.Amount, i.PaymentReference, actor, operationId), ct); return PlatformFinanceOutcome.Success(row.Id, ["platform-expenses", "ledger", "treasury"]); }
}
public sealed class AdminAICreatePlatformRefundAction(IPlatformFinanceOperationsService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAICreatePlatformRefundInput>(preview)
{
    public override string Key => "admin.platform-finance.refund.create";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAICreatePlatformRefundInput i, string operationId, CancellationToken ct)
    { var row = await service.CreateRefundAsync(new(i.OriginalSourceId, i.OriginalSourceType, i.StudentId, i.TeacherId, i.PlatformAmount, i.TeacherAmount, i.Method, i.TreasuryAccountId, i.Reason, i.PaymentReference, actor), ct); return PlatformFinanceOutcome.Success(row.Id, ["platform-refunds", "ledger"]); }
}
public sealed class AdminAIPostPlatformRefundAction(IPlatformFinanceOperationsService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIPostPlatformRefundInput>(preview)
{
    public override string Key => "admin.platform-finance.refund.post";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIPostPlatformRefundInput i, string operationId, CancellationToken ct)
    { var row = await service.PostRefundAsync(i.RefundId, operationId, actor, ct); return PlatformFinanceOutcome.Success(row.Id, ["platform-refunds", "ledger", "treasury"]); }
}
public sealed class AdminAITransferTreasuryAction(IPlatformFinancePlanningService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAITransferTreasuryInput>(preview)
{
    public override string Key => "admin.platform-finance.treasury.transfer";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAITransferTreasuryInput i, string operationId, CancellationToken ct)
    { var row = await service.TransferAsync(new(i.SourceTreasuryAccountId, i.DestinationTreasuryAccountId, i.Amount, i.Reference, actor, operationId), ct); return PlatformFinanceOutcome.Success(row.Id, ["treasury", "ledger"]); }
}
public sealed class AdminAIReconcileTreasuryAction(IPlatformFinancePlanningService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIReconcileTreasuryInput>(preview)
{
    public override string Key => "admin.platform-finance.treasury.reconcile";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIReconcileTreasuryInput i, string operationId, CancellationToken ct)
    { var row = await service.ReconcileAsync(new(i.TreasuryAccountId, i.AsOfDate, i.CountedOrStatementBalance, i.EvidenceNote, actor), ct); return PlatformFinanceOutcome.Success(row.Id, ["treasury", "reconciliation", "ledger"]); }
}
public sealed class AdminAICreateFinanceBudgetAction(IPlatformFinancePlanningService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAICreateFinanceBudgetInput>(preview)
{
    public override string Key => "admin.platform-finance.budget.create";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAICreateFinanceBudgetInput i, string operationId, CancellationToken ct)
    { var row = await service.CreateBudgetAsync(new(i.Name, i.PeriodKind, i.StartDate, i.EndDate, actor, i.Lines), ct); return PlatformFinanceOutcome.Success(row.Id, ["budgets", "finance"]); }
}
public sealed class AdminAICloseAccountingPeriodAction(AccountingPeriodCommands service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIAccountingPeriodInput>(preview)
{
    public override string Key => "admin.platform-finance.period.close";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIAccountingPeriodInput i, string operationId, CancellationToken ct)
    { var row = await service.CloseAsync(i.PeriodId, actor, i.Reason, ct); return PlatformFinanceOutcome.Success(row.Id, ["accounting-periods", "ledger"]); }
}
public sealed class AdminAIReopenAccountingPeriodAction(AccountingPeriodCommands service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIAccountingPeriodInput>(preview)
{
    public override string Key => "admin.platform-finance.period.reopen";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIAccountingPeriodInput i, string operationId, CancellationToken ct)
    { var row = await service.ReopenAsync(i.PeriodId, actor, i.Reason, ct); return PlatformFinanceOutcome.Success(row.Id, ["accounting-periods", "ledger"]); }
}
public sealed class AdminAIBackfillFinanceHistoryAction(IPlatformFinanceMigrationService service, IAdminAIActionPreviewSource preview) : AdminAIServiceActionCapability<AdminAIFinanceHistoryBackfillInput>(preview)
{
    public override string Key => "admin.platform-finance.history.backfill";
    protected override async Task<AdminAIActionOutcome> ExecuteAuthoritativelyAsync(Guid actor, AdminAIFinanceHistoryBackfillInput i, string operationId, CancellationToken ct)
    { var row = await service.PostAsync(i.From, i.To, actor, ct); return AdminAIActionOutcomeFactory.Success(new { row.BatchId, row.Posted, row.AlreadyPosted, row.Failed, row.Errors }, row.Posted, ["ledger", "finance-history"]); }
}
public sealed class AdminAIReversePlatformExpenseAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIReversePlatformRecordInput, PlatformFinanceReversalResult>(mediator, preview)
{
    public override string Key => "admin.platform-finance.expense.reverse";
    protected override IRequest<PlatformFinanceReversalResult> CreateCommand(AdminAIReversePlatformRecordInput i, Guid actor, string operationId) => new ReversePlatformExpenseCommand(i.RecordId, actor, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(PlatformFinanceReversalResult r) => AdminAIActionOutcomeFactory.Success(new { r.RecordId, r.ReversalId, r.AlreadyApplied }, r.AlreadyApplied ? 0 : 1, ["platform-expenses", "ledger"]);
}
public sealed class AdminAIReversePlatformRefundAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIReversePlatformRecordInput, PlatformFinanceReversalResult>(mediator, preview)
{
    public override string Key => "admin.platform-finance.refund.reverse";
    protected override IRequest<PlatformFinanceReversalResult> CreateCommand(AdminAIReversePlatformRecordInput i, Guid actor, string operationId) => new ReversePlatformRefundCommand(i.RecordId, actor, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(PlatformFinanceReversalResult r) => AdminAIActionOutcomeFactory.Success(new { r.RecordId, r.ReversalId, r.AlreadyApplied }, r.AlreadyApplied ? 0 : 1, ["platform-refunds", "ledger"]);
}
public sealed class AdminAIClassifyWalletExpenseAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIClassifyWalletExpenseInput, WalletTransferClassificationResult>(mediator, preview)
{
    public override string Key => "admin.platform-finance.wallet-transfer.classify-expense";
    protected override IRequest<WalletTransferClassificationResult> CreateCommand(AdminAIClassifyWalletExpenseInput i, Guid actor, string operationId) => new RecordWalletTransferExpenseCommand(i.ReviewId, actor, i.BeneficiaryName, i.Reason, i.CategoryId, i.CostCenterId);
    protected override AdminAIActionOutcome ToOutcome(WalletTransferClassificationResult r) => PlatformFinanceOutcome.Success(r.AuthorityRecordId, ["wallet-reviews", "platform-expenses", "ledger"]);
}
public sealed class AdminAIClassifyWalletTransferAction(IMediator mediator, IAdminAIActionPreviewSource preview) : AdminAIMediatRActionCapability<AdminAIClassifyWalletTransferInput, WalletTransferClassificationResult>(mediator, preview)
{
    public override string Key => "admin.platform-finance.wallet-transfer.classify-internal";
    protected override IRequest<WalletTransferClassificationResult> CreateCommand(AdminAIClassifyWalletTransferInput i, Guid actor, string operationId) => new RecordWalletInternalTransferCommand(i.ReviewId, actor, i.DestinationTreasuryAccountId);
    protected override AdminAIActionOutcome ToOutcome(WalletTransferClassificationResult r) => PlatformFinanceOutcome.Success(r.AuthorityRecordId, ["wallet-reviews", "treasury", "ledger"]);
}

internal static class PlatformFinanceOutcome
{
    public static AdminAIActionOutcome Success(Guid id, IReadOnlyList<string> scopes) =>
        AdminAIActionOutcomeFactory.Success(new { id }, 1, scopes);
}

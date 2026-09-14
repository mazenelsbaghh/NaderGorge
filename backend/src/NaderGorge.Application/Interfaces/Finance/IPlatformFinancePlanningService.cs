using NaderGorge.Domain.Entities;

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record FinanceBudgetLineInput(Guid FinancialAccountId, Guid? CostCenterId, Guid? TeacherId, decimal PlannedAmount);
public sealed record CreateFinanceBudgetRequest(string Name, int PeriodKind, DateTime StartDate, DateTime EndDate, Guid CreatedByUserId, IReadOnlyCollection<FinanceBudgetLineInput> Lines);
public sealed record TreasuryTransferRequest(Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId, decimal Amount, string Reference, Guid ActorUserId, string IdempotencyKey);
public sealed record TreasuryReconciliationRequest(Guid TreasuryAccountId, DateTime AsOfDate, decimal CountedOrStatementBalance, string EvidenceNote, Guid ActorUserId);

public interface IPlatformFinancePlanningService
{
    Task<FinanceBudgetPlan> CreateBudgetAsync(CreateFinanceBudgetRequest request, CancellationToken ct);
    Task<IReadOnlyList<object>> GetBudgetActualsAsync(DateTime from, DateTime to, CancellationToken ct);
    Task<TreasuryTransfer> TransferAsync(TreasuryTransferRequest request, CancellationToken ct);
    Task<TreasuryReconciliation> ReconcileAsync(TreasuryReconciliationRequest request, CancellationToken ct);
}

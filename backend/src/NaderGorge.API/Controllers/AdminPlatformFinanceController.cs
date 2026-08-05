using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/platform-finance")]
public sealed class AdminPlatformFinanceController(
    PlatformFinanceDashboardService finance,
    IPlatformFinanceOperationsService operations,
    IPlatformFinancePlanningService planning,
    IPlatformFinanceExportService export,
    IPlatformFinanceMigrationService migration,
    IAppDbContext db)
    : ControllerBase
{
    [HttpGet("dashboard")]
    [HasPermission("finance.dashboard.view")]
    public Task<PlatformFinanceDashboardDto> Dashboard(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct) => finance.GetDashboardAsync(from, to, ct);

    [HttpGet("ledger")]
    [HasPermission("finance.ledger.view")]
    public Task<IReadOnlyList<PlatformFinanceJournalDto>> Ledger(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) => finance.GetLedgerAsync(from, to, page, pageSize, ct);

    [HttpGet("journals/{journalId:guid}")]
    [HasPermission("finance.ledger.view")]
    public async Task<ActionResult<PlatformFinanceJournalDto>> Journal(Guid journalId, CancellationToken ct)
        => (await finance.GetJournalAsync(journalId, ct)) is { } journal ? Ok(journal) : NotFound();

    [HttpGet("teachers/summary")]
    [HasPermission("finance.teacher-summary.view")]
    public Task<IReadOnlyList<PlatformFinanceTeacherSummaryDto>> TeacherSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct) => finance.GetTeacherSummaryAsync(from, to, ct);

    [HttpPost("expenses")]
    [HasPermission("finance.expenses.create")]
    public async Task<ActionResult<object>> CreateExpense([FromBody] CreateExpenseBody body, CancellationToken ct)
    {
        var actor = CurrentUserId();
        var expense = await operations.CreateExpenseAsync(new CreatePlatformExpenseRequest(
            body.Amount, body.OccurredAt, body.CategoryId, body.CostCenterId, body.VendorId,
            body.Description, body.DocumentNumber, actor), ct);
        return Ok(new { expense.Id, expense.DocumentNumber, expense.Status, expense.Amount });
    }

    [HttpPost("expenses/{expenseId:guid}/post")]
    [HasPermission("finance.expenses.post")]
    public async Task<ActionResult<object>> PostExpense(Guid expenseId, [FromBody] PostExpenseBody body, CancellationToken ct)
    {
        var expense = await operations.PostExpenseAsync(expenseId, new PostPlatformExpenseRequest(
            body.TreasuryAccountId, CurrentUserId(), body.IdempotencyKey, body.Reason), ct);
        return Ok(new { expense.Id, expense.Status, expense.JournalEntryId });
    }

    [HttpPost("expenses/{expenseId:guid}/payments")]
    [HasPermission("finance.expenses.post")]
    public async Task<ActionResult<object>> PayExpense(Guid expenseId, [FromBody] PayExpenseBody body, CancellationToken ct)
    {
        var payment = await operations.PayExpenseAsync(expenseId, new PayPlatformExpenseRequest(
            body.TreasuryAccountId, body.Amount, body.PaymentReference, CurrentUserId(), body.IdempotencyKey), ct);
        return Ok(new { payment.Id, payment.Amount, payment.JournalEntryId });
    }

    [HttpPost("refunds")]
    [HasPermission("finance.refunds.create")]
    public async Task<ActionResult<object>> CreateRefund([FromBody] CreateRefundBody body, CancellationToken ct)
    {
        var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(
            body.OriginalSourceId, body.OriginalSourceType, body.StudentId, body.TeacherId,
            body.PlatformAmount, body.TeacherAmount, body.Method, body.TreasuryAccountId,
            body.Reason, body.PaymentReference, CurrentUserId()), ct);
        return Ok(new { refund.Id, refund.TotalAmount, refund.Method, refund.Status });
    }

    [HttpPost("refunds/{refundId:guid}/post")]
    [HasPermission("finance.refunds.post")]
    public async Task<ActionResult<object>> PostRefund(Guid refundId, [FromBody] PostRefundBody body, CancellationToken ct)
    {
        var refund = await operations.PostRefundAsync(refundId, body.IdempotencyKey, CurrentUserId(), ct);
        return Ok(new { refund.Id, refund.TotalAmount, refund.Status, refund.JournalEntryId });
    }

    [HttpGet("bootstrap")]
    [HasPermission("finance.dashboard.view")]
    public async Task<ActionResult<object>> Bootstrap(CancellationToken ct)
    {
        return Ok(new
        {
            accounts = await db.FinancialAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.Name, x.Type }).ToListAsync(ct),
            treasuryAccounts = await db.TreasuryAccounts.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Type, x.MaskedIdentifier }).ToListAsync(ct),
            categories = await db.ExpenseCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.AccountCode }).ToListAsync(ct),
            costCenters = await db.FinanceCostCenters.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct),
            vendors = await db.FinanceVendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).ToListAsync(ct)
        });
    }

    [HttpPost("budgets")]
    [HasPermission("finance.budgets.manage")]
    public async Task<ActionResult<object>> CreateBudget([FromBody] CreateBudgetBody body, CancellationToken ct)
    {
        var budget = await planning.CreateBudgetAsync(new CreateFinanceBudgetRequest(
            body.Name, body.PeriodKind, body.StartDate, body.EndDate, CurrentUserId(),
            body.Lines.Select(x => new FinanceBudgetLineInput(x.FinancialAccountId, x.CostCenterId, x.TeacherId, x.PlannedAmount)).ToArray()), ct);
        return Ok(new { budget.Id, budget.Name, budget.Status, budget.StartDate, budget.EndDate });
    }

    [HttpGet("budgets/actuals")]
    [HasPermission("finance.budgets.manage")]
    public Task<IReadOnlyList<object>> BudgetActuals([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => planning.GetBudgetActualsAsync(from, to, ct);

    [HttpPost("treasury/transfers")]
    [HasPermission("finance.treasury.manage")]
    public async Task<ActionResult<object>> Transfer([FromBody] TreasuryTransferBody body, CancellationToken ct)
    {
        var transfer = await planning.TransferAsync(new TreasuryTransferRequest(
            body.SourceTreasuryAccountId, body.DestinationTreasuryAccountId, body.Amount, body.Reference, CurrentUserId(), body.IdempotencyKey), ct);
        return Ok(new { transfer.Id, transfer.Amount, transfer.JournalEntryId });
    }

    [HttpPost("treasury/reconciliations")]
    [HasPermission("finance.treasury.reconcile")]
    public async Task<ActionResult<object>> Reconcile([FromBody] TreasuryReconciliationBody body, CancellationToken ct)
    {
        var result = await planning.ReconcileAsync(new TreasuryReconciliationRequest(
            body.TreasuryAccountId, body.AsOfDate, body.CountedOrStatementBalance, body.EvidenceNote, CurrentUserId()), ct);
        return Ok(new { result.Id, result.SystemBalance, result.CountedOrStatementBalance, result.Variance });
    }

    [HttpGet("exports/{format}")]
    [HasPermission("finance.export")]
    public async Task<IActionResult> Export(string format, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var result = await export.ExportLedgerAsync(format.ToLowerInvariant(), from, to, CurrentUserId(), ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("migration/preview")]
    [HasPermission("finance.migration.manage")]
    public Task<FinanceHistoricalMigrationPreview> MigrationPreview([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => migration.PreviewAsync(from, to, ct);

    [HttpPost("migration/post")]
    [HasPermission("finance.migration.manage")]
    public Task<FinanceHistoricalMigrationResult> PostHistoricalMigration([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => migration.PostAsync(from, to, CurrentUserId(), ct);

    [HttpGet("periods")]
    [HasPermission("finance.periods.close")]
    public async Task<ActionResult<object>> Periods(CancellationToken ct)
        => Ok(await db.AccountingPeriods.AsNoTracking().OrderByDescending(x => x.StartDate).ToListAsync(ct));

    [HttpPost("periods/{periodId:guid}/close")]
    [HasPermission("finance.periods.close")]
    public async Task<ActionResult<object>> ClosePeriod(Guid periodId, [FromBody] PeriodReasonBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason)) throw new InvalidOperationException("FINANCE_REASON_REQUIRED");
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.Id == periodId, ct)
            ?? throw new InvalidOperationException("FINANCE_PERIOD_NOT_FOUND");
        if (period.Status == NaderGorge.Domain.Enums.AccountingPeriodStatus.Closed)
            throw new InvalidOperationException("FINANCE_PERIOD_ALREADY_CLOSED");
        period.Status = NaderGorge.Domain.Enums.AccountingPeriodStatus.Closed;
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedByUserId = CurrentUserId();
        period.CloseReason = body.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return Ok(new { period.Id, period.Status });
    }

    [HttpPost("periods/{periodId:guid}/reopen")]
    [HasPermission("finance.periods.reopen")]
    public async Task<ActionResult<object>> ReopenPeriod(Guid periodId, [FromBody] PeriodReasonBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Reason)) throw new InvalidOperationException("FINANCE_REASON_REQUIRED");
        var period = await db.AccountingPeriods.SingleOrDefaultAsync(x => x.Id == periodId, ct)
            ?? throw new InvalidOperationException("FINANCE_PERIOD_NOT_FOUND");
        period.Status = NaderGorge.Domain.Enums.AccountingPeriodStatus.Reopened;
        period.CloseReason = body.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return Ok(new { period.Id, period.Status });
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record CreateExpenseBody(decimal Amount, DateTime OccurredAt, Guid CategoryId, Guid? CostCenterId, Guid? VendorId, string Description, string? DocumentNumber);
public sealed record PostExpenseBody(Guid? TreasuryAccountId, string IdempotencyKey, string? Reason);
public sealed record PayExpenseBody(Guid TreasuryAccountId, decimal Amount, string PaymentReference, string IdempotencyKey);
public sealed record CreateRefundBody(Guid OriginalSourceId, string OriginalSourceType, Guid StudentId, Guid? TeacherId, decimal PlatformAmount, decimal TeacherAmount, int Method, Guid? TreasuryAccountId, string Reason, string? PaymentReference);
public sealed record PostRefundBody(string IdempotencyKey);
public sealed record CreateBudgetLineBody(Guid FinancialAccountId, Guid? CostCenterId, Guid? TeacherId, decimal PlannedAmount);
public sealed record CreateBudgetBody(string Name, int PeriodKind, DateTime StartDate, DateTime EndDate, IReadOnlyList<CreateBudgetLineBody> Lines);
public sealed record TreasuryTransferBody(Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId, decimal Amount, string Reference, string IdempotencyKey);
public sealed record TreasuryReconciliationBody(Guid TreasuryAccountId, DateTime AsOfDate, decimal CountedOrStatementBalance, string EvidenceNote);
public sealed record PeriodReasonBody(string Reason);

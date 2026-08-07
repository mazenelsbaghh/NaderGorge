using Microsoft.AspNetCore.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Application.Features.Admin.PlatformFinance;
using NaderGorge.Application.Features.Admin.PlatformFinance.Periods;
using NaderGorge.Application.Features.Admin.PlatformFinance.Reports;
using NaderGorge.Application.Features.Admin.PlatformFinance.Teachers;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.Finance.Migration;
using NaderGorge.Application.Services;
using NaderGorge.Application.Features.Admin.Commands;

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
    IAppDbContext db,
    IFinancialPostingService posting,
    PlatformFinancialReportQueries reports,
    FinancialReconciliationService reconciliation,
    AccountingPeriodCommands periods,
    GetTeacherFinancialSummaryQuery teacherSummary,
    IMediator mediator)
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

    [HttpGet("teachers/{teacherId:guid}/summary")]
    [HasPermission("finance.teacher-summary.view")]
    public async Task<ActionResult<TeacherFinancialSummaryDto>> TeacherDetail(Guid teacherId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => await teacherSummary.GetAsync(teacherId, from, to, ct) is { } result ? Ok(result) : NotFound();

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

    [HttpGet("expenses")]
    [HasPermission("finance.expenses.view")]
    public async Task<ActionResult<object>> Expenses([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = db.PlatformExpenses.AsNoTracking().Include(x => x.Payments).AsQueryable();
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.OccurredAt < to.Value.Date.AddDays(1));
        return Ok(await query.OrderByDescending(x => x.OccurredAt).Take(200).Select(x => new { x.Id, x.DocumentNumber, x.Amount, x.OccurredAt, x.Status, x.Description, paid = x.Payments.Sum(payment => (decimal?)payment.Amount) ?? 0m }).ToListAsync(ct));
    }

    [HttpGet("wallet-transfers/reviews")]
    [HasPermission("finance.expenses.view")]
    public async Task<ActionResult<object>> WalletTransferReviews(CancellationToken ct)
        => Ok(await (from review in db.WalletTransferReviews.AsNoTracking()
                     join wallet in db.DigitalWallets.AsNoTracking() on review.SourceWalletId equals wallet.Id
                     join treasury in db.TreasuryAccounts.AsNoTracking() on wallet.Id equals treasury.DigitalWalletId into treasuries
                     from treasury in treasuries.Where(x => x.IsActive).DefaultIfEmpty()
                     where review.Status == NaderGorge.Domain.Entities.WalletTransferReviewStatus.PendingClassification
                     orderby review.OccurredAt descending
                     select new
                     {
                         review.Id, review.DestinationPhoneNumber, review.Amount, review.ServiceFee, review.TransferReference, review.OccurredAt,
                         sourceWallet = wallet.Label, sourceWalletNumber = wallet.PhoneNumber, sourceTreasuryAccountId = treasury == null ? (Guid?)null : treasury.Id
                     }).Take(200).ToListAsync(ct));

    [HttpPost("wallet-transfers/reviews/backfill")]
    [HasPermission("finance.expenses.create")]
    public async Task<ActionResult<object>> BackfillWalletTransferReviews(CancellationToken ct)
    {
        var alreadyReviewedSmsIds = await db.WalletTransferReviews.AsNoTracking().Select(x => x.IncomingSmsLogId).ToListAsync(ct);
        var oldLogs = await db.IncomingSmsLogs.AsNoTracking()
            .Where(x => !alreadyReviewedSmsIds.Contains(x.Id)).OrderBy(x => x.ReceivedAt).ToListAsync(ct);
        var reviews = oldLogs.Select(log => new { Log = log, Parsed = SmsParser.Parse(log.Body) })
            .Where(x => SmsParser.IsOutgoingTransfer(x.Log.Body) && x.Parsed.Amount.HasValue)
            .Select(x => new NaderGorge.Domain.Entities.WalletTransferReview
            {
                IncomingSmsLogId = x.Log.Id,
                SourceWalletId = x.Log.WalletId,
                DestinationPhoneNumber = x.Parsed.RecipientPhone ?? x.Parsed.SenderPhone ?? "غير معروف",
                Amount = x.Parsed.Amount!.Value,
                ServiceFee = x.Parsed.ServiceFee,
                TransferReference = x.Parsed.TransferReference,
                OccurredAt = x.Log.ReceivedAt
            }).ToList();
        if (reviews.Count > 0)
        {
            db.WalletTransferReviews.AddRange(reviews);
            await db.SaveChangesAsync(ct);
        }
        return Ok(new { added = reviews.Count });
    }

    [HttpGet("wallets/report")]
    [HasPermission("finance.dashboard.view")]
    public async Task<ActionResult<object>> WalletReport([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var start = (from ?? DateTime.UtcNow.AddMonths(-1)).Date;
        var end = (to ?? DateTime.UtcNow).Date.AddDays(1);
        var wallets = await db.DigitalWallets.AsNoTracking().OrderBy(x => x.Label).ToListAsync(ct);
        var logs = await db.IncomingSmsLogs.AsNoTracking().Where(x => x.ReceivedAt >= start && x.ReceivedAt < end).ToListAsync(ct);
        var reviews = await db.WalletTransferReviews.AsNoTracking().Where(x => x.OccurredAt >= start && x.OccurredAt < end).ToListAsync(ct);
        var teacherRecharges = await (from recharge in db.RechargeRequests.AsNoTracking()
                                      join teacher in db.TeacherProfiles.AsNoTracking() on recharge.TeacherId equals teacher.Id
                                      join user in db.Users.AsNoTracking() on teacher.UserId equals user.Id
                                      where recharge.Status == NaderGorge.Domain.Enums.RechargeRequestStatus.Matched || recharge.Status == NaderGorge.Domain.Enums.RechargeRequestStatus.Approved
                                      where recharge.TeacherId != null && (recharge.ResolvedAt ?? recharge.CreatedAt) >= start && (recharge.ResolvedAt ?? recharge.CreatedAt) < end
                                      select new { recharge.WalletId, TeacherName = user.FullName, recharge.Amount }).ToListAsync(ct);
        var transactions = logs.Select(log => new { Log = log, Parsed = SmsParser.Parse(log.Body) })
            .Where(x => x.Parsed.Amount.HasValue && (SmsParser.IsIncomingTransfer(x.Log.Body) || SmsParser.IsOutgoingTransfer(x.Log.Body)))
            .OrderByDescending(x => x.Log.ReceivedAt).Take(100)
            .Select(x => new { x.Log.Id, x.Log.WalletId, x.Log.ReceivedAt, amount = x.Parsed.Amount, type = SmsParser.IsOutgoingTransfer(x.Log.Body) ? "outgoing" : "incoming", phone = SmsParser.IsOutgoingTransfer(x.Log.Body) ? x.Parsed.RecipientPhone : x.Parsed.SenderPhone, x.Log.Body });
        return Ok(new
        {
            from = start, to = end.AddTicks(-1),
            wallets = wallets.Select(wallet => new
            {
                wallet.Id, wallet.Label, wallet.PhoneNumber, wallet.CurrentBalance,
                incoming = logs.Where(log => log.WalletId == wallet.Id && SmsParser.IsIncomingTransfer(log.Body)).Sum(log => SmsParser.Parse(log.Body).Amount ?? 0m),
                outgoing = reviews.Where(review => review.SourceWalletId == wallet.Id).Sum(review => review.Amount + review.ServiceFee),
                expenses = reviews.Where(review => review.SourceWalletId == wallet.Id && review.Status == NaderGorge.Domain.Entities.WalletTransferReviewStatus.RecordedAsExpense).Sum(review => review.Amount + review.ServiceFee),
                internalTransfers = reviews.Where(review => review.SourceWalletId == wallet.Id && review.Status == NaderGorge.Domain.Entities.WalletTransferReviewStatus.RecordedAsInternalTransfer).Sum(review => review.Amount),
                transactions = logs.Count(log => log.WalletId == wallet.Id)
            }),
            teacherRechargeCards = teacherRecharges.GroupBy(x => new { x.WalletId, x.TeacherName }).Select(group => new { group.Key.WalletId, group.Key.TeacherName, amount = group.Sum(x => x.Amount), count = group.Count() }),
            transactions
        });
    }

    [HttpPost("wallet-transfers/reviews/{reviewId:guid}/expense")]
    [HasPermission("finance.expenses.create")]
    public async Task<ActionResult<object>> RecordWalletTransferExpense(Guid reviewId, [FromBody] WalletTransferExpenseBody body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.BeneficiaryName) || string.IsNullOrWhiteSpace(body.Reason))
            throw new InvalidOperationException("FINANCE_BENEFICIARY_AND_REASON_REQUIRED");
        var review = await db.WalletTransferReviews.SingleOrDefaultAsync(x => x.Id == reviewId, ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_REVIEW_NOT_FOUND");
        if (review.Status != NaderGorge.Domain.Entities.WalletTransferReviewStatus.PendingClassification)
            throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_ALREADY_CLASSIFIED");
        var sourceTreasuryId = await db.TreasuryAccounts.Where(x => x.DigitalWalletId == review.SourceWalletId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TREASURY_NOT_FOUND");
        var beneficiaryName = body.BeneficiaryName.Trim();
        var vendor = await db.FinanceVendors.SingleOrDefaultAsync(x => x.Name == beneficiaryName, ct);
        if (vendor is null)
        {
            vendor = new NaderGorge.Domain.Entities.FinanceVendor { Name = beneficiaryName, Phone = review.DestinationPhoneNumber };
            db.FinanceVendors.Add(vendor);
            await db.SaveChangesAsync(ct);
        }
        var totalDebited = review.Amount + review.ServiceFee;
        var description = $"تحويل محفظة إلى {beneficiaryName} ({review.DestinationPhoneNumber}) — {body.Reason.Trim()}";
        if (review.ServiceFee > 0m) description += $" (يشمل رسوم تحويل {review.ServiceFee:0.##} ج.م)";
        var expense = await operations.CreateExpenseAsync(new CreatePlatformExpenseRequest(totalDebited, review.OccurredAt, body.CategoryId, body.CostCenterId, vendor.Id, description, $"WLT-{review.Id:N}", CurrentUserId()), ct);
        await operations.PostExpenseAsync(expense.Id, new PostPlatformExpenseRequest(sourceTreasuryId, CurrentUserId(), $"wallet-expense-{review.Id:N}", description), ct);
        review.PlatformExpenseId = expense.Id;
        review.Status = NaderGorge.Domain.Entities.WalletTransferReviewStatus.RecordedAsExpense;
        review.ClassifiedByUserId = CurrentUserId();
        review.ClassifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { reviewId = review.Id, review.Status, expenseId = expense.Id, totalDebited });
    }

    [HttpPost("wallet-transfers/reviews/{reviewId:guid}/internal-transfer")]
    [HasPermission("finance.treasury.manage")]
    public async Task<ActionResult<object>> RecordWalletInternalTransfer(Guid reviewId, [FromBody] WalletInternalTransferBody body, CancellationToken ct)
    {
        var review = await db.WalletTransferReviews.SingleOrDefaultAsync(x => x.Id == reviewId, ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_REVIEW_NOT_FOUND");
        if (review.Status != NaderGorge.Domain.Entities.WalletTransferReviewStatus.PendingClassification)
            throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_ALREADY_CLASSIFIED");
        if (review.ServiceFee > 0m)
            throw new InvalidOperationException("FINANCE_WALLET_TRANSFER_FEE_REQUIRES_EXPENSE");
        var sourceTreasuryId = await db.TreasuryAccounts.Where(x => x.DigitalWalletId == review.SourceWalletId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FINANCE_WALLET_TREASURY_NOT_FOUND");
        var transfer = await planning.TransferAsync(new TreasuryTransferRequest(sourceTreasuryId, body.DestinationTreasuryAccountId, review.Amount,
            $"تحويل داخلي من رسالة محفظة {review.TransferReference ?? review.Id.ToString("N")}", CurrentUserId(), $"wallet-internal-{review.Id:N}"), ct);
        review.TreasuryTransferId = transfer.Id;
        review.Status = NaderGorge.Domain.Entities.WalletTransferReviewStatus.RecordedAsInternalTransfer;
        review.ClassifiedByUserId = CurrentUserId();
        review.ClassifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new { reviewId = review.Id, review.Status, transferId = transfer.Id });
    }

    [HttpPost("expenses/{expenseId:guid}/reverse")]
    [HasPermission("finance.expenses.post")]
    public async Task<ActionResult<object>> ReverseExpense(Guid expenseId, [FromBody] PeriodReasonBody body, CancellationToken ct)
    {
        var expense = await db.PlatformExpenses.SingleOrDefaultAsync(x => x.Id == expenseId, ct) ?? throw new InvalidOperationException("FINANCE_EXPENSE_NOT_FOUND");
        if (!expense.JournalEntryId.HasValue) throw new InvalidOperationException("FINANCE_EXPENSE_NOT_POSTED");
        var reversal = await posting.ReverseAsync(expense.JournalEntryId.Value, CurrentUserId(), body.Reason, ct);
        expense.Status = NaderGorge.Domain.Entities.PlatformExpenseStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return Ok(new { expense.Id, expense.Status, reversalId = reversal.Id });
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

    [HttpPost("refunds/external-package")]
    [HasPermission("finance.refunds.create")]
    public async Task<ActionResult<object>> CreateExternalPackageRefund([FromBody] ExternalPackageRefundBody body, CancellationToken ct)
    {
        var transaction = await db.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            var cancellation = await mediator.Send(
                new CancelPackageGrantCommand(body.AccessGrantId, false, CurrentUserId(), body.Reason), ct);
            if (!cancellation.Success)
            {
                await transaction.RollbackAsync(ct);
                return BadRequest(cancellation);
            }

            var refund = await operations.CreateRefundAsync(new CreatePlatformRefundRequest(
                body.PurchaseOperationId,
                "PurchaseOperation",
                body.StudentId,
                body.TeacherId,
                body.PlatformAmount,
                body.TeacherAmount,
                (int)NaderGorge.Domain.Entities.PlatformRefundMethod.Cash,
                body.TreasuryAccountId,
                body.Reason,
                body.PaymentReference,
                CurrentUserId()), ct);
            refund = await operations.PostRefundAsync(
                refund.Id,
                $"external-refund-{body.AccessGrantId}",
                CurrentUserId(),
                ct);
            await transaction.CommitAsync(ct);
            return Ok(new { refund.Id, refund.TotalAmount, refund.Status });
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    [HttpPost("refunds/{refundId:guid}/post")]
    [HasPermission("finance.refunds.post")]
    public async Task<ActionResult<object>> PostRefund(Guid refundId, [FromBody] PostRefundBody body, CancellationToken ct)
    {
        var refund = await operations.PostRefundAsync(refundId, body.IdempotencyKey, CurrentUserId(), ct);
        return Ok(new { refund.Id, refund.TotalAmount, refund.Status, refund.JournalEntryId });
    }

    [HttpGet("refunds")]
    [HasPermission("finance.refunds.view")]
    public async Task<ActionResult<object>> Refunds([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var query = db.PlatformRefunds.AsNoTracking().AsQueryable();
        if (from.HasValue) query = query.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) query = query.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));

        var recordedRefunds = await query
            .Select(x => new PlatformRefundListItem(
                x.Id,
                x.OriginalSourceId,
                x.OriginalSourceType,
                x.StudentId,
                db.Users.Where(user => user.Id == x.StudentId).Select(user => user.FullName).FirstOrDefault() ?? "طالب غير معروف",
                db.Users.Where(user => user.Id == x.StudentId).Select(user => user.PhoneNumber).FirstOrDefault() ?? string.Empty,
                x.TeacherId,
                x.PlatformAmount,
                x.TeacherAmount,
                x.PlatformAmount + x.TeacherAmount,
                (int)x.Method,
                (int)x.Status,
                x.Reason,
                x.JournalEntryId,
                x.CreatedAt,
                false))
            .ToListAsync(ct);

        var historicalQuery = db.BalanceTransactions
            .AsNoTracking()
            .Where(x => x.TransactionType == "Refund");
        if (from.HasValue) historicalQuery = historicalQuery.Where(x => x.CreatedAt >= from.Value.Date);
        if (to.HasValue) historicalQuery = historicalQuery.Where(x => x.CreatedAt < to.Value.Date.AddDays(1));

        var historicalRefunds = await historicalQuery
            .Select(x => new PlatformRefundListItem(
                x.Id,
                x.ReferenceId ?? x.Id,
                "BalanceTransaction",
                x.StudentBalance.UserId,
                x.StudentBalance.User.FullName,
                x.StudentBalance.User.PhoneNumber,
                null,
                x.Amount,
                0m,
                x.Amount,
                1,
                2,
                x.Description,
                null,
                x.CreatedAt,
                true))
            .ToListAsync(ct);

        return Ok(recordedRefunds
            .Concat(historicalRefunds)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500));
    }

    [HttpPost("refunds/{refundId:guid}/reverse")]
    [HasPermission("finance.refunds.post")]
    public async Task<ActionResult<object>> ReverseRefund(Guid refundId, [FromBody] PeriodReasonBody body, CancellationToken ct)
    {
        var refund = await db.PlatformRefunds.SingleOrDefaultAsync(x => x.Id == refundId, ct) ?? throw new InvalidOperationException("FINANCE_REFUND_NOT_FOUND");
        if (!refund.JournalEntryId.HasValue) throw new InvalidOperationException("FINANCE_REFUND_NOT_POSTED");
        var reversal = await posting.ReverseAsync(refund.JournalEntryId.Value, CurrentUserId(), body.Reason, ct);
        refund.Status = NaderGorge.Domain.Entities.PlatformRefundStatus.Reversed;
        await db.SaveChangesAsync(ct);
        return Ok(new { refund.Id, refund.Status, reversalId = reversal.Id });
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

    [HttpGet("reports/{kind}")]
    [HasPermission("finance.dashboard.view")]
    public Task<PlatformFinancialReportDto> Report(string kind, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => reports.GetAsync(kind, from, to, ct);

    [HttpGet("reconciliation")]
    [HasPermission("finance.ledger.view")]
    public Task<FinancialReconciliationReport> Reconciliation([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        => reconciliation.GetAsync(from, to, ct);

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
        => Ok(await periods.CloseAsync(periodId, CurrentUserId(), body.Reason, ct));

    [HttpPost("periods/{periodId:guid}/reopen")]
    [HasPermission("finance.periods.reopen")]
    public async Task<ActionResult<object>> ReopenPeriod(Guid periodId, [FromBody] PeriodReasonBody body, CancellationToken ct)
        => Ok(await periods.ReopenAsync(periodId, CurrentUserId(), body.Reason, ct));

    private Guid CurrentUserId()
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record CreateExpenseBody(decimal Amount, DateTime OccurredAt, Guid CategoryId, Guid? CostCenterId, Guid? VendorId, string Description, string? DocumentNumber);
public sealed record PostExpenseBody(Guid? TreasuryAccountId, string IdempotencyKey, string? Reason);
public sealed record WalletTransferExpenseBody(Guid CategoryId, Guid? CostCenterId, string BeneficiaryName, string Reason);
public sealed record WalletInternalTransferBody(Guid DestinationTreasuryAccountId);
public sealed record PayExpenseBody(Guid TreasuryAccountId, decimal Amount, string PaymentReference, string IdempotencyKey);
public sealed record CreateRefundBody(Guid OriginalSourceId, string OriginalSourceType, Guid StudentId, Guid? TeacherId, decimal PlatformAmount, decimal TeacherAmount, int Method, Guid? TreasuryAccountId, string Reason, string? PaymentReference);
public sealed record ExternalPackageRefundBody(Guid AccessGrantId, Guid PurchaseOperationId, Guid StudentId, Guid? TeacherId, decimal PlatformAmount, decimal TeacherAmount, Guid TreasuryAccountId, string Reason, string? PaymentReference);
public sealed record PostRefundBody(string IdempotencyKey);
public sealed record CreateBudgetLineBody(Guid FinancialAccountId, Guid? CostCenterId, Guid? TeacherId, decimal PlannedAmount);
public sealed record CreateBudgetBody(string Name, int PeriodKind, DateTime StartDate, DateTime EndDate, IReadOnlyList<CreateBudgetLineBody> Lines);
public sealed record TreasuryTransferBody(Guid SourceTreasuryAccountId, Guid DestinationTreasuryAccountId, decimal Amount, string Reference, string IdempotencyKey);
public sealed record TreasuryReconciliationBody(Guid TreasuryAccountId, DateTime AsOfDate, decimal CountedOrStatementBalance, string EvidenceNote);
public sealed record PeriodReasonBody(string Reason);
public sealed record PlatformRefundListItem(
    Guid Id,
    Guid OriginalSourceId,
    string OriginalSourceType,
    Guid StudentId,
    string StudentName,
    string StudentPhoneNumber,
    Guid? TeacherId,
    decimal PlatformAmount,
    decimal TeacherAmount,
    decimal TotalAmount,
    int Method,
    int Status,
    string Reason,
    Guid? JournalEntryId,
    DateTime CreatedAt,
    bool IsHistorical);

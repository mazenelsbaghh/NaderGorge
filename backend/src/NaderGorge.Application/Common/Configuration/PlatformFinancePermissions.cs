namespace NaderGorge.Application.Common.Configuration;

public static class PlatformFinancePermissions
{
    public const string DashboardView = "finance.dashboard.view";
    public const string LedgerView = "finance.ledger.view";
    public const string TeacherSummaryView = "finance.teacher-summary.view";
    public const string ExpensesCreate = "finance.expenses.create";
    public const string ExpensesView = "finance.expenses.view";
    public const string ExpensesPost = "finance.expenses.post";
    public const string RefundsCreate = "finance.refunds.create";
    public const string RefundsView = "finance.refunds.view";
    public const string RefundsPost = "finance.refunds.post";
    public const string TreasuryManage = "finance.treasury.manage";
    public const string TreasuryReconcile = "finance.treasury.reconcile";
    public const string BudgetsManage = "finance.budgets.manage";
    public const string Export = "finance.export";
    public const string PeriodClose = "finance.periods.close";
    public const string PeriodReopen = "finance.periods.reopen";
    public const string HistoricalMigration = "finance.migration.manage";

    public static IReadOnlyList<string> All { get; } =
    [
        DashboardView, LedgerView, TeacherSummaryView,
        ExpensesCreate, ExpensesView, ExpensesPost, RefundsCreate, RefundsView, RefundsPost,
        TreasuryManage, TreasuryReconcile, BudgetsManage, Export,
        PeriodClose, PeriodReopen, HistoricalMigration
    ];
}

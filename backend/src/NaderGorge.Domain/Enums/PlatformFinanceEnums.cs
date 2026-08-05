namespace NaderGorge.Domain.Enums;

public enum FinancialAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    ContraRevenue = 5,
    Expense = 6
}

public enum FinancialNormalSide
{
    Debit = 1,
    Credit = 2
}

public enum FinancialAccountRole
{
    None = 0,
    Treasury = 1,
    GeneralStudentLiability = 2,
    TeacherStudentLiability = 3,
    TeacherPayable = 4,
    SupplierPayable = 5,
    PlatformRevenue = 6,
    Refunds = 7,
    OperatingExpense = 8,
    PayrollExpense = 9,
    OpeningSuspense = 10
}

public enum JournalEntryStatus
{
    Posted = 1,
    Reversed = 2
}

public enum TreasuryAccountType
{
    DigitalWallet = 1,
    Cashbox = 2,
    BankAccount = 3
}

public enum AccountingPeriodStatus
{
    Open = 1,
    Closed = 2,
    Reopened = 3
}

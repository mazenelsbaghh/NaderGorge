using NaderGorge.Application.Services;

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record TeacherStatementRow(
    Guid Id, string Kind, DateTime OccurredAt, string Title, string Detail, string Status,
    string? Reference = null, decimal? GrossAmount = null, decimal? DiscountAmount = null,
    decimal? SalePaidAmount = null, decimal? TeacherShareAmount = null,
    decimal? PlatformShareAmount = null, decimal? TeacherPaymentAmount = null,
    decimal? PlatformDueAmount = null, decimal? PlatformPaymentAmount = null,
    decimal? StudentCollectionAmount = null, decimal? AdjustmentAmount = null,
    bool Recognized = false, bool RetainedByTeacher = false,
    decimal? StudentRefundAmount = null);

public sealed record TeacherStatementActivity(
    int PurchasingStudents, int PurchaseOperations, decimal PurchaseValue,
    int RechargeStudents, int RechargeOperations, decimal RechargeAmount,
    int VodafoneCashStudents, int VodafoneCashOperations, decimal VodafoneCashAmount,
    decimal OtherRechargeAmount, int RefundedStudents, int RefundOperations, decimal RefundAmount,
    int ActivatedCodes, int CodeStudents, decimal ActivatedCodeValue);

public sealed record TeacherStatementTotals(
    decimal Earned, decimal PendingEarnings, decimal TeacherPayments, decimal RetainedEarnings,
    decimal PlatformCodeDue, decimal PlatformCodePayments, decimal StudentCollections,
    decimal OpenDebtAdjustments, decimal PlatformEarned = 0m);

public sealed record TeacherStatementSale(int Students, int Operations, decimal UnitPrice,
    decimal Total, decimal TeacherShare, decimal PlatformShare, decimal? PlatformPercent);

public sealed record TeacherStatementCodeBatch(string Name, int Codes, decimal? Value,
    decimal? PlatformDue, decimal Collected, decimal? Remaining);

public sealed record TeacherStatement(
    Guid TeacherId, string TeacherName, DateTime? From, DateTime? To, DateTime GeneratedAt,
    TeacherFinanceAccountSnapshot Account, TeacherStatementTotals Totals, TeacherStatementActivity Activity,
    IReadOnlyList<TeacherStatementRow> Items, int Total, int Page, int PageSize,
    IReadOnlyList<TeacherStatementSale> Sales, IReadOnlyList<TeacherStatementCodeBatch> CodeBatches);

public interface ITeacherStatementService
{
    Task<TeacherStatement?> GetAsync(Guid teacherId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct);
    Task<FinanceExportResult?> ExportPdfAsync(Guid teacherId, DateTime? from, DateTime? to, CancellationToken ct);
}

namespace NaderGorge.Application.Interfaces.Finance;

public sealed record TeacherReportPeriod(DateOnly? From, DateOnly To);
public sealed record TeacherReportPurchase(Guid OperationId, Guid? StudentId, string Student,
    Guid CourseId, string Course, string Kind, string Content, DateTime At, string Status,
    decimal Paid, decimal Teacher, decimal Platform, bool Counted, bool Retained,
    Guid? GrantId, DateTime? CancelledAt, string? CancellationReason);
public sealed record TeacherReportGift(Guid StudentId, string Student, Guid CourseId, string Course,
    string Kind, string Content, DateTime At, string Status);
public sealed record TeacherReportRecharge(Guid StudentId, string Student, decimal Amount, DateTime At);
public sealed record TeacherReportFunding(Guid StudentId, string Student, string Source, decimal Added,
    decimal Used, decimal Remaining, DateTime At, bool? Paid);
public sealed record TeacherReportRefund(Guid StudentId, string Student, Guid? GrantId, Guid SourceId,
    decimal Amount, DateTime At, string Method, string Reason);
public sealed record TeacherReportPayment(decimal Amount, DateTime At, string Method, string? Reference);
public sealed record TeacherReportMovement(DateTime At, string Description, decimal Teacher, decimal Platform,
    string Status, string Reference);
public sealed record TeacherReportSummary(decimal Opening, decimal Earned, decimal Platform,
    decimal Retained, decimal Paid, decimal Adjustments, decimal Closing);
public sealed record TeacherDetailedReport(string TeacherName, TeacherReportPeriod Period,
    TeacherReportSummary Summary, IReadOnlyList<string> Agreements, IReadOnlyList<string> Notes,
    IReadOnlyList<TeacherReportPurchase> Purchases, IReadOnlyList<TeacherReportGift> Gifts,
    IReadOnlyList<TeacherReportRecharge> Recharges, IReadOnlyList<TeacherReportFunding> Funding,
    IReadOnlyList<TeacherReportRefund> Refunds, IReadOnlyList<TeacherReportPayment> Payments,
    IReadOnlyList<TeacherReportPurchase> Cancellations, IReadOnlyList<TeacherReportMovement> Movements);

public interface ITeacherDetailedReportService
{
    Task<FinanceExportResult?> ExportAsync(Guid teacherId, TeacherReportPeriod period, CancellationToken ct);
}

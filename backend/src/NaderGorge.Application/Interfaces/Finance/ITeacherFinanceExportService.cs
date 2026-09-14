namespace NaderGorge.Application.Interfaces.Finance;

public interface ITeacherFinanceExportService
{
    Task<FinanceExportResult> ExportDayAsync(Guid teacherUserId, DateTime date, CancellationToken ct);
}

using System.Text.Json;

namespace NaderGorge.Application.Features.Reporting;

public static class ReportDomains
{
    public const string StudentJourney = "student-journey";
    public const string Students = "students";
    public const string Purchases = "purchases";
    public const string Codes = "codes";
    public const string BalanceRecharge = "balance-recharge";
    public const string Content = "content";
    public const string Engagement = "engagement";
    public const string Attendance = "attendance";
    public const string Assessments = "assessments";
    public const string TeachersFinance = "teachers-finance";
    public const string Staff = "staff";
    public const string Support = "support";
    public const string CommentsCommunity = "comments-community";
    public const string ParentTracking = "parent-tracking";
    public const string OperationsSecurity = "operations-security";
}

public sealed record ReportFilter(string Field, string Operator, IReadOnlyList<JsonElement>? Values);

public sealed record ReportFilterGroup(
    string Logic,
    IReadOnlyList<ReportFilter>? Filters = null,
    IReadOnlyList<ReportFilterGroup>? Groups = null);

public sealed record ReportSort(string Field, string Direction = "asc");

public sealed record ExecuteReportRequest(
    string Domain,
    ReportFilterGroup? FilterGroup = null,
    IReadOnlyList<string>? Columns = null,
    ReportSort? Sort = null,
    int Page = 1,
    int PageSize = 25);

public sealed record ReportFieldDto(
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string> Operators,
    bool IsSensitive = false);

public sealed record ReportDomainDto(
    string Key,
    string Label,
    string Description,
    bool IsAvailable,
    IReadOnlyList<ReportFieldDto> Fields,
    IReadOnlyList<string> DefaultColumns);

public sealed record ReportCatalogDto(IReadOnlyList<ReportDomainDto> Domains, string TimeZone);

public sealed record ReportFilterOptionDto(string Field, string Value, string Label);
public sealed record ReportFilterOptionsDto(IReadOnlyList<ReportFilterOptionDto> Options);

public sealed record ReportColumnDto(string Key, string Label, string Type);
public sealed record ReportMetricDto(string Key, string Label, decimal Value);
public sealed record ReportChartPointDto(string Label, decimal Value);
public sealed record ReportChartDto(string Type, string Label, IReadOnlyList<ReportChartPointDto> Points);

public sealed record ReportResultDto(
    string Domain,
    DateTime GeneratedAtCairo,
    IReadOnlyList<ReportMetricDto> Summary,
    ReportChartDto Chart,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<string> AppliedFilters,
    bool IsTruncated,
    string? Notice);

public sealed record SaveReportDefinitionRequest(string Name, ExecuteReportRequest Configuration);
public sealed record UpdateReportDefinitionRequest(string Name, ExecuteReportRequest Configuration, uint? Version = null);
public sealed record CopyReportDefinitionRequest(string? Name = null);
public sealed record ReportDefinitionDto(
    Guid Id,
    string Name,
    string Domain,
    ExecuteReportRequest Configuration,
    int SchemaVersion,
    uint Version,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ReportExportDto(byte[] Content, string ContentType, string FileName);

public interface IReportExportService
{
    Task<ReportExportDto> ExportAsync(string format, ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct);
}

public interface IStudentLedgerExportService
{
    Task<ReportExportDto> ExportAsync(Guid teacherId, Guid actorUserId, CancellationToken ct);
    Task<ReportExportDto> ExportForTeacherAsync(Guid actorUserId, CancellationToken ct);
}

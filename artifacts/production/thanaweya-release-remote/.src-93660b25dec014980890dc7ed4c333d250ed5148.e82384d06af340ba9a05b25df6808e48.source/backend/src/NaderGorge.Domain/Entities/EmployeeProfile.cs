using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public class EmployeeProfile : BaseEntity
{
    private static readonly TimeZoneInfo CairoTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public EmployeeEmploymentStatus EmploymentStatus { get; set; } = EmployeeEmploymentStatus.Active;
    public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CairoTimeZone));
    public DateOnly? TerminationDate { get; set; }
    public EmployeeWorkMode WorkMode { get; set; } = EmployeeWorkMode.OnSite;

    public decimal BasicSalary { get; set; }
    public TimeSpan StandardStartTime { get; set; } = new TimeSpan(9, 0, 0); // Default to 09:00 AM
    public int TargetDailyHours { get; set; } = 8; // Default to 8 hours
    public int DailyBreakAllowanceMinutes { get; set; } = 30;
    public int ShortPermissionMaxMinutes { get; set; } = 5;
    public int DailyShortPermissionAllowanceMinutes { get; set; } = 15;

    public static string GenerateEmployeeNumber(Guid profileId) =>
        $"EMP-{profileId:N}".ToUpperInvariant();
}

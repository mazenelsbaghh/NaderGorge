using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Attendance;

public sealed record AttendanceEvaluationInput(Guid EmployeeId, Guid ShiftTemplateId, DateTime OccurredAt,
    double? Latitude, double? Longitude, double? AccuracyMeters, string? DeviceToken);
public sealed record AttendanceEvaluationResult(bool Accepted, string Code, Guid? PolicyId, string Source);

public sealed class AttendancePolicyEvaluator
{
    private readonly IAppDbContext _db;
    public AttendancePolicyEvaluator(IAppDbContext db) => _db = db;

    public async Task<AttendanceEvaluationResult> EvaluateAsync(AttendanceEvaluationInput input, CancellationToken ct)
    {
        var occurredAt = input.OccurredAt.Kind == DateTimeKind.Utc ? input.OccurredAt : input.OccurredAt.ToUniversalTime();
        var exception = await _db.AttendancePolicyExceptions.AsNoTracking()
            .Where(item => item.EmployeeId == input.EmployeeId && item.StartsAt <= occurredAt && item.EndsAt >= occurredAt)
            .OrderByDescending(item => item.StartsAt).FirstOrDefaultAsync(ct);
        if (exception?.AllowRemote == true) return new(true, "REMOTE_EXCEPTION", exception.OverridePolicyId, "employee-exception");

        var workDate = CairoTime.ToDate(occurredAt);
        var assignment = await _db.AttendancePolicyAssignments.AsNoTracking()
            .Include(item => item.AttendancePolicy)
            .Where(item => item.AttendancePolicy!.IsActive && item.EffectiveFrom <= workDate && (!item.EffectiveTo.HasValue || item.EffectiveTo > workDate) &&
                (item.EmployeeId == input.EmployeeId || (!item.EmployeeId.HasValue && item.ShiftTemplateId == input.ShiftTemplateId)))
            .OrderByDescending(item => item.EmployeeId.HasValue)
            .ThenByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        var policy = assignment?.AttendancePolicy;
        // A published shift without an explicit policy is intentionally unrestricted.
        // Explicit employee/shift policies still take precedence and enforce their rules.
        if (policy is null) return new(true, "ATTENDANCE_ACCEPTED", null, "shift-default");
        if (policy.Kind == AttendancePolicyKind.Unrestricted) return new(true, "ATTENDANCE_ACCEPTED", policy.Id, assignment!.EmployeeId.HasValue ? "employee" : "shift");
        if (policy.Kind == AttendancePolicyKind.Geofence)
        {
            if (!input.Latitude.HasValue || !input.Longitude.HasValue || !input.AccuracyMeters.HasValue || input.AccuracyMeters > policy.MaximumAccuracyMeters)
                return new(false, "LOCATION_ACCURACY_LOW", policy.Id, "geofence");
            if (!policy.Latitude.HasValue || !policy.Longitude.HasValue || DistanceMeters(
                    (double)policy.Latitude.Value, (double)policy.Longitude.Value, input.Latitude.Value, input.Longitude.Value) > policy.RadiusMeters)
                return new(false, "OUTSIDE_GEOFENCE", policy.Id, "geofence");
            return new(true, "ATTENDANCE_ACCEPTED", policy.Id, "geofence");
        }
        if (string.IsNullOrWhiteSpace(input.DeviceToken)) return new(false, "DEVICE_NOT_TRUSTED", policy.Id, "trusted-device");
        var hash = HashToken(input.DeviceToken);
        var trusted = await _db.TrustedAttendanceDevices.AsNoTracking().AnyAsync(item => item.EmployeeId == input.EmployeeId &&
            item.TokenHash == hash && item.IsActive && (!item.ExpiresAt.HasValue || item.ExpiresAt > occurredAt), ct);
        return trusted ? new(true, "ATTENDANCE_ACCEPTED", policy.Id, "trusted-device") : new(false, "DEVICE_NOT_TRUSTED", policy.Id, "trusted-device");
    }

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earth = 6371000;
        var dLat = DegreesToRadians(lat2 - lat1); var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earth * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
    private static double DegreesToRadians(double value) => value * Math.PI / 180;
}

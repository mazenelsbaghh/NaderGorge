using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

public sealed class AttendancePolicy : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AttendancePolicyKind Kind { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int RadiusMeters { get; set; } = 150;
    public int MaximumAccuracyMeters { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

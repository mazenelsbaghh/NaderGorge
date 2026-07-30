using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class WorkLocation : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public int? GeofenceRadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
}

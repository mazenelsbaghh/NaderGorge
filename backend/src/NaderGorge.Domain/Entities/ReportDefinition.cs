using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

/// <summary>
/// A user-owned, versioned definition of a report. The JSON payload is validated by
/// the reporting application service and never executed as a database expression.
/// </summary>
public sealed class ReportDefinition : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = "{}";
    public int SchemaVersion { get; set; } = 1;
    public uint Version { get; set; }
}

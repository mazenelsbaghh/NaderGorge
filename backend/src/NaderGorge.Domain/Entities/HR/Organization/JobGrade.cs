using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public sealed class JobGrade : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsActive { get; set; } = true;
}

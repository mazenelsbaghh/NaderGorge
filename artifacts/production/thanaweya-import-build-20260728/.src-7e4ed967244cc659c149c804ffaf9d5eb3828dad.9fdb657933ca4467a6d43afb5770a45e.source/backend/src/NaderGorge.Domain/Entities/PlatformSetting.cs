using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public class PlatformSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

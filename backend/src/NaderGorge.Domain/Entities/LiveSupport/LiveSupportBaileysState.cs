using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities.LiveSupport;

public sealed class LiveSupportBaileysAuth : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
}

public sealed class LiveSupportBaileysCallback : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Ciphertext { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
}

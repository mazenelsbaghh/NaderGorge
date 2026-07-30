namespace NaderGorge.Domain.Entities;

public sealed class ClusterLease
{
    public string Name { get; set; } = string.Empty;
    public Guid OwnerToken { get; set; }
    public long FencingGeneration { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime RenewedAt { get; set; }
    public string? LastOutcome { get; set; }
}

namespace NaderGorge.Application.Interfaces;

public sealed record ClusterLeaseClaim(
    string Name,
    Guid OwnerToken,
    long FencingGeneration,
    DateTime ExpiresAt);

public interface IClusterLeaseService
{
    Task<ClusterLeaseClaim?> TryAcquireAsync(
        string name,
        Guid ownerToken,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<bool> RenewAsync(
        ClusterLeaseClaim claim,
        TimeSpan lifetime,
        string? outcome,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        ClusterLeaseClaim claim,
        string? outcome,
        CancellationToken cancellationToken);
}

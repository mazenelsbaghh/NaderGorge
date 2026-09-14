namespace NaderGorge.Application.Interfaces;

public sealed record UserSecurityState(
    bool IsActive,
    int PasswordResetVersion,
    int SecurityStampVersion);

public enum UserSecurityStateCacheStatus
{
    Hit,
    Miss,
    Unavailable
}

public sealed record UserSecurityStateCacheLookup(
    UserSecurityStateCacheStatus Status,
    UserSecurityState? State);

public interface IUserSecurityStateCache
{
    Task<UserSecurityStateCacheLookup> GetAsync(Guid userId, CancellationToken ct);
    Task SetAsync(Guid userId, UserSecurityState state, CancellationToken ct);
    Task RemoveAsync(Guid userId, CancellationToken ct);
}

public interface IUserSecurityStateSource
{
    Task<UserSecurityState?> GetAsync(Guid userId, CancellationToken ct);
}

public interface IUserSecurityStateResolver
{
    Task<UserSecurityState?> ResolveAsync(Guid userId, CancellationToken ct);
}

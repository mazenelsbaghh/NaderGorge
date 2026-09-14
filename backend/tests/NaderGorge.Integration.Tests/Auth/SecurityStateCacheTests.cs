using NaderGorge.Application.Features.Auth.Services;
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Integration.Tests.Auth;

public sealed class SecurityStateCacheTests
{
    private static readonly Guid UserId = Guid.Parse(
        "11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task Cache_hit_keeps_authentication_available_when_source_is_down()
    {
        var cached = new UserSecurityState(
            IsActive: true,
            PasswordResetVersion: 3,
            SecurityStampVersion: 7);
        var resolver = new UserSecurityStateResolver(
            new MemorySecurityStateCache(cached),
            new ThrowingSecurityStateSource());

        var resolved = await resolver.ResolveAsync(UserId, default);

        Assert.Equal(cached, resolved);
    }

    [Fact]
    public async Task Cache_miss_reads_authoritative_state_and_warms_cache()
    {
        var authoritative = new UserSecurityState(
            IsActive: true,
            PasswordResetVersion: 4,
            SecurityStampVersion: 8);
        var cache = new MemorySecurityStateCache();
        var resolver = new UserSecurityStateResolver(
            cache,
            new FixedSecurityStateSource(authoritative));

        var resolved = await resolver.ResolveAsync(UserId, default);

        Assert.Equal(authoritative, resolved);
        Assert.Equal(authoritative, cache.State);
    }

    [Fact]
    public async Task Cache_outage_falls_back_to_authoritative_state()
    {
        var authoritative = new UserSecurityState(
            IsActive: true,
            PasswordResetVersion: 5,
            SecurityStampVersion: 9);
        var resolver = new UserSecurityStateResolver(
            new MemorySecurityStateCache(
                emptyStatus: UserSecurityStateCacheStatus.Unavailable),
            new FixedSecurityStateSource(authoritative));

        var resolved = await resolver.ResolveAsync(UserId, default);

        Assert.Equal(authoritative, resolved);
    }

    [Fact]
    public async Task Revocation_invalidation_rejects_the_next_resolution_immediately()
    {
        var active = new UserSecurityState(
            IsActive: true,
            PasswordResetVersion: 1,
            SecurityStampVersion: 1);
        var revoked = active with { IsActive = false, SecurityStampVersion = 2 };
        var cache = new MemorySecurityStateCache(active);
        var source = new FixedSecurityStateSource(revoked);
        var resolver = new UserSecurityStateResolver(cache, source);

        Assert.True((await resolver.ResolveAsync(UserId, default))?.IsActive);

        await cache.RemoveAsync(UserId, default);
        var resolvedAfterRevocation = await resolver.ResolveAsync(
            UserId,
            default);

        Assert.NotNull(resolvedAfterRevocation);
        Assert.False(resolvedAfterRevocation.IsActive);
        Assert.Equal(revoked, cache.State);
    }

    private sealed class MemorySecurityStateCache(
        UserSecurityState? state = null,
        UserSecurityStateCacheStatus emptyStatus =
            UserSecurityStateCacheStatus.Miss) : IUserSecurityStateCache
    {
        public UserSecurityState? State { get; private set; } = state;

        public Task<UserSecurityStateCacheLookup> GetAsync(
            Guid userId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(
                State is null
                    ? new UserSecurityStateCacheLookup(emptyStatus, null)
                    : new UserSecurityStateCacheLookup(
                        UserSecurityStateCacheStatus.Hit,
                        State));
        }

        public Task SetAsync(
            Guid userId,
            UserSecurityState newState,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            State = newState;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid userId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            State = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedSecurityStateSource(UserSecurityState? state)
        : IUserSecurityStateSource
    {
        public Task<UserSecurityState?> GetAsync(
            Guid userId,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(state);
        }
    }

    private sealed class ThrowingSecurityStateSource
        : IUserSecurityStateSource
    {
        public Task<UserSecurityState?> GetAsync(
            Guid userId,
            CancellationToken ct) =>
            throw new InvalidOperationException(
                "The authoritative source is unavailable.");
    }
}

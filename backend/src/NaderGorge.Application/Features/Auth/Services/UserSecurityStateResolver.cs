using NaderGorge.Application.Interfaces;

namespace NaderGorge.Application.Features.Auth.Services;

public sealed class UserSecurityStateResolver(
    IUserSecurityStateCache cache,
    IUserSecurityStateSource source) : IUserSecurityStateResolver
{
    private readonly IUserSecurityStateCache _cache = cache;
    private readonly IUserSecurityStateSource _source = source;

    public async Task<UserSecurityState?> ResolveAsync(
        Guid userId,
        CancellationToken ct)
    {
        var lookup = await _cache.GetAsync(userId, ct);
        if (lookup is
            {
                Status: UserSecurityStateCacheStatus.Hit,
                State: not null
            })
        {
            return lookup.State;
        }

        var state = await _source.GetAsync(userId, ct);
        if (state is not null)
        {
            await _cache.SetAsync(userId, state, ct);
        }

        return state;
    }
}

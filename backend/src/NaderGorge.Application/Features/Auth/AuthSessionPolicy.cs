namespace NaderGorge.Application.Features.Auth;

public static class AuthSessionPolicy
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);
}

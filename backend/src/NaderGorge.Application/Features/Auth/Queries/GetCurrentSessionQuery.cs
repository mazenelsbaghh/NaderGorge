using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Auth.Commands;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Auth.Queries;

public sealed record GetCurrentSessionQuery(Guid UserId) : IRequest<ApiResponse<CurrentSessionSnapshot>>;

public sealed record CurrentSessionSnapshot(UserDto User, int AuthorizationVersion, DateTime ServerTime);

public sealed class GetCurrentSessionQueryHandler : IRequestHandler<GetCurrentSessionQuery, ApiResponse<CurrentSessionSnapshot>>
{
    private readonly IAppDbContext _db;

    public GetCurrentSessionQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<CurrentSessionSnapshot>> Handle(GetCurrentSessionQuery request, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.StudentProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct);

        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("Current session is no longer active.");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).Distinct().ToArray();
        var permissions = user.UserRoles.SelectMany(ur => ParseArray(ur.Role.PermissionsJson)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allowedDomains = user.UserRoles.Select(ur => ur.Role.AllowedDomain).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var allowedNavbarItems = user.UserRoles.SelectMany(ur => ParseArray(ur.Role.AllowedNavbarItemsJson)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var dto = new UserDto(user.Id, user.FullName, user.PhoneNumber, roles, permissions, user.IsProfileComplete, user.StudentProfile?.AvatarSlug, allowedDomains, allowedNavbarItems, user.SecurityStampVersion);
        return ApiResponse<CurrentSessionSnapshot>.Ok(new CurrentSessionSnapshot(dto, user.SecurityStampVersion, DateTime.UtcNow));
    }

    private static IEnumerable<string> ParseArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch (System.Text.Json.JsonException) { return Array.Empty<string>(); }
    }
}

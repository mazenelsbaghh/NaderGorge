using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Cache;

public sealed class EfUserSecurityStateSource(IAppDbContext db)
    : IUserSecurityStateSource
{
    private readonly IAppDbContext _db = db;

    public Task<UserSecurityState?> GetAsync(
        Guid userId,
        CancellationToken ct) =>
        _db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new UserSecurityState(
                item.IsActive,
                item.PasswordResetVersion,
                item.SecurityStampVersion))
            .FirstOrDefaultAsync(ct);
}

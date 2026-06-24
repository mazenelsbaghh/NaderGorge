using System.Security.Claims;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    string GenerateAccessToken(User user, IEnumerable<string> roles, TimeSpan lifetime);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    string GenerateParentToken(Guid studentId);
}

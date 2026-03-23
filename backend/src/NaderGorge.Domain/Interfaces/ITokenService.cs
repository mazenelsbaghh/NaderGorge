using System.Security.Claims;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupport.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services;

public sealed class LiveSupportGuestSessionService(IAppDbContext db) : ILiveSupportGuestSessionService
{
    public async Task<LiveSupportGuestSessionDto> IssueAsync(string displayName, string phoneNumber, string ipAddress, string? userAgent, CancellationToken ct)
    {
        displayName = displayName.Trim();
        phoneNumber = phoneNumber.Trim();
        if (displayName.Length is < 2 or > 100 || phoneNumber.Length is < 8 or > 20 || !phoneNumber.All(c => char.IsDigit(c) || c is '+' or ' ' or '-'))
            throw new LiveSupportException("VALIDATION_ERROR", "الاسم أو رقم الهاتف غير صحيح.");
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session = new LiveSupportGuestSession
        {
            DisplayName = displayName,
            PhoneNumber = phoneNumber,
            SecurityStampHash = Hash(secret),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastSeenAt = DateTime.UtcNow,
            CreatedIpHash = Hash(ipAddress),
            UserAgentSummary = userAgent?[..Math.Min(userAgent.Length, 240)]
        };
        db.LiveSupportGuestSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return new(session.Id, session.DisplayName, session.ExpiresAt, $"{session.Id:N}.{secret}");
    }

    public async Task<LiveSupportParticipantIdentity?> ValidateAsync(string? token, CancellationToken ct)
    {
        var parsed = Parse(token);
        if (parsed is null) return null;
        var (id, secret) = parsed.Value;
        var session = await db.LiveSupportGuestSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (session is null || session.RevokedAt.HasValue || session.ExpiresAt <= DateTime.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(session.SecurityStampHash), Convert.FromHexString(Hash(secret)))) return null;
        session.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(LiveSupportParticipantType.Guest, null, session.Id);
    }

    public async Task RevokeAsync(string? token, CancellationToken ct)
    {
        var parsed = Parse(token);
        if (parsed is null) return;
        var session = await db.LiveSupportGuestSessions.FirstOrDefaultAsync(x => x.Id == parsed.Value.Id, ct);
        if (session is null || session.RevokedAt.HasValue) return;
        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static (Guid Id, string Secret)? Parse(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.', 2);
        return parts.Length == 2 && Guid.TryParseExact(parts[0], "N", out var id) && parts[1].Length == 64 ? (id, parts[1]) : null;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

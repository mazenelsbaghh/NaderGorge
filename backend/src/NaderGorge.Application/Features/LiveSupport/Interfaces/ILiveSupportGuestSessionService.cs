using NaderGorge.Application.Features.LiveSupport.Dtos;

namespace NaderGorge.Application.Features.LiveSupport.Interfaces;

public interface ILiveSupportGuestSessionService
{
    Task<LiveSupportGuestSessionDto> IssueAsync(string displayName, string phoneNumber, string ipAddress, string? userAgent, CancellationToken ct);
    Task<LiveSupportParticipantIdentity?> ValidateAsync(string? token, CancellationToken ct);
    Task RevokeAsync(string? token, CancellationToken ct);
}

using System.Security.Claims;
using NaderGorge.Application.Common.HR;

namespace NaderGorge.API.Services;

public sealed class HttpHrRequestContext : IHrRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpHrRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? ActorUserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var actorUserId) ? actorUserId : null;
        }
    }

    public string CorrelationId =>
        _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
        ?? Guid.NewGuid().ToString("N");

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string RequestId =>
        _httpContextAccessor.HttpContext?.TraceIdentifier ?? CorrelationId;
}

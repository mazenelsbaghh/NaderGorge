using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.Admin.Commands;

public record RejectWatchRequestCommand(Guid RequestId, string Reason) : IRequest<ApiResponse<bool>>;

public class RejectWatchRequestCommandHandler : IRequestHandler<RejectWatchRequestCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _context;

    public RejectWatchRequestCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<bool>> Handle(RejectWatchRequestCommand request, CancellationToken cancellationToken)
    {
        var req = await _context.ExtraWatchRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken);

        if (req == null)
            return ApiResponse<bool>.Fail("Request not found", new List<string> { "NOT_FOUND" });

        if (req.Status != RequestStatus.Pending)
            return ApiResponse<bool>.Fail("Request is already resolved", new List<string> { "ALREADY_RESOLVED" });

        var reason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse<bool>.Fail("Reason is required", new List<string> { "INVALID_REASON" });

        req.Status = RequestStatus.Rejected;
        req.ResolvedAt = DateTime.UtcNow;
        req.RejectionReason = reason;

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.Ok(true);
    }
}

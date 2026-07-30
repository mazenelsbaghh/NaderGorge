using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Commands;

public record AcknowledgeTrackingPopupCommand(Guid UserId) : IRequest<ApiResponse<bool>>;

public class AcknowledgeTrackingPopupCommandHandler : IRequestHandler<AcknowledgeTrackingPopupCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public AcknowledgeTrackingPopupCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(AcknowledgeTrackingPopupCommand request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<bool>.Fail("ملف الطالب غير موجود");
        }

        profile.HasSeenTrackingCodePopup = true;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}

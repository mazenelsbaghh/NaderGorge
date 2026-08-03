using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public record GetAdminRechargeRequestsQuery(RechargeRequestStatus? Status = null) : IRequest<ApiResponse<List<AdminRechargeRequestDto>>>;

public class GetAdminRechargeRequestsQueryHandler : IRequestHandler<GetAdminRechargeRequestsQuery, ApiResponse<List<AdminRechargeRequestDto>>>
{
    private readonly IAppDbContext _db;

    public GetAdminRechargeRequestsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<AdminRechargeRequestDto>>> Handle(GetAdminRechargeRequestsQuery request, CancellationToken ct)
    {
        await RechargeRequestExpiryService.RejectPendingOlderThan48Hours(_db, ct);

        var query = _db.RechargeRequests
            .Include(r => r.User)
            .Include(r => r.Wallet)
            .Include(r => r.Teacher!).ThenInclude(t => t.User)
            .Include(r => r.ResolvedByUser)
            // A pending reservation is created before the student uploads the proof.
            // It is not an admin-reviewable request until the required evidence is submitted.
            .Where(r => r.ScreenshotUrl != null && r.ScreenshotUrl != "" && r.SenderPhoneNumber != "")
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AdminRechargeRequestDto
            {
                Id = r.Id,
                UserId = r.UserId,
                StudentName = r.User.FullName,
                StudentPhoneNumber = r.User.PhoneNumber,
                WalletId = r.WalletId,
                WalletLabel = r.Wallet.Label,
                WalletPhoneNumber = r.Wallet.PhoneNumber,
                Amount = r.Amount,
                TeacherId = r.TeacherId,
                TeacherName = r.Teacher != null && r.Teacher.User != null ? r.Teacher.User.FullName : null,
                SenderPhoneNumber = r.SenderPhoneNumber,
                ScreenshotUrl = r.ScreenshotUrl,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt,
                ResolvedByUserId = r.ResolvedByUserId,
                ResolvedByUserName = r.ResolvedByUser != null ? r.ResolvedByUser.FullName : null,
                RejectionReason = r.RejectionReason,
                MatchedSmsLogId = r.MatchedSmsLogId,
                ReservationExpiresAt = r.ReservationExpiresAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<AdminRechargeRequestDto>>.Ok(results);
    }
}

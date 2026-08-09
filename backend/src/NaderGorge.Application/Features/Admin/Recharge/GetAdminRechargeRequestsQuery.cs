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
        await RechargeRequestExpiryService.ResolveExpiredPendingRequests(_db, ct);
        var now = DateTime.UtcNow;

        var query = _db.RechargeRequests
            .Include(r => r.User)
            .Include(r => r.Wallet)
            .Include(r => r.Teacher!).ThenInclude(t => t.User)
            .Include(r => r.ResolvedByUser)
            .Where(r => r.Status == RechargeRequestStatus.Pending ||
                r.Status == RechargeRequestStatus.Cancelled ||
                (r.ScreenshotUrl != null && r.ScreenshotUrl != "" && r.SenderPhoneNumber != ""))
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
                StudentBalance = _db.StudentBalances
                    .Where(balance => balance.UserId == r.UserId)
                    .Select(balance => balance.CurrentBalance)
                    .FirstOrDefault(),
                TeacherBalance = r.TeacherId.HasValue
                    ? _db.PromotionalBalanceAllocations
                        .Where(allocation => allocation.StudentId == r.UserId
                            && allocation.TeacherId == r.TeacherId
                            && allocation.Status == PromotionalBalanceStatus.Active
                            && (!allocation.ExpiresAt.HasValue || allocation.ExpiresAt > now))
                        .Sum(allocation => allocation.AvailableAmount)
                    : 0m,
                HasPreviousRequest = _db.RechargeRequests.Any(previous =>
                    previous.UserId == r.UserId && previous.Id != r.Id && previous.CreatedAt < r.CreatedAt),
                PreviousRequestStatus = _db.RechargeRequests
                    .Where(previous => previous.UserId == r.UserId && previous.Id != r.Id && previous.CreatedAt < r.CreatedAt)
                    .OrderByDescending(previous => previous.CreatedAt)
                    .Select(previous => (RechargeRequestStatus?)previous.Status)
                    .FirstOrDefault(),
                PreviousRequestCreatedAt = _db.RechargeRequests
                    .Where(previous => previous.UserId == r.UserId && previous.Id != r.Id && previous.CreatedAt < r.CreatedAt)
                    .OrderByDescending(previous => previous.CreatedAt)
                    .Select(previous => (DateTime?)previous.CreatedAt)
                    .FirstOrDefault(),
                WalletId = r.WalletId,
                WalletLabel = r.Wallet.Label,
                WalletPhoneNumber = r.Wallet.PhoneNumber,
                Amount = r.Amount,
                TeacherId = r.TeacherId,
                TeacherName = r.Teacher != null && r.Teacher.User != null ? r.Teacher.User.FullName : null,
                SenderPhoneNumber = r.SenderPhoneNumber,
                OriginalSenderPhoneNumber = r.OriginalSenderPhoneNumber,
                RequiresSenderPhoneConfirmation = r.RequiresSenderPhoneConfirmation,
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

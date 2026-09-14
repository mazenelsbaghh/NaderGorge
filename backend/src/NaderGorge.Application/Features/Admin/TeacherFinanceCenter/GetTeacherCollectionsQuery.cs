using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.TeacherFinanceCenter;

public sealed record GetTeacherCollectionsQuery(Guid TeacherId, int Page = 1, int PageSize = 25,
    bool VodafoneOnly = false) : IRequest<ApiResponse<TeacherCollectionsDto>>;

public sealed record TeacherCollectionDto(Guid Id, string StudentName, decimal Amount, string WalletLabel,
    string WalletPhoneNumber, string SenderPhoneNumber, DateTime? ResolvedAt, string Status,
    string? TransferReference, bool IsVodafoneCash);

public sealed record TeacherCollectionsDto(Guid TeacherId, decimal TotalAmount, decimal VodafoneCashAmount,
    decimal OtherOrUnverifiedAmount, int TotalCount, int VodafoneCashCount, int FilteredCount,
    int Page, int PageSize, IReadOnlyList<TeacherCollectionDto> Items);

public sealed class GetTeacherCollectionsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetTeacherCollectionsQuery, ApiResponse<TeacherCollectionsDto>>
{
    public async Task<ApiResponse<TeacherCollectionsDto>> Handle(GetTeacherCollectionsQuery request, CancellationToken ct)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100 || request.Page > int.MaxValue / request.PageSize)
            return ApiResponse<TeacherCollectionsDto>.Fail("بيانات الصفحة غير صالحة");
        if (!await db.TeacherProfiles.AsNoTracking().AnyAsync(x => x.Id == request.TeacherId, ct))
            return ApiResponse<TeacherCollectionsDto>.Fail("المدرس غير موجود");

        // The original receipt identifies the receiving provider; editable wallet labels/filters do not.
        var query = db.RechargeRequests.AsNoTracking()
            .Where(x => x.TeacherId == request.TeacherId &&
                (x.Status == RechargeRequestStatus.Matched || x.Status == RechargeRequestStatus.Approved))
            .Select(x => new
            {
                x.Id, StudentName = x.User.FullName, x.Amount, WalletLabel = x.Wallet.Label,
                WalletPhoneNumber = x.Wallet.PhoneNumber, x.SenderPhoneNumber, x.ResolvedAt, x.Status,
                TransferReference = x.MatchedSmsLog != null ? x.MatchedSmsLog.TransferReference : null,
                IsVodafoneCash = x.MatchedSmsLog != null &&
                    (x.MatchedSmsLog.Sender.Trim().ToLower() == "vodafonecash" ||
                     x.MatchedSmsLog.Sender.Trim().ToLower() == "vf-cash")
            });
        var totals = await query.GroupBy(x => 1).Select(group => new
        {
            Amount = group.Sum(x => x.Amount), Count = group.Count(),
            VodafoneAmount = group.Sum(x => x.IsVodafoneCash ? x.Amount : 0m),
            VodafoneCount = group.Count(x => x.IsVodafoneCash)
        }).SingleOrDefaultAsync(ct);
        var filtered = request.VodafoneOnly ? query.Where(x => x.IsVodafoneCash) : query;
        var items = await filtered.OrderByDescending(x => x.ResolvedAt).ThenByDescending(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new TeacherCollectionDto(x.Id, x.StudentName, x.Amount, x.WalletLabel,
                x.WalletPhoneNumber, x.SenderPhoneNumber, x.ResolvedAt, x.Status.ToString(),
                x.TransferReference, x.IsVodafoneCash)).ToListAsync(ct);
        return ApiResponse<TeacherCollectionsDto>.Ok(new(request.TeacherId, totals?.Amount ?? 0m,
            totals?.VodafoneAmount ?? 0m, (totals?.Amount ?? 0m) - (totals?.VodafoneAmount ?? 0m),
            totals?.Count ?? 0, totals?.VodafoneCount ?? 0,
            request.VodafoneOnly ? totals?.VodafoneCount ?? 0 : totals?.Count ?? 0,
            request.Page, request.PageSize, items));
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public sealed record GetWalletSmsLogsQuery(string? Search, bool? IsMatched, Guid? WalletId, int Page = 1, int PageSize = 50)
    : IRequest<AdminIncomingSmsLogPageDto>;

public sealed class GetWalletSmsLogsQueryHandler(IAppDbContext db)
    : IRequestHandler<GetWalletSmsLogsQuery, AdminIncomingSmsLogPageDto>
{
    public async Task<AdminIncomingSmsLogPageDto> Handle(GetWalletSmsLogsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 10, 100);
        var query = db.IncomingSmsLogs.AsNoTracking();

        if (request.IsMatched.HasValue)
            query = query.Where(log => log.IsMatched == request.IsMatched.Value);
        if (request.WalletId.HasValue)
            query = query.Where(log => log.WalletId == request.WalletId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(log =>
                log.Body.Contains(search) ||
                (log.ParsedSenderPhone != null && log.ParsedSenderPhone.Contains(search)) ||
                log.Wallet.PhoneNumber.Contains(search) ||
                log.Wallet.Label.Contains(search));
        }

        var totalCount = await query.CountAsync(ct);
        var logs = await query
            .OrderByDescending(log => log.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new AdminIncomingSmsLogDto
            {
                Id = log.Id,
                WalletId = log.WalletId,
                WalletLabel = log.Wallet.Label,
                WalletPhoneNumber = log.Wallet.PhoneNumber,
                Sender = log.Sender,
                Body = log.Body,
                ReceivedAt = log.ReceivedAt,
                ParsedAmount = log.ParsedAmount,
                ParsedSenderPhone = log.ParsedSenderPhone,
                IsMatched = log.IsMatched,
                MatchedRechargeRequestId = log.MatchedRechargeRequestId,
                MatchedStudentName = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.FullName : null,
                MatchedStudentPhoneNumber = log.MatchedRechargeRequest != null ? log.MatchedRechargeRequest.User.PhoneNumber : null,
                DeduplicationHash = log.DeduplicationHash
            })
            .ToListAsync(ct);

        return new AdminIncomingSmsLogPageDto(logs, totalCount, page, pageSize);
    }
}

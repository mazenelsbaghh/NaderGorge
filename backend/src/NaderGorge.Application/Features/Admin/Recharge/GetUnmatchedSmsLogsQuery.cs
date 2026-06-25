using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Recharge;

public record GetUnmatchedSmsLogsQuery : IRequest<ApiResponse<List<AdminIncomingSmsLogDto>>>;

public class GetUnmatchedSmsLogsQueryHandler : IRequestHandler<GetUnmatchedSmsLogsQuery, ApiResponse<List<AdminIncomingSmsLogDto>>>
{
    private readonly IAppDbContext _db;

    public GetUnmatchedSmsLogsQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<AdminIncomingSmsLogDto>>> Handle(GetUnmatchedSmsLogsQuery request, CancellationToken ct)
    {
        var logs = await _db.IncomingSmsLogs
            .Include(l => l.Wallet)
            .Where(l => !l.IsMatched)
            .OrderByDescending(l => l.ReceivedAt)
            .Select(l => new AdminIncomingSmsLogDto
            {
                Id = l.Id,
                WalletId = l.WalletId,
                WalletLabel = l.Wallet.Label,
                WalletPhoneNumber = l.Wallet.PhoneNumber,
                Sender = l.Sender,
                Body = l.Body,
                ReceivedAt = l.ReceivedAt,
                ParsedAmount = l.ParsedAmount,
                ParsedSenderPhone = l.ParsedSenderPhone,
                IsMatched = l.IsMatched,
                MatchedRechargeRequestId = l.MatchedRechargeRequestId,
                DeduplicationHash = l.DeduplicationHash
            })
            .ToListAsync(ct);

        return ApiResponse<List<AdminIncomingSmsLogDto>>.Ok(logs);
    }
}

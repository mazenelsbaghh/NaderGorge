using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Services;
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
        var unmatchedLogs = await _db.IncomingSmsLogs
            .AsNoTracking()
            .Include(l => l.Wallet)
            .Where(l => !l.IsMatched && l.ParsedAmount.HasValue)
            .OrderByDescending(l => l.ReceivedAt)
            .ToListAsync(ct);

        var logs = unmatchedLogs
            .Where(log => SmsParser.IsIncomingTransfer(log.Body))
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
                TransferReference = l.TransferReference,
                IsMatched = l.IsMatched,
                MatchedRechargeRequestId = l.MatchedRechargeRequestId,
                DeduplicationHash = l.DeduplicationHash
            })
            .ToList();

        return ApiResponse<List<AdminIncomingSmsLogDto>>.Ok(logs);
    }
}

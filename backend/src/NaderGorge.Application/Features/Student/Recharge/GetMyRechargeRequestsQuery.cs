using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Recharge;

public record GetMyRechargeRequestsQuery(Guid UserId) : IRequest<ApiResponse<List<StudentRechargeRequestDto>>>;

public class StudentRechargeRequestDto
{
    public Guid Id { get; set; }
    public string ReviewCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string WalletLabel { get; set; } = string.Empty;
    public string WalletPhoneNumber { get; set; } = string.Empty;
    public RechargeRequestStatus Status { get; set; }
    public string? ScreenshotUrl { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class GetMyRechargeRequestsQueryHandler : IRequestHandler<GetMyRechargeRequestsQuery, ApiResponse<List<StudentRechargeRequestDto>>>
{
    private readonly IAppDbContext _db;

    public GetMyRechargeRequestsQueryHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<List<StudentRechargeRequestDto>>> Handle(GetMyRechargeRequestsQuery request, CancellationToken ct)
    {
        var requests = await _db.RechargeRequests
            .AsNoTracking()
            .Include(r => r.Wallet)
            .Where(r => r.UserId == request.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var items = requests
            .Select(r => new StudentRechargeRequestDto
            {
                Id = r.Id,
                ReviewCode = r.Id.ToString("N").Substring(0, 8).ToUpper(),
                Amount = r.Amount,
                SenderPhoneNumber = r.SenderPhoneNumber,
                WalletLabel = r.Wallet.Label,
                WalletPhoneNumber = r.Wallet.PhoneNumber,
                Status = r.Status,
                ScreenshotUrl = r.ScreenshotUrl,
                RejectionReason = r.RejectionReason,
                CreatedAt = r.CreatedAt,
                ResolvedAt = r.ResolvedAt
            })
            .ToList();

        return ApiResponse<List<StudentRechargeRequestDto>>.Ok(items);
    }
}

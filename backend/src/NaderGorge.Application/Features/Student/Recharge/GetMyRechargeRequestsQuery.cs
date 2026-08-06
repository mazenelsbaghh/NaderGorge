using System;
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

namespace NaderGorge.Application.Features.Student.Recharge;

public record GetMyRechargeRequestsQuery(Guid UserId) : IRequest<ApiResponse<List<StudentRechargeRequestDto>>>;

public class StudentRechargeRequestDto
{
    public Guid Id { get; set; }
    public string ReviewCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public string SenderPhoneNumber { get; set; } = string.Empty;
    public string? OriginalSenderPhoneNumber { get; set; }
    public bool RequiresSenderPhoneConfirmation { get; set; }
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
        await RechargeRequestExpiryService.ResolveExpiredPendingRequests(_db, ct);

        var requests = await _db.RechargeRequests
            .AsNoTracking()
            .Include(r => r.Wallet)
            .Include(r => r.Teacher!).ThenInclude(t => t.User)
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
                TeacherId = r.TeacherId,
                TeacherName = r.Teacher != null && r.Teacher.User != null ? r.Teacher.User.FullName : null,
                SenderPhoneNumber = r.SenderPhoneNumber,
                OriginalSenderPhoneNumber = r.OriginalSenderPhoneNumber,
                RequiresSenderPhoneConfirmation = r.RequiresSenderPhoneConfirmation,
                WalletLabel = r.Wallet.Label,
                // Never keep advertising a wallet after an operator disables it.
                WalletPhoneNumber = r.Wallet.IsActive ? r.Wallet.PhoneNumber : string.Empty,
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

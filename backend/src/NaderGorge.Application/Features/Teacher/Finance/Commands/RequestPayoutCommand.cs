using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Teacher.Finance.Commands;

public record RequestPayoutCommand(
    Guid TeacherUserId,
    decimal Amount
) : IRequest<ApiResponse<TeacherPayoutRequestDto>>;

public record TeacherPayoutRequestDto(
    Guid Id,
    decimal Amount,
    string Status,
    DateTime CreatedAt
);

public class RequestPayoutCommandHandler : IRequestHandler<RequestPayoutCommand, ApiResponse<TeacherPayoutRequestDto>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public RequestPayoutCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<TeacherPayoutRequestDto>> Handle(RequestPayoutCommand request, CancellationToken ct)
    {
        var teacherProfile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == request.TeacherUserId, ct);

        if (teacherProfile == null)
        {
            return ApiResponse<TeacherPayoutRequestDto>.Fail("حساب المعلم غير موجود");
        }

        var account = await _db.TeacherAccounts
            .FirstOrDefaultAsync(ta => ta.TeacherId == teacherProfile.Id, ct);

        if (account == null)
        {
            account = new TeacherAccount
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherProfile.Id,
                TotalEarnings = 0m,
                CurrentBalance = 0m,
                CommissionRate = teacherProfile.CommissionRate
            };
            _db.TeacherAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        if (request.Amount <= 0)
        {
            return ApiResponse<TeacherPayoutRequestDto>.Fail("المبلغ المطلوب يجب أن يكون أكبر من صفر");
        }

        if (request.Amount > account.CurrentBalance)
        {
            return ApiResponse<TeacherPayoutRequestDto>.Fail($"رصيدك الحالي لا يكفي لطلب دفعة قيمتها ({request.Amount} ج.م)");
        }

        var payout = new TeacherPayout
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherProfile.Id,
            Amount = request.Amount,
            Status = PayoutStatus.Pending
        };

        _db.TeacherPayouts.Add(payout);
        await _db.SaveChangesAsync(ct);

        // Audit log
        var auditEntry = new AuditLog
        {
            Action = "RequestPayout",
            EntityType = nameof(TeacherPayout),
            EntityId = payout.Id,
            PerformedByUserId = request.TeacherUserId,
            NewValues = $"Amount: {request.Amount}",
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        var dto = new TeacherPayoutRequestDto(
            payout.Id,
            payout.Amount,
            payout.Status.ToString(),
            payout.CreatedAt
        );

        return ApiResponse<TeacherPayoutRequestDto>.Ok(dto, "تم تقديم طلب الدفعة بنجاح");
    }
}

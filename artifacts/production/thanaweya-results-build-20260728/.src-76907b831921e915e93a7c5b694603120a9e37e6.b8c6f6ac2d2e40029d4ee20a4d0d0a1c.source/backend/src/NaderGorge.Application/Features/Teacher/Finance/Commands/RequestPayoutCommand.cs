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
    decimal ReservedBalance,
    decimal AvailableBalance,
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
        return await SerializationRetryHelper.ExecuteAsync(
            retryCt => HandleOnce(request, retryCt),
            ct);
    }

    private async Task<ApiResponse<TeacherPayoutRequestDto>> HandleOnce(RequestPayoutCommand request, CancellationToken ct)
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
                ReservedBalance = 0m,
                CommissionRate = teacherProfile.CommissionRate
            };
            _db.TeacherAccounts.Add(account);
            await _db.SaveChangesAsync(ct);
        }

        if (request.Amount <= 0)
        {
            return ApiResponse<TeacherPayoutRequestDto>.Fail("المبلغ المطلوب يجب أن يكون أكبر من صفر");
        }

        var availableBalance = account.CurrentBalance - account.ReservedBalance;
        if (request.Amount > availableBalance)
        {
            return ApiResponse<TeacherPayoutRequestDto>.Fail($"رصيدك المتاح لا يكفي لطلب دفعة قيمتها ({request.Amount} ج.م)");
        }

        var hasActiveTransaction = _db is DbContext efDb && efDb.Database.CurrentTransaction != null;
        await using var transaction = hasActiveTransaction ? null : await _db.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        if (_db is DbContext efDb2 && efDb2.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            account.ReservedBalance += request.Amount;
            account.UpdatedAt = DateTime.UtcNow;
            _db.TeacherAccounts.Update(account);
        }
        else
        {
            var reservedRows = await _db.TeacherAccounts
                .Where(ta => ta.Id == account.Id && ta.CurrentBalance - ta.ReservedBalance >= request.Amount)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(ta => ta.ReservedBalance, ta => ta.ReservedBalance + request.Amount)
                    .SetProperty(ta => ta.Version, ta => ta.Version + 1)
                    .SetProperty(ta => ta.UpdatedAt, DateTime.UtcNow), ct);

            if (reservedRows != 1)
            {
                return ApiResponse<TeacherPayoutRequestDto>.Fail($"رصيدك المتاح لا يكفي لطلب دفعة قيمتها ({request.Amount} ج.م)");
            }
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
        if (transaction != null)
        {
            await transaction.CommitAsync(ct);
        }
        await _db.Entry(account).ReloadAsync(ct);

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
            account.ReservedBalance,
            account.CurrentBalance - account.ReservedBalance,
            payout.CreatedAt
        );

        return ApiResponse<TeacherPayoutRequestDto>.Ok(dto, "تم تقديم طلب الدفعة بنجاح");
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentBalanceQuery(Guid StudentId) : IRequest<ApiResponse<StudentBalanceDto>>;

public record StudentBalanceDto(
    decimal CurrentBalance,
    List<BalanceTransactionDto> RecentTransactions,
    decimal PromotionalBalance = 0,
    List<PromotionalBalanceDto>? PromotionalAllocations = null
);

public record PromotionalBalanceDto(
    Guid Id,
    decimal OriginalAmount,
    decimal AvailableAmount,
    decimal ConsumedAmount,
    decimal ExpiredAmount,
    decimal RevokedAmount,
    Guid? TeacherId,
    string? TeacherName,
    string? TeacherProfileImageUrl,
    DateTime? ExpiresAt,
    int PurchaseCount,
    int? MaxPurchaseCount,
    string Status);

public record BalanceTransactionDto(
    Guid Id,
    decimal Amount,
    decimal BalanceAfter,
    string TransactionType,
    string Description,
    DateTime CreatedAt,
    bool AffectsBalance = true
);

public class GetStudentBalanceQueryHandler : IRequestHandler<GetStudentBalanceQuery, ApiResponse<StudentBalanceDto>>
{
    private readonly IAppDbContext _db;
    private readonly IPromotionalBalanceService? _promotionalBalance;

    public GetStudentBalanceQueryHandler(IAppDbContext db, IPromotionalBalanceService? promotionalBalance = null)
    {
        _db = db;
        _promotionalBalance = promotionalBalance;
    }

    public async Task<ApiResponse<StudentBalanceDto>> Handle(GetStudentBalanceQuery request, CancellationToken ct)
    {
        var balance = await _db.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.StudentId, ct);

        decimal currentBalance = balance?.CurrentBalance ?? 0m;
        var balanceTransactions = new List<BalanceTransactionDto>();

        if (balance != null)
        {
            balanceTransactions = await _db.BalanceTransactions
                .Where(t => t.StudentBalanceId == balance.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new BalanceTransactionDto(
                    t.Id,
                    t.Amount,
                    t.BalanceAfter,
                    t.TransactionType,
                    t.Description ?? "",
                    t.CreatedAt,
                    true
                ))
                .ToListAsync(ct);
        }

        var codeTransactions = await _db.AccessCodes
            .AsNoTracking()
            .Where(code => code.IsConsumed
                && code.ConsumedByUserId == request.StudentId
                && code.CodeGroup.CodeType != CodeType.Balance)
            .OrderByDescending(code => code.ConsumedAt ?? code.CreatedAt)
            .Take(20)
            .Select(code => new BalanceTransactionDto(
                code.Id,
                0,
                0,
                "ContentCodeRedemption",
                "تفعيل " + (
                    code.CodeGroup.CodeType == CodeType.Package ? "باقة" :
                    code.CodeGroup.CodeType == CodeType.Term ? "ترم" :
                    code.CodeGroup.CodeType == CodeType.Month ? "شهر" :
                    code.CodeGroup.CodeType == CodeType.Lesson ? "حصة" :
                    code.CodeGroup.CodeType == CodeType.Video ? "فيديوهات" :
                    code.CodeGroup.CodeType == CodeType.Exam ? "امتحان" : "محتوى")
                    + " بالكود: " + code.CodeGroup.Name,
                code.ConsumedAt ?? code.CreatedAt,
                false))
            .ToListAsync(ct);

        var transactions = balanceTransactions
            .Concat(codeTransactions)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(20)
            .ToList();

        if (_promotionalBalance != null)
            await _promotionalBalance.ExpireAvailableAsync(request.StudentId, ct);

        var promotionalAllocations = await _db.PromotionalBalanceAllocations
            .AsNoTracking()
            .Where(x => x.StudentId == request.StudentId)
            .OrderByDescending(x => x.AvailableAmount > 0)
            .ThenBy(x => x.ExpiresAt == null)
            .ThenBy(x => x.ExpiresAt)
            .Select(x => new PromotionalBalanceDto(
                x.Id,
                x.OriginalAmount,
                x.AvailableAmount,
                x.ConsumedAmount,
                x.ExpiredAmount,
                x.RevokedAmount,
                x.TeacherId,
                x.Teacher != null ? x.Teacher.User.FullName : null,
                x.Teacher != null ? x.Teacher.ProfileImageUrl : null,
                x.ExpiresAt,
                x.PurchaseCount,
                x.MaxPurchaseCount,
                x.Status.ToString()))
            .ToListAsync(ct);

        var dto = new StudentBalanceDto(
            currentBalance,
            transactions,
            promotionalAllocations.Sum(x => x.AvailableAmount),
            promotionalAllocations);

        return ApiResponse<StudentBalanceDto>.Ok(dto);
    }
}

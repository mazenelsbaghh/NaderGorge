using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentBalanceQuery(Guid StudentId) : IRequest<ApiResponse<StudentBalanceDto>>;

public record StudentBalanceDto(
    decimal CurrentBalance,
    List<BalanceTransactionDto> RecentTransactions
);

public record BalanceTransactionDto(
    Guid Id,
    decimal Amount,
    decimal BalanceAfter,
    string TransactionType,
    string Description,
    DateTime CreatedAt
);

public class GetStudentBalanceQueryHandler : IRequestHandler<GetStudentBalanceQuery, ApiResponse<StudentBalanceDto>>
{
    private readonly IAppDbContext _db;

    public GetStudentBalanceQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentBalanceDto>> Handle(GetStudentBalanceQuery request, CancellationToken ct)
    {
        var balance = await _db.StudentBalances
            .FirstOrDefaultAsync(b => b.UserId == request.StudentId, ct);

        decimal currentBalance = balance?.CurrentBalance ?? 0m;
        var transactions = new List<BalanceTransactionDto>();

        if (balance != null)
        {
            transactions = await _db.BalanceTransactions
                .Where(t => t.StudentBalanceId == balance.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .Select(t => new BalanceTransactionDto(
                    t.Id,
                    t.Amount,
                    t.BalanceAfter,
                    t.TransactionType,
                    t.Description ?? "",
                    t.CreatedAt
                ))
                .ToListAsync(ct);
        }

        var dto = new StudentBalanceDto(currentBalance, transactions);

        return ApiResponse<StudentBalanceDto>.Ok(dto);
    }
}

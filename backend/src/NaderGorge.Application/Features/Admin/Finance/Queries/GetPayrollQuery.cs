using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Queries;

public record GetPayrollQuery(int Month, int Year) : IRequest<ApiResponse<List<PayrollRecordDto>>>;

public record PayrollRecordDto(
    Guid Id,
    Guid EmployeeProfileId,
    string EmployeeName,
    int Month,
    int Year,
    decimal BasicSalary,
    decimal Additions,
    decimal Deductions,
    decimal NetSalary,
    string Status,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    DateTime CreatedAt,
    List<PayrollAdjustmentDto> Adjustments
);

public record PayrollAdjustmentDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Reason,
    DateTime CreatedAt
);

public class GetPayrollQueryHandler : IRequestHandler<GetPayrollQuery, ApiResponse<List<PayrollRecordDto>>>
{
    private readonly IAppDbContext _db;

    public GetPayrollQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<List<PayrollRecordDto>>> Handle(GetPayrollQuery request, CancellationToken ct)
    {
        var records = await _db.PayrollRecords
            .Include(pr => pr.EmployeeProfile).ThenInclude(ep => ep.User)
            .Include(pr => pr.ApprovedByUser)
            .Include(pr => pr.Adjustments)
            .Where(pr => pr.Month == request.Month && pr.Year == request.Year)
            .ToListAsync(ct);

        var dtos = records.Select(pr =>
        {
            var additions = pr.Adjustments.Where(a => a.Type == PayrollAdjustmentType.Addition).Sum(a => a.Amount);
            var deductions = pr.Adjustments.Where(a => a.Type == PayrollAdjustmentType.Deduction).Sum(a => a.Amount);
            var net = pr.BasicSalary + additions - deductions;

            var adjDtos = pr.Adjustments.Select(a => new PayrollAdjustmentDto(
                a.Id,
                a.Type.ToString(),
                a.Amount,
                a.Reason,
                a.CreatedAt
            )).ToList();

            return new PayrollRecordDto(
                pr.Id,
                pr.EmployeeProfileId,
                pr.EmployeeProfile.User?.FullName ?? "Unknown",
                pr.Month,
                pr.Year,
                pr.BasicSalary,
                additions,
                deductions,
                net,
                pr.Status.ToString(),
                pr.ApprovedByUserId,
                pr.ApprovedByUser?.FullName,
                pr.ApprovedAt,
                pr.CreatedAt,
                adjDtos
            );
        }).ToList();

        return ApiResponse<List<PayrollRecordDto>>.Ok(dtos);
    }
}

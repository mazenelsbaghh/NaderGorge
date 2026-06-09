using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Finance.Commands;

public record GeneratePayrollCommand(
    int Month,
    int Year,
    Guid AdminUserId
) : IRequest<ApiResponse<int>>;

public class GeneratePayrollCommandHandler : IRequestHandler<GeneratePayrollCommand, ApiResponse<int>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public GeneratePayrollCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<int>> Handle(GeneratePayrollCommand request, CancellationToken ct)
    {
        if (request.Month < 1 || request.Month > 12)
        {
            return ApiResponse<int>.Fail("الشهر يجب أن يكون بين 1 و 12");
        }

        if (request.Year < 2000 || request.Year > 2100)
        {
            return ApiResponse<int>.Fail("السنة غير صالحة");
        }

        // Get all employees
        var employees = await _db.EmployeeProfiles
            .Include(e => e.User)
            .ToListAsync(ct);

        int count = 0;
        foreach (var employee in employees)
        {
            // Check if payroll already exists
            var exists = await _db.PayrollRecords
                .AnyAsync(pr => pr.EmployeeProfileId == employee.Id && pr.Month == request.Month && pr.Year == request.Year, ct);

            if (!exists)
            {
                var payroll = new PayrollRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeProfileId = employee.Id,
                    Month = request.Month,
                    Year = request.Year,
                    BasicSalary = employee.BasicSalary,
                    Status = PayrollStatus.Draft
                };
                _db.PayrollRecords.Add(payroll);
                count++;
            }
        }

        if (count > 0)
        {
            await _db.SaveChangesAsync(ct);

            // Log audit
            var auditEntry = new AuditLog
            {
                Action = "GeneratePayroll",
                EntityType = nameof(PayrollRecord),
                EntityId = Guid.Empty,
                PerformedByUserId = request.AdminUserId,
                NewValues = $"Month: {request.Month}, Year: {request.Year}, GeneratedCount: {count}",
                CreatedAt = DateTime.UtcNow
            };
            await _audit.AddAsync(auditEntry);
        }

        return ApiResponse<int>.Ok(count, $"تم إنشاء {count} سجل مرتبات بنجاح");
    }
}

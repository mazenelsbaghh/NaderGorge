using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.People;

public interface IHrLifecycleNotificationService
{
    Task<int> EnqueueDueAsync(DateOnly asOf, int warningDays, CancellationToken ct);
}

public sealed class HrLifecycleNotificationService : IHrLifecycleNotificationService
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;

    public HrLifecycleNotificationService(IAppDbContext db, IHrAuditWriter audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<int> EnqueueDueAsync(DateOnly asOf, int warningDays, CancellationToken ct)
    {
        if (warningDays is < 0 or > 365) throw new ArgumentOutOfRangeException(nameof(warningDays));
        var until = asOf.AddDays(warningDays);
        var contracts = await _db.EmploymentContracts.AsNoTracking()
            .Where(item => item.Status != Domain.Enums.EmploymentContractStatus.Terminated &&
                ((item.EndDate.HasValue && item.EndDate >= asOf && item.EndDate <= until) ||
                 (item.ProbationEndDate.HasValue && item.ProbationEndDate >= asOf && item.ProbationEndDate <= until)))
            .Select(item => new { item.Id, item.EmployeeId, UserId = item.Employee!.UserId, item.ContractNumber, item.EndDate, item.ProbationEndDate })
            .ToListAsync(ct);
        var created = 0;
        foreach (var contract in contracts)
        {
            if (contract.EndDate.HasValue && await AddOnceAsync(contract.Id, contract.UserId, "ContractExpiryAlert",
                    "تنبيه انتهاء العقد", $"العقد {contract.ContractNumber} ينتهي في {contract.EndDate:yyyy-MM-dd}", ct)) created++;
            if (contract.ProbationEndDate.HasValue && await AddOnceAsync(contract.Id, contract.UserId, "ProbationExpiryAlert",
                    "تنبيه نهاية فترة التجربة", $"فترة التجربة للعقد {contract.ContractNumber} تنتهي في {contract.ProbationEndDate:yyyy-MM-dd}", ct)) created++;
        }
        if (created > 0) await _db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<bool> AddOnceAsync(Guid contractId, Guid userId, string action, string title, string body, CancellationToken ct)
    {
        if (await _db.AuditLogs.AsNoTracking().AnyAsync(item => item.Action == action && item.EntityId == contractId, ct)) return false;
        _db.NotificationEvents.Add(new NotificationEvent
        {
            UserId = userId, ChannelType = NotificationChannelType.InApp, Title = title, Body = body
        });
        await _audit.WriteMutationAsync(action, nameof(EmploymentContract), contractId, null,
            new { recipientUserId = userId, notification = title }, "Lifecycle deadline notification", ct,
            systemActor: "hr-lifecycle-notification-service");
        return true;
    }
}

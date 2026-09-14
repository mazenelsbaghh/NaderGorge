using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Lifecycle;

public sealed class LifecycleOrchestrationService(IAppDbContext db, DocumentAssetService documentAssetService)
{
    public async Task<ApiResponse<Guid>> StartOffboardingAsync(Guid employeeId, DateOnly lastWorkingDate, string reason, Guid actorUserId, CancellationToken ct)
    {
        var existing = await db.OffboardingProcesses.SingleOrDefaultAsync(item => item.EmployeeId == employeeId && item.State != OffboardingState.Completed && item.State != OffboardingState.Cancelled, ct);
        if (existing is not null) return ApiResponse<Guid>.Ok(existing.Id);
        if (!await documentAssetService.CanOffboardAsync(employeeId, ct)) return ApiResponse<Guid>.Fail("توجد عهدة مفتوحة", ["OPEN_ASSET_CUSTODY"]);
        var outstanding = await db.HrFinancialRequests.Where(item => item.EmployeeId == employeeId && item.OutstandingBalance > 0).SumAsync(item => (decimal?)item.OutstandingBalance, ct) ?? 0;
        var blockers = outstanding > 0 ? new[] { $"OUTSTANDING_FINANCIAL_BALANCE:{outstanding:0.00}" } : Array.Empty<string>();
        var process = new OffboardingProcess { EmployeeId = employeeId, LastWorkingDate = lastWorkingDate, Reason = reason.Trim(),
            InitiatedByUserId = actorUserId, State = blockers.Length == 0 ? OffboardingState.InProgress : OffboardingState.Blocked, BlockersJson = JsonSerializer.Serialize(blockers) };
        db.OffboardingProcesses.Add(process); db.EmployeeLifecycleTasks.Add(new EmployeeLifecycleTask { EmployeeId = employeeId, Phase = "Offboarding",
            Title = "إلغاء الصلاحيات وتسليم العهد", DueAt = lastWorkingDate.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc) });
        await db.SaveChangesAsync(ct); return blockers.Length == 0 ? ApiResponse<Guid>.Ok(process.Id) : ApiResponse<Guid>.Fail("توجد موانع مالية", ["OFFBOARDING_BLOCKED"], process.Id);
    }

    public async Task<ApiResponse<bool>> CompleteOffboardingAsync(Guid processId, Guid actorUserId, int expectedVersion, CancellationToken ct)
    {
        var process = await db.OffboardingProcesses.Include(item => item.Employee).ThenInclude(item => item!.User).SingleOrDefaultAsync(item => item.Id == processId, ct);
        if (process is null) return ApiResponse<bool>.Fail("إجراء الخروج غير موجود", ["OFFBOARDING_NOT_FOUND"]);
        if (process.Version != expectedVersion) return ApiResponse<bool>.Fail("تم تعديل الإجراء", ["CONCURRENCY_CONFLICT"]);
        if (process.State == OffboardingState.Completed) return ApiResponse<bool>.Ok(true);
        if (process.State != OffboardingState.InProgress || !await documentAssetService.CanOffboardAsync(process.EmployeeId, ct)) return ApiResponse<bool>.Fail("ما زالت توجد موانع", ["OFFBOARDING_BLOCKED"]);
        process.State = OffboardingState.Completed; process.CompletedAt = DateTime.UtcNow; process.CompletedByUserId = actorUserId; process.Version++;
        var employee = process.Employee!; employee.EmploymentStatus = EmployeeEmploymentStatus.Terminated; employee.TerminationDate = process.LastWorkingDate;
        employee.User!.IsActive = false; employee.User.SecurityStampVersion++; employee.User.SuspensionReason = $"Offboarded: {process.Reason}";
        foreach (var task in await db.EmployeeLifecycleTasks.Where(item => item.EmployeeId == employee.Id && item.State != LifecycleTaskState.Completed).ToListAsync(ct))
        { task.State = LifecycleTaskState.Waived; task.CompletionNote = "Closed by offboarding"; task.CompletedAt = DateTime.UtcNow; }
        db.OutboxEvents.Add(new OutboxEvent { Type = "hr.employee.offboarded", TargetUserId = employee.UserId.ToString(),
            PayloadJson = JsonSerializer.Serialize(new { employee.Id, process.LastWorkingDate, process.Reason, actorUserId }) });
        await db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<int> QueueLateTaskAlertsAsync(DateTime now, CancellationToken ct)
    {
        var tasks = await db.EmployeeLifecycleTasks.Include(item => item.Employee).Where(item => item.State != LifecycleTaskState.Completed && item.State != LifecycleTaskState.Waived && item.DueAt < now).ToListAsync(ct); var count = 0;
        foreach (var task in tasks)
        {
            var key = $"{task.Id:N}:{now:yyyyMMdd}"; if (await db.HrIdempotencyRecords.AnyAsync(item => item.Scope == "lifecycle-task-overdue" && item.Key == key, ct)) continue;
            db.HrIdempotencyRecords.Add(new HrIdempotencyRecord { Scope = "lifecycle-task-overdue", Key = key, RequestHash = key, ActorUserId = Guid.Empty, ResultEntityId = task.Id, ExpiresAt = now.AddYears(1) });
            db.OutboxEvents.Add(new OutboxEvent { Type = "hr.lifecycle.task.overdue", TargetUserId = task.AssignedToUserId?.ToString() ?? task.Employee?.UserId.ToString(), PayloadJson = JsonSerializer.Serialize(new { task.Id, task.Title, task.DueAt, task.Phase }) }); count++;
        }
        if (count > 0) await db.SaveChangesAsync(ct); return count;
    }
}

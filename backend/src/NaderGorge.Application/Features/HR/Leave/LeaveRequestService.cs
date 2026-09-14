using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Leave;

public sealed class LeaveRequestService
{
    private readonly IAppDbContext _db;
    public LeaveRequestService(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> SubmitAsync(Guid userId, Guid leaveTypeId, DateOnly startDate, DateOnly endDate,
        decimal dayFraction, string reason, string? attachmentReference, CancellationToken ct)
    {
        if (endDate < startDate || startDate.Year != endDate.Year || dayFraction is not (0.5m or 1m))
            return ApiResponse<Guid>.Fail("تواريخ أو نسبة الإجازة غير صالحة", ["LEAVE_DATE_INVALID"]);
        if (dayFraction == 0.5m && startDate != endDate)
            return ApiResponse<Guid>.Fail("نصف اليوم متاح ليوم واحد فقط", ["HALF_DAY_RANGE_INVALID"]);
        if (string.IsNullOrWhiteSpace(reason))
            return ApiResponse<Guid>.Fail("سبب الإجازة مطلوب", ["LEAVE_REASON_REQUIRED"]);

        var employee = await _db.EmployeeProfiles.SingleOrDefaultAsync(item => item.UserId == userId, ct);
        if (employee is null) return ApiResponse<Guid>.Fail("ملف الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        var type = await _db.LeaveTypes.SingleOrDefaultAsync(item => item.Id == leaveTypeId && item.IsActive, ct);
        if (type is null) return ApiResponse<Guid>.Fail("نوع الإجازة غير متاح", ["LEAVE_TYPE_NOT_FOUND"]);
        if (dayFraction != 1 && !type.AllowsHalfDay)
            return ApiResponse<Guid>.Fail("نوع الإجازة لا يسمح بجزء من اليوم", ["HALF_DAY_NOT_ALLOWED"]);
        if (type.RequiresAttachment && string.IsNullOrWhiteSpace(attachmentReference))
            return ApiResponse<Guid>.Fail("المرفق مطلوب", ["ATTACHMENT_REQUIRED"]);
        if (await _db.HrLeaveRequests.AnyAsync(item => item.EmployeeId == employee.Id &&
            item.State != LeaveRequestState.Rejected && item.State != LeaveRequestState.Withdrawn && item.State != LeaveRequestState.Cancelled &&
            item.StartDate <= endDate && item.EndDate >= startDate, ct))
            return ApiResponse<Guid>.Fail("توجد إجازة متداخلة", ["LEAVE_OVERLAP"]);

        var policy = await _db.LeavePolicies.Include(item => item.WorkCalendar)
            .Where(item => item.LeaveTypeId == leaveTypeId && item.EffectiveFrom <= startDate &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= endDate))
            .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (policy?.WorkCalendar is null) return ApiResponse<Guid>.Fail("لا توجد سياسة إجازة فعالة", ["LEAVE_POLICY_NOT_FOUND"]);
        var workdays = LeaveWorkdayCalculator.Calculate(startDate, endDate, dayFraction, policy.WorkCalendar);
        if (workdays <= 0) return ApiResponse<Guid>.Fail("الفترة لا تحتوي يوم عمل", ["NO_WORKDAYS"]);
        var balance = await _db.LeaveBalances.SingleOrDefaultAsync(item => item.EmployeeId == employee.Id &&
            item.LeaveTypeId == leaveTypeId && item.Year == startDate.Year, ct);
        if (balance is null) return ApiResponse<Guid>.Fail("رصيد الإجازة غير موجود", ["LEAVE_BALANCE_NOT_FOUND"]);
        if (!policy.AllowNegativeBalance && balance.Available < workdays)
            return ApiResponse<Guid>.Fail("الرصيد غير كافٍ", ["INSUFFICIENT_LEAVE_BALANCE"]);

        var request = new HrLeaveRequest
        {
            EmployeeId = employee.Id, LeaveTypeId = leaveTypeId, StartDate = startDate, EndDate = endDate,
            DayFraction = dayFraction, Workdays = workdays, ReservedAmount = workdays, Reason = reason.Trim(),
            AttachmentReference = attachmentReference?.Trim(), State = LeaveRequestState.PendingApproval
        };
        balance.Reserved += workdays; balance.Version++;
        _db.HrLeaveRequests.Add(request);
        _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
        {
            LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Reserve, Amount = workdays,
            SourceType = nameof(HrLeaveRequest), SourceId = request.Id, Reason = "submit", ActorUserId = userId
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(request.Id);
    }

    public async Task<ApiResponse<bool>> WithdrawAsync(Guid userId, Guid requestId, string reason, CancellationToken ct)
    {
        var request = await _db.HrLeaveRequests.Include(item => item.Employee)
            .SingleOrDefaultAsync(item => item.Id == requestId && item.Employee!.UserId == userId, ct);
        if (request is null) return ApiResponse<bool>.Fail("طلب الإجازة غير موجود", ["LEAVE_REQUEST_NOT_FOUND"]);
        if (request.State == LeaveRequestState.Withdrawn) return ApiResponse<bool>.Ok(true);
        if (request.State != LeaveRequestState.PendingApproval)
            return ApiResponse<bool>.Fail("لا يمكن سحب الطلب بعد حسمه", ["LEAVE_WITHDRAW_NOT_ALLOWED"]);
        var balance = await _db.LeaveBalances.SingleAsync(item => item.EmployeeId == request.EmployeeId &&
            item.LeaveTypeId == request.LeaveTypeId && item.Year == request.StartDate.Year, ct);
        balance.Reserved -= request.ReservedAmount; balance.Version++;
        request.State = LeaveRequestState.Withdrawn; request.Version++;
        _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
        {
            LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Release, Amount = request.ReservedAmount,
            SourceType = nameof(HrLeaveRequest), SourceId = request.Id, Reason = reason.Trim(), ActorUserId = userId
        });
        await _db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> FinalizeApprovedAsync(Guid requestId, Guid actorUserId, CancellationToken ct)
    {
        var request = await _db.HrLeaveRequests.SingleOrDefaultAsync(item => item.Id == requestId, ct);
        if (request is null) return ApiResponse<bool>.Fail("طلب الإجازة غير موجود", ["LEAVE_REQUEST_NOT_FOUND"]);
        if (request.State == LeaveRequestState.Approved) return ApiResponse<bool>.Ok(true);
        if (request.State != LeaveRequestState.PendingApproval)
            return ApiResponse<bool>.Fail("حالة الطلب لا تسمح بالاعتماد", ["LEAVE_STATE_INVALID"]);
        var balance = await _db.LeaveBalances.SingleAsync(item => item.EmployeeId == request.EmployeeId &&
            item.LeaveTypeId == request.LeaveTypeId && item.Year == request.StartDate.Year, ct);
        var policy = await _db.LeavePolicies.Include(item => item.WorkCalendar)
            .Where(item => item.LeaveTypeId == request.LeaveTypeId && item.EffectiveFrom <= request.StartDate &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo >= request.EndDate))
            .OrderByDescending(item => item.EffectiveFrom).FirstAsync(ct);
        balance.Reserved -= request.ReservedAmount; balance.Used += request.Workdays; balance.Version++;
        request.State = LeaveRequestState.Approved; request.Version++;
        _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry
        {
            LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Debit, Amount = request.Workdays,
            SourceType = nameof(HrLeaveRequest), SourceId = request.Id, Reason = "approved", ActorUserId = actorUserId
        });
        var kind = (await _db.LeaveTypes.Where(item => item.Id == request.LeaveTypeId).Select(item => item.IsPaid).SingleAsync(ct))
            ? WorkdayClassificationKind.Leave : WorkdayClassificationKind.UnpaidLeave;
        foreach (var date in LeaveWorkdayCalculator.EnumerateWorkingDates(request.StartDate, request.EndDate, policy.WorkCalendar!))
            if (!await _db.WorkdayClassifications.AnyAsync(item => item.EmployeeId == request.EmployeeId && item.WorkDate == date, ct))
                _db.WorkdayClassifications.Add(new WorkdayClassification { EmployeeId = request.EmployeeId, WorkDate = date,
                    Kind = kind, SourceType = nameof(HrLeaveRequest), SourceId = request.Id });
        await _db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> FinalizeRejectedAsync(Guid requestId, Guid actorUserId, string reason, CancellationToken ct)
    {
        var request = await _db.HrLeaveRequests.SingleOrDefaultAsync(item => item.Id == requestId, ct);
        if (request is null) return ApiResponse<bool>.Fail("طلب الإجازة غير موجود", ["LEAVE_REQUEST_NOT_FOUND"]);
        if (request.State == LeaveRequestState.Rejected) return ApiResponse<bool>.Ok(true);
        if (request.State != LeaveRequestState.PendingApproval)
            return ApiResponse<bool>.Fail("حالة الطلب لا تسمح بالرفض", ["LEAVE_STATE_INVALID"]);
        var balance = await _db.LeaveBalances.SingleAsync(item => item.EmployeeId == request.EmployeeId &&
            item.LeaveTypeId == request.LeaveTypeId && item.Year == request.StartDate.Year, ct);
        balance.Reserved -= request.ReservedAmount; balance.Version++; request.State = LeaveRequestState.Rejected; request.Version++;
        _db.LeaveLedgerEntries.Add(new LeaveLedgerEntry { LeaveBalanceId = balance.Id, EntryType = LeaveLedgerEntryType.Release,
            Amount = request.ReservedAmount, SourceType = nameof(HrLeaveRequest), SourceId = request.Id, Reason = reason.Trim(), ActorUserId = actorUserId });
        await _db.SaveChangesAsync(ct); return ApiResponse<bool>.Ok(true);
    }
}

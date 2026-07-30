using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Scheduling.Commands;

public sealed record ShiftSegmentInput(int Sequence, DayOfWeek? DayOfWeek, TimeSpan StartsAt, TimeSpan EndsAt,
    int UnpaidBreakMinutes, ShiftWorkDateRule WorkDateRule);
public sealed record UpdateWorkCalendarCommand(Guid CalendarId, int WorkingDaysMask, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ShiftManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class UpdateWorkCalendarCommandHandler : IRequestHandler<UpdateWorkCalendarCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;

    public UpdateWorkCalendarCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<Guid>> Handle(UpdateWorkCalendarCommand request, CancellationToken ct)
    {
        if (request.WorkingDaysMask is < 1 or > 127)
            return ApiResponse<Guid>.Fail("اختر يوم عمل واحدًا على الأقل", ["WORK_CALENDAR_DAYS_INVALID"]);

        var calendar = await _db.WorkCalendars.SingleOrDefaultAsync(
            item => item.Id == request.CalendarId && item.IsActive,
            ct);
        if (calendar is null)
            return ApiResponse<Guid>.Fail("تقويم العمل غير موجود", ["WORK_CALENDAR_NOT_FOUND"]);

        var previousMask = calendar.WorkingDaysMask;
        calendar.WorkingDaysMask = request.WorkingDaysMask;
        await _audit.WriteMutationAsync(
            "UpdateWorkCalendar",
            nameof(WorkCalendar),
            calendar.Id,
            new { workingDaysMask = previousMask },
            new { calendar.WorkingDaysMask },
            "Update weekly working and rest days",
            ct,
            request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(calendar.Id);
    }
}

public sealed record CreateShiftTemplateCommand(string Code, string Name, ShiftTemplateMode Mode, Guid WorkCalendarId,
    int GraceMinutes, int MinimumBreakMinutes, int OvertimeAfterMinutes, IReadOnlyList<ShiftSegmentInput> Segments, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ShiftManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class CreateShiftTemplateCommandHandler : IRequestHandler<CreateShiftTemplateCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db; private readonly IHrAuditWriter _audit;
    public CreateShiftTemplateCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null) { _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance); }
    public async Task<ApiResponse<Guid>> Handle(CreateShiftTemplateCommand request, CancellationToken ct)
    {
        if (!await _db.WorkCalendars.AnyAsync(item => item.Id == request.WorkCalendarId && item.IsActive, ct))
            return ApiResponse<Guid>.Fail("تقويم العمل غير موجود", ["WORK_CALENDAR_NOT_FOUND"]);
        if (await _db.ShiftTemplates.AnyAsync(item => item.Code == request.Code.Trim(), ct))
            return ApiResponse<Guid>.Fail("كود الشفت مستخدم", ["SHIFT_CODE_EXISTS"]);
        var segments = request.Segments.Select(item => new ShiftSegment
        {
            Sequence = item.Sequence, DayOfWeek = item.DayOfWeek, StartsAt = item.StartsAt, EndsAt = item.EndsAt,
            UnpaidBreakMinutes = item.UnpaidBreakMinutes, WorkDateRule = item.WorkDateRule
        }).ToList();
        var errors = ShiftScheduleRules.ValidateSegments(segments);
        if (errors.Count > 0) return ApiResponse<Guid>.Fail("فترات الشفت غير صالحة", errors.ToList());
        var template = new ShiftTemplate
        {
            Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), Mode = request.Mode,
            WorkCalendarId = request.WorkCalendarId, GraceMinutes = request.GraceMinutes,
            MinimumBreakMinutes = request.MinimumBreakMinutes, OvertimeAfterMinutes = request.OvertimeAfterMinutes,
            Segments = segments
        };
        foreach (var segment in segments) segment.ShiftTemplateId = template.Id;
        _db.ShiftTemplates.Add(template);
        await _audit.WriteMutationAsync("CreateShiftTemplate", nameof(ShiftTemplate), template.Id, null,
            new { template.Code, template.Name, template.Mode, segmentCount = segments.Count }, "Create shift template", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(template.Id);
    }
}

public sealed record ShiftAssignmentInput(Guid EmployeeId, Guid ShiftTemplateId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason);
public sealed record PublishShiftAssignmentsCommand(IReadOnlyList<ShiftAssignmentInput> Assignments, Guid ActorUserId, string IdempotencyKey)
    : IRequest<ApiResponse<IReadOnlyList<Guid>>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ShiftManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class PublishShiftAssignmentsCommandHandler : IRequestHandler<PublishShiftAssignmentsCommand, ApiResponse<IReadOnlyList<Guid>>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;
    public PublishShiftAssignmentsCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db; _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<IReadOnlyList<Guid>>> Handle(PublishShiftAssignmentsCommand request, CancellationToken ct)
    {
        if (request.Assignments.Count == 0) return ApiResponse<IReadOnlyList<Guid>>.Fail("لا توجد تعيينات للنشر", ["SHIFT_ASSIGNMENTS_REQUIRED"]);
        var requestJson = JsonSerializer.Serialize(request.Assignments);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        var replay = await _db.HrIdempotencyRecords.FirstOrDefaultAsync(item => item.Scope == "shift.publish" && item.ActorUserId == request.ActorUserId && item.Key == request.IdempotencyKey, ct);
        if (replay is not null)
        {
            if (replay.RequestHash != hash) return ApiResponse<IReadOnlyList<Guid>>.Fail("مفتاح الطلب مستخدم لبيانات مختلفة", ["IDEMPOTENCY_KEY_REUSED"]);
            return ApiResponse<IReadOnlyList<Guid>>.Ok(JsonSerializer.Deserialize<Guid[]>(replay.ResponseJson ?? "[]") ?? []);
        }
        var employeeIds = request.Assignments.Select(item => item.EmployeeId).Distinct().ToList();
        var templateIds = request.Assignments.Select(item => item.ShiftTemplateId).Distinct().ToList();
        if (await _db.EmployeeProfiles.CountAsync(item => employeeIds.Contains(item.Id), ct) != employeeIds.Count ||
            await _db.ShiftTemplates.CountAsync(item => templateIds.Contains(item.Id) && item.IsActive, ct) != templateIds.Count)
            return ApiResponse<IReadOnlyList<Guid>>.Fail("موظف أو شفت غير صالح", ["SHIFT_REFERENCE_INVALID"]);
        var existing = await _db.ShiftAssignments.AsNoTracking().Where(item => employeeIds.Contains(item.EmployeeId) && item.Status == ShiftAssignmentStatus.Published)
            .Select(item => new { item.EmployeeId, item.EffectiveFrom, item.EffectiveTo }).ToListAsync(ct);
        foreach (var row in request.Assignments)
        {
            if (row.EffectiveTo <= row.EffectiveFrom) return ApiResponse<IReadOnlyList<Guid>>.Fail("فترة الشفت غير صالحة", ["SHIFT_ASSIGNMENT_PERIOD_INVALID"]);
            if (existing.Any(item => item.EmployeeId == row.EmployeeId && ShiftScheduleRules.PeriodsOverlap(item.EffectiveFrom, item.EffectiveTo, row.EffectiveFrom, row.EffectiveTo)) ||
                request.Assignments.Any(other => !ReferenceEquals(other, row) && other.EmployeeId == row.EmployeeId && ShiftScheduleRules.PeriodsOverlap(other.EffectiveFrom, other.EffectiveTo, row.EffectiveFrom, row.EffectiveTo)))
                return ApiResponse<IReadOnlyList<Guid>>.Fail("يوجد تعارض في تعيينات الشفت", ["SHIFT_ASSIGNMENT_OVERLAP"]);
        }
        var now = DateTime.UtcNow;
        var entities = request.Assignments.Select(row => new ShiftAssignment
        {
            EmployeeId = row.EmployeeId, ShiftTemplateId = row.ShiftTemplateId, EffectiveFrom = row.EffectiveFrom,
            EffectiveTo = row.EffectiveTo, Reason = row.Reason.Trim(), Status = ShiftAssignmentStatus.Published,
            PublishedByUserId = request.ActorUserId, PublishedAt = now
        }).ToList();
        _db.ShiftAssignments.AddRange(entities);
        foreach (var entity in entities)
            await _audit.WriteMutationAsync("PublishShiftAssignment", nameof(ShiftAssignment), entity.Id, null,
                new { entity.EmployeeId, entity.ShiftTemplateId, entity.EffectiveFrom, entity.EffectiveTo }, entity.Reason, ct, request.ActorUserId);
        var ids = entities.Select(item => item.Id).ToArray();
        _db.HrIdempotencyRecords.Add(new HrIdempotencyRecord
        {
            Scope = "shift.publish", ActorUserId = request.ActorUserId, Key = request.IdempotencyKey,
            RequestHash = hash, ResponseJson = JsonSerializer.Serialize(ids), ExpiresAt = now.AddDays(7)
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<IReadOnlyList<Guid>>.Ok(ids);
    }
}

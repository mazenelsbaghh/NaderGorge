using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.HR.Scheduling;
using NaderGorge.Application.Features.HR.Scheduling.Commands;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public sealed record CreateRequisitionCommand(string Title, Guid? OrganizationUnitId, int Openings, string Requirements, Guid ActorUserId) : IRequest<ApiResponse<Guid>>;
public sealed record AddCandidateCommand(Guid RequisitionId, string FullName, string PhoneNumber, string? Email, string? CvAssetReference) : IRequest<ApiResponse<Guid>>;
public sealed record ScheduleCandidateInterviewCommand(Guid CandidateId, DateTime ScheduledAt, Guid InterviewerUserId) : IRequest<ApiResponse<Guid>>;
public sealed record CreateCandidateOfferCommand(Guid CandidateId, decimal BaseSalary, string Currency, DateOnly ProposedStartDate) : IRequest<ApiResponse<Guid>>;
public sealed record AcceptCandidateOfferCommand(Guid OfferId, int ExpectedVersion) : IRequest<ApiResponse<Guid>>;
public sealed record UpdatePublishedShiftCommand(Guid AssignmentId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Reason,
    IReadOnlyList<ShiftSegmentInput> Segments, Guid ActorUserId) : IRequest<ApiResponse<Guid>>;
public sealed record CreateAttendancePolicyCommand(string Code, string Name, AttendancePolicyKind Kind, decimal? Latitude,
    decimal? Longitude, int RadiusMeters, int MaximumAccuracyMeters) : IRequest<ApiResponse<Guid>>;
public sealed record AssignAttendancePolicyCommand(Guid AttendancePolicyId, Guid? EmployeeId, Guid? ShiftTemplateId,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo) : IRequest<ApiResponse<Guid>>;

public sealed class HrRecruitmentShiftMutationHandler(IAppDbContext db) :
    IRequestHandler<CreateRequisitionCommand, ApiResponse<Guid>>,
    IRequestHandler<AddCandidateCommand, ApiResponse<Guid>>,
    IRequestHandler<ScheduleCandidateInterviewCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateCandidateOfferCommand, ApiResponse<Guid>>,
    IRequestHandler<AcceptCandidateOfferCommand, ApiResponse<Guid>>,
    IRequestHandler<UpdatePublishedShiftCommand, ApiResponse<Guid>>,
    IRequestHandler<CreateAttendancePolicyCommand, ApiResponse<Guid>>,
    IRequestHandler<AssignAttendancePolicyCommand, ApiResponse<Guid>>
{
    public async Task<ApiResponse<Guid>> Handle(CreateRequisitionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 300 || request.Openings <= 0 || string.IsNullOrWhiteSpace(request.Requirements) || request.Requirements.Length > 10000)
            return ApiResponse<Guid>.Fail("Invalid requisition", ["REQUISITION_INVALID"]);
        var row = new Requisition { RequisitionNumber = $"REQ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant(),
            Title = request.Title.Trim(), OrganizationUnitId = request.OrganizationUnitId, Openings = request.Openings,
            Requirements = request.Requirements.Trim(), RequestedByUserId = request.ActorUserId, State = RequisitionState.Open };
        db.Requisitions.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(AddCandidateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length > 300 || string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            request.PhoneNumber.Length > 30 || request.Email?.Length > 320 || request.CvAssetReference?.Length > 1000)
            return ApiResponse<Guid>.Fail("Invalid candidate", ["CANDIDATE_INVALID"]);
        if (!await db.Requisitions.AnyAsync(item => item.Id == request.RequisitionId && item.State == RequisitionState.Open, ct))
            return ApiResponse<Guid>.Fail("Requisition not found", ["REQUISITION_NOT_FOUND"]);
        var row = new Candidate { RequisitionId = request.RequisitionId, FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber.Trim(), Email = request.Email, CvAssetReference = request.CvAssetReference };
        db.Candidates.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(ScheduleCandidateInterviewCommand request, CancellationToken ct)
    {
        if (request.InterviewerUserId == Guid.Empty || request.ScheduledAt <= DateTime.UtcNow) return ApiResponse<Guid>.Fail("Invalid interview", ["INTERVIEW_INVALID"]);
        var candidate = await db.Candidates.SingleOrDefaultAsync(item => item.Id == request.CandidateId, ct);
        if (candidate is null) return ApiResponse<Guid>.Fail("Candidate not found", ["CANDIDATE_NOT_FOUND"]);
        candidate.Stage = CandidateStage.Interview; candidate.Version++;
        var row = new CandidateInterview { CandidateId = request.CandidateId, ScheduledAt = request.ScheduledAt, InterviewerUserId = request.InterviewerUserId };
        db.CandidateInterviews.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(CreateCandidateOfferCommand request, CancellationToken ct)
    {
        if (request.BaseSalary < 0 || string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
            return ApiResponse<Guid>.Fail("Invalid offer", ["OFFER_INVALID"]);
        var candidate = await db.Candidates.SingleOrDefaultAsync(item => item.Id == request.CandidateId, ct);
        if (candidate is null) return ApiResponse<Guid>.Fail("Candidate not found", ["CANDIDATE_NOT_FOUND"]);
        candidate.Stage = CandidateStage.Offer; candidate.Version++;
        var row = new CandidateOffer { CandidateId = request.CandidateId, OfferNumber = $"OFF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant(),
            BaseSalary = request.BaseSalary, Currency = request.Currency.ToUpperInvariant(), ProposedStartDate = request.ProposedStartDate, State = OfferState.Sent };
        db.CandidateOffers.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(AcceptCandidateOfferCommand request, CancellationToken ct)
    {
        var offer = await db.CandidateOffers.SingleOrDefaultAsync(item => item.Id == request.OfferId, ct);
        if (offer is null) return ApiResponse<Guid>.Fail("Offer not found", ["OFFER_NOT_FOUND"]);
        if (offer.Version != request.ExpectedVersion || offer.State != OfferState.Sent) return ApiResponse<Guid>.Fail("Offer conflict", ["OFFER_CONFLICT"]);
        offer.State = OfferState.Accepted; offer.AcceptedAt = DateTime.UtcNow; offer.Version++;
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(offer.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(UpdatePublishedShiftCommand request, CancellationToken ct)
    {
        if (request.EffectiveTo.HasValue && request.EffectiveTo <= request.EffectiveFrom || request.Segments.Count == 0 || string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<Guid>.Fail("Invalid assignment", ["SHIFT_ASSIGNMENT_INVALID"]);
        var assignment = await db.ShiftAssignments.Include(item => item.ShiftTemplate).Include(item => item.Employee).ThenInclude(item => item!.User)
            .SingleOrDefaultAsync(item => item.Id == request.AssignmentId && item.Status == ShiftAssignmentStatus.Published, ct);
        if (assignment?.ShiftTemplate is null) return ApiResponse<Guid>.Fail("Assignment not found", ["SHIFT_ASSIGNMENT_NOT_FOUND"]);
        if (await db.ShiftAssignments.AsNoTracking().AnyAsync(item => item.Id != request.AssignmentId && item.EmployeeId == assignment.EmployeeId &&
            item.Status == ShiftAssignmentStatus.Published && item.EffectiveFrom < (request.EffectiveTo ?? DateOnly.MaxValue) &&
            request.EffectiveFrom < (item.EffectiveTo ?? DateOnly.MaxValue), ct)) return ApiResponse<Guid>.Fail("Overlap", ["SHIFT_ASSIGNMENT_OVERLAP"]);
        var segments = request.Segments.Select(item => new ShiftSegment { Sequence = item.Sequence, DayOfWeek = item.DayOfWeek,
            StartsAt = item.StartsAt, EndsAt = item.EndsAt, UnpaidBreakMinutes = item.UnpaidBreakMinutes, WorkDateRule = item.WorkDateRule }).ToList();
        var errors = ShiftScheduleRules.ValidateSegments(segments);
        if (errors.Count > 0) return ApiResponse<Guid>.Fail("Invalid segments", errors.ToList());
        var source = assignment.ShiftTemplate;
        var replacement = new ShiftTemplate { Code = $"EDIT-{Guid.NewGuid():N}", Name = $"جدول أسبوعي: {assignment.Employee?.User?.FullName ?? source.Name}",
            Mode = source.Mode, WorkCalendarId = source.WorkCalendarId, GraceMinutes = source.GraceMinutes,
            MinimumBreakMinutes = source.MinimumBreakMinutes, OvertimeAfterMinutes = source.OvertimeAfterMinutes, Segments = segments };
        db.ShiftTemplates.Add(replacement); assignment.ShiftTemplateId = replacement.Id; assignment.EffectiveFrom = request.EffectiveFrom;
        assignment.EffectiveTo = request.EffectiveTo; assignment.Reason = request.Reason.Trim(); assignment.PublishedByUserId = request.ActorUserId; assignment.PublishedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(assignment.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(CreateAttendancePolicyCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim(); var name = request.Name.Trim();
        if (code.Length is < 2 or > 40 || name.Length is < 2 or > 200) return ApiResponse<Guid>.Fail("Invalid policy", ["POLICY_NAME_OR_CODE_INVALID"]);
        if (await db.AttendancePolicies.AnyAsync(item => item.Code == code, ct)) return ApiResponse<Guid>.Fail("Code exists", ["POLICY_CODE_EXISTS"]);
        if (request.Kind == AttendancePolicyKind.Geofence && (!request.Latitude.HasValue || !request.Longitude.HasValue || request.RadiusMeters <= 0 || request.MaximumAccuracyMeters <= 0))
            return ApiResponse<Guid>.Fail("Geofence required", ["GEOFENCE_CONFIGURATION_REQUIRED"]);
        var row = new AttendancePolicy { Code = code, Name = name, Kind = request.Kind,
            Latitude = request.Kind == AttendancePolicyKind.Geofence ? request.Latitude : null,
            Longitude = request.Kind == AttendancePolicyKind.Geofence ? request.Longitude : null,
            RadiusMeters = request.RadiusMeters, MaximumAccuracyMeters = request.MaximumAccuracyMeters };
        db.AttendancePolicies.Add(row); await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(row.Id);
    }
    public async Task<ApiResponse<Guid>> Handle(AssignAttendancePolicyCommand request, CancellationToken ct)
    {
        if (request.EmployeeId.HasValue == request.ShiftTemplateId.HasValue || request.EffectiveTo.HasValue && request.EffectiveTo <= request.EffectiveFrom)
            return ApiResponse<Guid>.Fail("Invalid assignment", ["POLICY_ASSIGNMENT_INVALID"]);
        if (!await db.AttendancePolicies.AnyAsync(item => item.Id == request.AttendancePolicyId && item.IsActive, ct))
            return ApiResponse<Guid>.Fail("Policy not found", ["ATTENDANCE_POLICY_NOT_FOUND"]);
        if (request.EmployeeId.HasValue && !await db.EmployeeProfiles.AnyAsync(item => item.Id == request.EmployeeId, ct) ||
            request.ShiftTemplateId.HasValue && !await db.ShiftTemplates.AnyAsync(item => item.Id == request.ShiftTemplateId && item.IsActive, ct))
            return ApiResponse<Guid>.Fail("Target not found", ["POLICY_ASSIGNMENT_TARGET_NOT_FOUND"]);
        var existing = await db.AttendancePolicyAssignments.Where(item => item.EmployeeId == request.EmployeeId && item.ShiftTemplateId == request.ShiftTemplateId &&
            item.EffectiveFrom <= request.EffectiveFrom && (!item.EffectiveTo.HasValue || item.EffectiveTo > request.EffectiveFrom))
            .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(ct);
        if (existing?.EffectiveFrom == request.EffectiveFrom) { existing.AttendancePolicyId = request.AttendancePolicyId; existing.EffectiveTo = request.EffectiveTo; }
        else { if (existing is not null) existing.EffectiveTo = request.EffectiveFrom; db.AttendancePolicyAssignments.Add(new AttendancePolicyAssignment
            { AttendancePolicyId = request.AttendancePolicyId, EmployeeId = request.EmployeeId, ShiftTemplateId = request.ShiftTemplateId, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo }); }
        await db.SaveChangesAsync(ct); return ApiResponse<Guid>.Ok(existing?.Id ?? Guid.Empty);
    }
}

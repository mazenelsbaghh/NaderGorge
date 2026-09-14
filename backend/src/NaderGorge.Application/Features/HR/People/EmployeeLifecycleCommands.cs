using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.Organization;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.People;

public sealed record CreateEmploymentAssignmentCommand(
    Guid EmployeeId, Guid OrganizationUnitId, Guid? JobPositionId, Guid? JobGradeId,
    Guid? ManagerEmployeeId, Guid? WorkLocationId, Guid? CostCenterId,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ChangeReason, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.OrganizationManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
    public Guid? ResourceEmployeeId => EmployeeId;
}

public sealed class CreateEmploymentAssignmentCommandHandler
    : IRequestHandler<CreateEmploymentAssignmentCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;
    public CreateEmploymentAssignmentCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<Guid>> Handle(CreateEmploymentAssignmentCommand request, CancellationToken ct)
    {
        if (HrOrganizationRules.ValidateManager(request.EmployeeId, request.ManagerEmployeeId) is { } managerError)
            return ApiResponse<Guid>.Fail("لا يمكن للموظف أن يكون مدير نفسه", [managerError]);
        if (request.EffectiveTo < request.EffectiveFrom)
            return ApiResponse<Guid>.Fail("نهاية التكليف تسبق بدايته", ["ASSIGNMENT_PERIOD_INVALID"]);
        if (!await _db.EmployeeProfiles.AnyAsync(item => item.Id == request.EmployeeId, ct) ||
            !await _db.OrganizationUnits.AnyAsync(item => item.Id == request.OrganizationUnitId && item.IsActive, ct))
            return ApiResponse<Guid>.Fail("الموظف أو الوحدة التنظيمية غير موجود", ["ASSIGNMENT_REFERENCE_NOT_FOUND"]);

        var existingPeriods = await _db.EmploymentAssignments
            .Where(item => item.EmployeeId == request.EmployeeId)
            .Select(item => new { item.EffectiveFrom, item.EffectiveTo })
            .ToListAsync(ct);
        if (existingPeriods.Any(item => HrOrganizationRules.PeriodsOverlap(
                item.EffectiveFrom, item.EffectiveTo, request.EffectiveFrom, request.EffectiveTo)))
            return ApiResponse<Guid>.Fail("يوجد تكليف وظيفي متداخل", ["ASSIGNMENT_PERIOD_OVERLAP"]);

        var assignment = new EmploymentAssignment
        {
            EmployeeId = request.EmployeeId,
            OrganizationUnitId = request.OrganizationUnitId,
            JobPositionId = request.JobPositionId,
            JobGradeId = request.JobGradeId,
            ManagerEmployeeId = request.ManagerEmployeeId,
            WorkLocationId = request.WorkLocationId,
            CostCenterId = request.CostCenterId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ChangeReason = request.ChangeReason.Trim()
        };
        _db.EmploymentAssignments.Add(assignment);
        await _audit.WriteMutationAsync("CreateEmploymentAssignment", nameof(EmploymentAssignment), assignment.Id,
            null, new { assignment.OrganizationUnitId, assignment.JobPositionId, assignment.ManagerEmployeeId, assignment.EffectiveFrom, assignment.EffectiveTo },
            request.ChangeReason, ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(assignment.Id);
    }
}

public sealed record CreateEmploymentContractCommand(
    Guid EmployeeId, string ContractNumber, EmploymentContractType Type,
    DateOnly StartDate, DateOnly? EndDate, DateOnly? ProbationEndDate,
    decimal BaseSalary, string Currency, string? TermsJson, Guid ActorUserId)
    : IRequest<ApiResponse<Guid>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ContractManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
    public Guid? ResourceEmployeeId => EmployeeId;
}

public sealed class CreateEmploymentContractCommandHandler
    : IRequestHandler<CreateEmploymentContractCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;
    public CreateEmploymentContractCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<Guid>> Handle(CreateEmploymentContractCommand request, CancellationToken ct)
    {
        if (request.EndDate < request.StartDate || request.BaseSalary < 0)
            return ApiResponse<Guid>.Fail("بيانات العقد غير صالحة", ["CONTRACT_INVALID"]);
        if (await _db.EmploymentContracts.AnyAsync(item => item.ContractNumber == request.ContractNumber, ct))
            return ApiResponse<Guid>.Fail("رقم العقد مستخدم", ["CONTRACT_NUMBER_EXISTS"]);
        var periods = await _db.EmploymentContracts.Where(item => item.EmployeeId == request.EmployeeId)
            .Select(item => new { item.StartDate, item.EndDate }).ToListAsync(ct);
        if (periods.Any(item => HrOrganizationRules.PeriodsOverlap(item.StartDate, item.EndDate, request.StartDate, request.EndDate)))
            return ApiResponse<Guid>.Fail("يوجد عقد متداخل", ["CONTRACT_PERIOD_OVERLAP"]);

        var contract = new EmploymentContract
        {
            EmployeeId = request.EmployeeId, ContractNumber = request.ContractNumber.Trim(), Type = request.Type,
            StartDate = request.StartDate, EndDate = request.EndDate, ProbationEndDate = request.ProbationEndDate,
            BaseSalary = request.BaseSalary, Currency = request.Currency.Trim().ToUpperInvariant(), TermsJson = request.TermsJson
        };
        _db.EmploymentContracts.Add(contract);
        await _audit.WriteMutationAsync("CreateEmploymentContract", nameof(EmploymentContract), contract.Id, null,
            new { contract.ContractNumber, contract.Type, contract.StartDate, contract.EndDate, contract.ProbationEndDate, contract.BaseSalary, contract.Currency },
            "Create employment contract", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Guid>.Ok(contract.Id);
    }
}

public sealed record TransitionEmploymentContractCommand(Guid ContractId, EmploymentContractStatus NextStatus, Guid ActorUserId)
    : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.ContractManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
}

public sealed class TransitionEmploymentContractCommandHandler
    : IRequestHandler<TransitionEmploymentContractCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;
    public TransitionEmploymentContractCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<bool>> Handle(TransitionEmploymentContractCommand request, CancellationToken ct)
    {
        var contract = await _db.EmploymentContracts.SingleOrDefaultAsync(item => item.Id == request.ContractId, ct);
        if (contract is null) return ApiResponse<bool>.Fail("العقد غير موجود", ["CONTRACT_NOT_FOUND"]);
        if (!HrOrganizationRules.CanTransitionContract(contract.Status, request.NextStatus))
            return ApiResponse<bool>.Fail("انتقال حالة العقد غير مسموح", ["CONTRACT_TRANSITION_INVALID"]);
        var previous = contract.Status;
        contract.Status = request.NextStatus;
        await _audit.WriteMutationAsync("TransitionEmploymentContract", nameof(EmploymentContract), contract.Id,
            new { status = previous }, new { status = request.NextStatus }, "Transition employment contract status", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

public sealed record CompleteEmployeeExitCommand(Guid EmployeeId, DateOnly TerminationDate, Guid ActorUserId)
    : IRequest<ApiResponse<bool>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.EmployeeManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
    public Guid? ResourceEmployeeId => EmployeeId;
}

public sealed class CompleteEmployeeExitCommandHandler
    : IRequestHandler<CompleteEmployeeExitCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IHrAuditWriter _audit;
    public CompleteEmployeeExitCommandHandler(IAppDbContext db, IHrAuditWriter? audit = null)
    {
        _db = db;
        _audit = audit ?? new HrAuditWriter(db, DetachedHrRequestContext.Instance);
    }

    public async Task<ApiResponse<bool>> Handle(CompleteEmployeeExitCommand request, CancellationToken ct)
    {
        var profile = await _db.EmployeeProfiles.Include(item => item.User).SingleOrDefaultAsync(item => item.Id == request.EmployeeId, ct);
        if (profile is null) return ApiResponse<bool>.Fail("الموظف غير موجود", ["EMPLOYEE_NOT_FOUND"]);
        var previous = new { profile.EmploymentStatus, profile.TerminationDate, userActive = profile.User?.IsActive };
        profile.EmploymentStatus = EmployeeEmploymentStatus.Terminated;
        profile.TerminationDate = request.TerminationDate;
        if (profile.User is not null)
        {
            profile.User.IsActive = false;
            profile.User.SecurityStampVersion += 1;
        }
        await _audit.WriteMutationAsync("CompleteEmployeeExit", nameof(EmployeeProfile), profile.Id, previous,
            new { profile.EmploymentStatus, profile.TerminationDate, userActive = profile.User?.IsActive },
            "Complete employee offboarding", ct, request.ActorUserId);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}

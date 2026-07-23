using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Common.HR;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record AdminSaveEmployeeProfileCommand(
    Guid UserId,
    decimal BasicSalary,
    string StandardStartTime, // e.g. "09:00:00"
    int TargetDailyHours,
    DateTime? ExpectedUpdatedAt = null,
    Guid? ActorUserId = null
) : IRequest<ApiResponse<EmployeeProfileMutationResult>>, IHrAuthorizedRequest
{
    public string RequiredPermission => HrPermissions.EmployeeManage;
    public HrAccessScope RequiredScope => HrAccessScope.All;
    public Guid? ResourceUserId => UserId;
}

public record EmployeeProfileMutationResult(Guid Id, Guid UserId, DateTime? UpdatedAt, string? RowVersion);

public class AdminSaveEmployeeProfileCommandValidator : AbstractValidator<AdminSaveEmployeeProfileCommand>
{
    public AdminSaveEmployeeProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
        RuleFor(x => x.BasicSalary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetDailyHours).InclusiveBetween(1, 24);
        RuleFor(x => x.StandardStartTime).NotEmpty().Must(BeAValidTimeSpan).WithMessage("Time must be in format hh:mm or hh:mm:ss");
    }

    private bool BeAValidTimeSpan(string timeStr)
    {
        return TimeSpan.TryParse(timeStr, out _);
    }
}

public class AdminSaveEmployeeProfileCommandHandler : IRequestHandler<AdminSaveEmployeeProfileCommand, ApiResponse<EmployeeProfileMutationResult>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;
    private readonly IHrAuditWriter? _hrAudit;

    public AdminSaveEmployeeProfileCommandHandler(IAppDbContext db, IAuditRepository audit, IHrAuditWriter? hrAudit = null)
    {
        _db = db;
        _audit = audit;
        _hrAudit = hrAudit;
    }

    public async Task<ApiResponse<EmployeeProfileMutationResult>> Handle(AdminSaveEmployeeProfileCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.EmployeeProfile)
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new KeyNotFoundException("User not found");

        var userRoles = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == request.UserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        if (userRoles.Contains("Student") && userRoles.Count == 1)
        {
            throw new InvalidOperationException("Cannot configure employee profile for a Student user.");
        }

        var parsedTime = TimeSpan.Parse(request.StandardStartTime);

        string? oldValues = null;
        string? newValues = null;
        bool isNew = user.EmployeeProfile == null;

        if (isNew)
        {
            var profile = new EmployeeProfile
            {
                UserId = request.UserId,
                BasicSalary = request.BasicSalary,
                StandardStartTime = parsedTime,
                TargetDailyHours = request.TargetDailyHours
            };
            profile.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(profile.Id);
            user.EmployeeProfile = profile;
            _db.EmployeeProfiles.Add(profile);

            newValues = $"BasicSalary: {request.BasicSalary}, StartTime: {parsedTime}, DailyHours: {request.TargetDailyHours}";
        }
        else
        {
            if (request.ExpectedUpdatedAt.HasValue
                && user.EmployeeProfile!.UpdatedAt != request.ExpectedUpdatedAt.Value)
            {
                return ApiResponse<EmployeeProfileMutationResult>.Fail(
                    "Employee profile was changed by another user. Reload the latest profile before saving.",
                    new List<string> { "EMPLOYEE_PROFILE_CONFLICT" });
            }

            oldValues = $"BasicSalary: {user.EmployeeProfile!.BasicSalary}, StartTime: {user.EmployeeProfile.StandardStartTime}, DailyHours: {user.EmployeeProfile.TargetDailyHours}";

            user.EmployeeProfile!.BasicSalary = request.BasicSalary;
            user.EmployeeProfile.StandardStartTime = parsedTime;
            user.EmployeeProfile.TargetDailyHours = request.TargetDailyHours;

            newValues = $"BasicSalary: {request.BasicSalary}, StartTime: {parsedTime}, DailyHours: {request.TargetDailyHours}";
        }

        if (_hrAudit is not null)
        {
            await _hrAudit.WriteMutationAsync(
                isNew ? "CreateEmployeeProfile" : "UpdateEmployeeProfile", nameof(EmployeeProfile),
                user.EmployeeProfile.Id,
                isNew ? null : new { basicSalary = oldValues, startTime = user.EmployeeProfile.StandardStartTime, dailyHours = user.EmployeeProfile.TargetDailyHours },
                new { basicSalary = request.BasicSalary, startTime = parsedTime, dailyHours = request.TargetDailyHours },
                isNew ? "Create employee profile" : "Update employee profile", ct, request.ActorUserId);
        }
        else
        {
            await _audit.AddAsync(new AuditLog
            {
                Action = isNew ? "CreateEmployeeProfile" : "UpdateEmployeeProfile",
                EntityType = nameof(EmployeeProfile), EntityId = user.EmployeeProfile.Id,
                PerformedByUserId = request.ActorUserId, OldValues = oldValues, NewValues = newValues,
                Reason = isNew ? "Create employee profile" : "Update employee profile"
            });
        }
        await _db.SaveChangesAsync(ct);

        return ApiResponse<EmployeeProfileMutationResult>.Ok(
            new EmployeeProfileMutationResult(
                user.EmployeeProfile.Id,
                user.Id,
                user.EmployeeProfile.UpdatedAt,
                user.EmployeeProfile.UpdatedAt?.ToString("O")));
    }
}

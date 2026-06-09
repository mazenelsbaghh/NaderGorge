using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.HR.Commands;

public record AdminSaveEmployeeProfileCommand(
    Guid UserId,
    decimal BasicSalary,
    string StandardStartTime, // e.g. "09:00:00"
    int TargetDailyHours
) : IRequest<ApiResponse<Guid>>;

public class AdminSaveEmployeeProfileCommandValidator : AbstractValidator<AdminSaveEmployeeProfileCommand>
{
    public AdminSaveEmployeeProfileCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.BasicSalary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetDailyHours).InclusiveBetween(1, 24);
        RuleFor(x => x.StandardStartTime).NotEmpty().Must(BeAValidTimeSpan).WithMessage("Time must be in format hh:mm or hh:mm:ss");
    }

    private bool BeAValidTimeSpan(string timeStr)
    {
        return TimeSpan.TryParse(timeStr, out _);
    }
}

public class AdminSaveEmployeeProfileCommandHandler : IRequestHandler<AdminSaveEmployeeProfileCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;
    private readonly IAuditRepository _audit;

    public AdminSaveEmployeeProfileCommandHandler(IAppDbContext db, IAuditRepository audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<Guid>> Handle(AdminSaveEmployeeProfileCommand request, CancellationToken ct)
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
            user.EmployeeProfile = profile;
            _db.EmployeeProfiles.Add(profile);

            newValues = $"BasicSalary: {request.BasicSalary}, StartTime: {parsedTime}, DailyHours: {request.TargetDailyHours}";
        }
        else
        {
            oldValues = $"BasicSalary: {user.EmployeeProfile!.BasicSalary}, StartTime: {user.EmployeeProfile.StandardStartTime}, DailyHours: {user.EmployeeProfile.TargetDailyHours}";

            user.EmployeeProfile!.BasicSalary = request.BasicSalary;
            user.EmployeeProfile.StandardStartTime = parsedTime;
            user.EmployeeProfile.TargetDailyHours = request.TargetDailyHours;

            newValues = $"BasicSalary: {request.BasicSalary}, StartTime: {parsedTime}, DailyHours: {request.TargetDailyHours}";
        }

        await _db.SaveChangesAsync(ct);

        var auditEntry = new AuditLog
        {
            Action = isNew ? "CreateEmployeeProfile" : "UpdateEmployeeProfile",
            EntityType = nameof(EmployeeProfile),
            EntityId = user.EmployeeProfile.Id,
            PerformedByUserId = null,
            OldValues = oldValues,
            NewValues = newValues,
            CreatedAt = DateTime.UtcNow
        };
        await _audit.AddAsync(auditEntry);

        return ApiResponse<Guid>.Ok(user.EmployeeProfile.Id);
    }
}

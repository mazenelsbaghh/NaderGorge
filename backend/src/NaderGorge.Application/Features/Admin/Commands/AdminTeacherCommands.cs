using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateTeacherProfileCommand(
    Guid UserId,
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds) : IRequest<ApiResponse<Guid>>;

public class CreateTeacherProfileCommandHandler : IRequestHandler<CreateTeacherProfileCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateTeacherProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<Guid>> Handle(CreateTeacherProfileCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct);
        if (user == null)
            return ApiResponse<Guid>.Fail("User not found");

        var profileExists = await _db.TeacherProfiles.AnyAsync(tp => tp.UserId == request.UserId, ct);
        if (profileExists)
            return ApiResponse<Guid>.Fail("Teacher profile already exists for this user");

        // Validate subjects
        if (request.SubjectIds.Any())
        {
            var dbSubjectIds = await _db.Subjects
                .Where(s => request.SubjectIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (dbSubjectIds.Count != request.SubjectIds.Count)
                return ApiResponse<Guid>.Fail("One or more subject IDs are invalid.");
        }

        var profile = new TeacherProfile
        {
            UserId = request.UserId,
            Bio = request.Bio ?? string.Empty,
            Specialization = request.Specialization ?? string.Empty,
            CommissionRate = request.CommissionRate,
            ProfileImageUrl = request.ProfileImageUrl,
            ContactInfo = request.ContactInfo ?? string.Empty
        };

        foreach (var subId in request.SubjectIds)
        {
            profile.TeacherSubjects.Add(new TeacherSubject { SubjectId = subId });
        }

        _db.TeacherProfiles.Add(profile);

        // Auto-assign Teacher role if not present
        var teacherRole = await _db.Roles.FirstOrDefaultAsync(r => r.Type == RoleType.Teacher, ct);
        if (teacherRole != null)
        {
            var hasRole = await _db.UserRoles.AnyAsync(ur => ur.UserId == request.UserId && ur.RoleId == teacherRole.Id, ct);
            if (!hasRole)
            {
                _db.UserRoles.Add(new UserRole { UserId = request.UserId, RoleId = teacherRole.Id });
            }
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(profile.Id);
    }
}

public record UpdateTeacherProfileCommand(
    Guid Id,
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds) : IRequest<ApiResponse>;

public class UpdateTeacherProfileCommandHandler : IRequestHandler<UpdateTeacherProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateTeacherProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateTeacherProfileCommand request, CancellationToken ct)
    {
        var profile = await _db.TeacherProfiles
            .Include(tp => tp.TeacherSubjects)
            .FirstOrDefaultAsync(tp => tp.Id == request.Id, ct);

        if (profile == null)
            return ApiResponse.Fail("Teacher profile not found");

        // Validate subjects
        if (request.SubjectIds.Any())
        {
            var dbSubjectIds = await _db.Subjects
                .Where(s => request.SubjectIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(ct);

            if (dbSubjectIds.Count != request.SubjectIds.Count)
                return ApiResponse.Fail("One or more subject IDs are invalid.");
        }

        profile.Bio = request.Bio ?? string.Empty;
        profile.Specialization = request.Specialization ?? string.Empty;
        profile.CommissionRate = request.CommissionRate;
        profile.ProfileImageUrl = request.ProfileImageUrl;
        profile.ContactInfo = request.ContactInfo ?? string.Empty;

        // Sync subjects
        var toRemove = profile.TeacherSubjects.Where(ts => !request.SubjectIds.Contains(ts.SubjectId)).ToList();
        foreach (var ts in toRemove)
        {
            profile.TeacherSubjects.Remove(ts);
        }

        var existingSubjectIds = profile.TeacherSubjects.Select(ts => ts.SubjectId).ToList();
        foreach (var subId in request.SubjectIds)
        {
            if (!existingSubjectIds.Contains(subId))
            {
                profile.TeacherSubjects.Add(new TeacherSubject { SubjectId = subId });
            }
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}

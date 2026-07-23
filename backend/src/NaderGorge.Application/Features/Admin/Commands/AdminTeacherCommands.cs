using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using System.Text.Json;

namespace NaderGorge.Application.Features.Admin.Commands;

public record CreateTeacherProfileCommand(
    Guid UserId,
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds,
    string? AssistantPhoneNumbers,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? TelegramUrl,
    bool ShowOnLanding = true) : IRequest<ApiResponse<Guid>>;

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
            ContactInfo = request.ContactInfo ?? string.Empty,
            AssistantPhoneNumbers = request.AssistantPhoneNumbers,
            FacebookUrl = request.FacebookUrl,
            YouTubeUrl = request.YouTubeUrl,
            TelegramUrl = request.TelegramUrl,
            ShowOnLanding = request.ShowOnLanding
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
    Guid AdminId,
    string FullName,
    string PhoneNumber,
    string? NewPassword,
    string Bio,
    string Specialization,
    decimal CommissionRate,
    string? ProfileImageUrl,
    string ContactInfo,
    List<Guid> SubjectIds,
    string? AssistantPhoneNumbers,
    string? FacebookUrl,
    string? YouTubeUrl,
    string? TelegramUrl,
    string? IntroVideoUrl,
    bool ShowOnLanding,
    bool IsVisibleToStudents,
    bool IsContentVisibleToStudents) : IRequest<ApiResponse>
{
    public UpdateTeacherProfileCommand(
        Guid id,
        string bio,
        string specialization,
        decimal commissionRate,
        string? profileImageUrl,
        string contactInfo,
        List<Guid> subjectIds,
        string? assistantPhoneNumbers,
        string? facebookUrl,
        string? youtubeUrl,
        string? telegramUrl,
        string? introVideoUrl = null,
        bool showOnLanding = true)
        : this(id, Guid.Empty, string.Empty, string.Empty, null, bio, specialization, commissionRate,
            profileImageUrl, contactInfo, subjectIds, assistantPhoneNumbers, facebookUrl, youtubeUrl,
            telegramUrl, introVideoUrl, showOnLanding, true, true)
    {
    }
}

public class UpdateTeacherProfileCommandHandler : IRequestHandler<UpdateTeacherProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateTeacherProfileCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse> Handle(UpdateTeacherProfileCommand request, CancellationToken ct)
    {
        var profile = await _db.TeacherProfiles
            .Include(tp => tp.User)
            .Include(tp => tp.TeacherSubjects)
            .FirstOrDefaultAsync(tp => tp.Id == request.Id, ct);

        if (profile == null)
            return ApiResponse.Fail("Teacher profile not found");

        var isAdminUpdate = request.AdminId != Guid.Empty;
        var normalizedPhone = request.PhoneNumber.Trim();
        if (isAdminUpdate && (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(normalizedPhone)))
            return ApiResponse.Fail("الاسم ورقم الهاتف مطلوبان.");

        if (isAdminUpdate && await _db.Users.AnyAsync(
                user => user.Id != profile.UserId && user.PhoneNumber == normalizedPhone,
                ct))
            return ApiResponse.Fail("رقم الهاتف مستخدم بالفعل.");

        if (!string.IsNullOrWhiteSpace(request.NewPassword) && request.NewPassword.Length < 8)
            return ApiResponse.Fail("كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل.");

        if (request.CommissionRate is < 0 or > 100)
            return ApiResponse.Fail("نسبة العمولة يجب أن تكون بين 0 و100.");

        if (request.SubjectIds.Distinct().Count() != request.SubjectIds.Count)
            return ApiResponse.Fail("لا يمكن تكرار المادة.");

        var requestedSubjectIds = request.SubjectIds.Distinct().ToList();
        if (requestedSubjectIds.Count > 0)
        {
            var existingSubjectIds = await _db.Subjects
                .Where(subject => requestedSubjectIds.Contains(subject.Id))
                .Select(subject => subject.Id)
                .ToListAsync(ct);
            if (existingSubjectIds.Count != requestedSubjectIds.Count)
                return ApiResponse.Fail("One or more subject IDs are invalid.");
        }

        var oldValues = new
        {
            fullName = profile.User.FullName,
            phoneNumber = profile.User.PhoneNumber,
            bio = profile.Bio,
            specialization = profile.Specialization,
            commissionRate = profile.CommissionRate,
            showOnLanding = profile.ShowOnLanding,
            isVisibleToStudents = profile.IsVisibleToStudents,
            isContentVisibleToStudents = profile.IsContentVisibleToStudents
        };

        if (isAdminUpdate)
        {
            profile.User.FullName = request.FullName.Trim();
            profile.User.PhoneNumber = normalizedPhone;
        }
        profile.Bio = request.Bio?.Trim() ?? string.Empty;
        profile.Specialization = request.Specialization?.Trim() ?? string.Empty;
        profile.CommissionRate = request.CommissionRate;
        profile.ProfileImageUrl = request.ProfileImageUrl;
        profile.ContactInfo = request.ContactInfo?.Trim() ?? string.Empty;
        profile.AssistantPhoneNumbers = request.AssistantPhoneNumbers;
        profile.FacebookUrl = request.FacebookUrl;
        profile.YouTubeUrl = request.YouTubeUrl;
        profile.TelegramUrl = request.TelegramUrl;
        profile.IntroVideoUrl = request.IntroVideoUrl;
        profile.ShowOnLanding = request.ShowOnLanding;
        profile.IsVisibleToStudents = request.IsVisibleToStudents;
        profile.IsContentVisibleToStudents = request.IsContentVisibleToStudents;

        if (isAdminUpdate && !string.IsNullOrWhiteSpace(request.NewPassword))
        {
            profile.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            profile.User.PasswordResetVersion += 1;
            profile.User.SecurityStampVersion += 1;
            var activeTokens = await _db.RefreshTokens
                .Where(token => token.UserId == profile.UserId && !token.IsRevoked)
                .ToListAsync(ct);
            foreach (var token in activeTokens)
                token.IsRevoked = true;
        }

        // Sync subjects
        var toRemove = profile.TeacherSubjects.Where(ts => !requestedSubjectIds.Contains(ts.SubjectId)).ToList();
        foreach (var ts in toRemove)
        {
            profile.TeacherSubjects.Remove(ts);
        }

        var linkedSubjectIds = profile.TeacherSubjects.Select(ts => ts.SubjectId).ToList();
        foreach (var subId in requestedSubjectIds)
        {
            if (!linkedSubjectIds.Contains(subId))
            {
                profile.TeacherSubjects.Add(new TeacherSubject { SubjectId = subId });
            }
        }

        var newValues = new
        {
            fullName = profile.User.FullName,
            phoneNumber = profile.User.PhoneNumber,
            bio = profile.Bio,
            specialization = profile.Specialization,
            commissionRate = profile.CommissionRate,
            showOnLanding = profile.ShowOnLanding,
            isVisibleToStudents = profile.IsVisibleToStudents,
            isContentVisibleToStudents = profile.IsContentVisibleToStudents,
            passwordChanged = !string.IsNullOrWhiteSpace(request.NewPassword)
        };

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateTeacherProfile",
            EntityType = "TeacherProfile",
            EntityId = profile.Id,
            PerformedByUserId = request.AdminId == Guid.Empty ? null : request.AdminId,
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            IpAddress = "Admin"
        });

        await _db.SaveChangesAsync(ct);

        return ApiResponse.Ok();
    }
}

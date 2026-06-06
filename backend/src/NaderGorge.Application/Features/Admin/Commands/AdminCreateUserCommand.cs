using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record AdminCreateUserCommand(
    string FullName,
    string PhoneNumber,
    string Password,
    string Role,            // "Admin" | "Assistant" | "Student"
    List<Guid>? PackageIds  // optional, for Student role
) : IRequest<ApiResponse<AdminCreateUserResult>>;

public record AdminCreateUserResult(Guid Id, string FullName, string PhoneNumber, string Role);

public class AdminCreateUserCommandHandler : IRequestHandler<AdminCreateUserCommand, ApiResponse<AdminCreateUserResult>>
{
    private readonly IAppDbContext _context;

    public AdminCreateUserCommandHandler(IAppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<AdminCreateUserResult>> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate phone uniqueness
        var phoneExists = await _context.Users
            .AnyAsync(u => u.PhoneNumber == request.PhoneNumber, cancellationToken);

        if (phoneExists)
            return ApiResponse<AdminCreateUserResult>.Fail(
                "رقم الهاتف مسجل بالفعل",
                new List<string> { "PHONE_ALREADY_EXISTS" });

        // 2. Validate role
        var validRoles = new[] { "Admin", "Assistant", "Student" };
        if (!Array.Exists(validRoles, r => r.Equals(request.Role, StringComparison.OrdinalIgnoreCase)))
            return ApiResponse<AdminCreateUserResult>.Fail(
                "الدور غير صالح",
                new List<string> { "INVALID_ROLE" });

        // 3. Validate password
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return ApiResponse<AdminCreateUserResult>.Fail(
                "كلمة السر يجب أن تكون 6 أحرف على الأقل",
                new List<string> { "PASSWORD_TOO_SHORT" });

        // 4. Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // 5. Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            PasswordHash = passwordHash,
            IsActive = true,
            IsProfileComplete = request.Role != "Student" // non-students are immediately complete
        };
        _context.Users.Add(user);

        // 6. Find and assign role
        var roleEntity = await _context.Roles
            .FirstOrDefaultAsync(r => r.Name == request.Role, cancellationToken);

        if (roleEntity == null)
            return ApiResponse<AdminCreateUserResult>.Fail(
                "لم يُعثر على الدور في النظام",
                new List<string> { "ROLE_NOT_FOUND" });

        _context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = roleEntity.Id
        });

        // 7. If student: create minimal profile + enrol in packages
        if (request.Role == "Student")
        {
            var profile = new StudentProfile
            {
                UserId = user.Id,
                DateOfBirth = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Gender = Gender.Male,
                Governorate = "غير محدد",
                Address = "غير محدد",
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary
            };
            _context.StudentProfiles.Add(profile);

            if (request.PackageIds != null && request.PackageIds.Count > 0)
            {
                foreach (var packageId in request.PackageIds)
                {
                    var packageExists = await _context.Packages
                        .AnyAsync(p => p.Id == packageId, cancellationToken);

                    if (!packageExists) continue;

                    // Check not already granted
                    var alreadyGranted = await _context.StudentAccessGrants
                        .AnyAsync(g => g.UserId == user.Id && g.PackageId == packageId && g.IsActive, cancellationToken);

                    if (!alreadyGranted)
                    {
                        _context.StudentAccessGrants.Add(new StudentAccessGrant
                        {
                            UserId = user.Id,
                            PackageId = packageId,
                            GrantType = CodeType.Package,
                            GrantedAt = DateTime.UtcNow,
                            IsActive = true
                        });
                    }
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse<AdminCreateUserResult>.Ok(
            new AdminCreateUserResult(user.Id, user.FullName, user.PhoneNumber, request.Role),
            "تم إنشاء المستخدم بنجاح");
    }
}

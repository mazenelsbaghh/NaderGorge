using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Roles.AnyAsync()) return;

        var roles = new[]
        {
            new Role { Name = "Admin", Type = RoleType.Admin },
            new Role { Name = "Teacher", Type = RoleType.Teacher },
            new Role { Name = "Assistant", Type = RoleType.Assistant },
            new Role { Name = "Student", Type = RoleType.Student }
        };
        db.Roles.AddRange(roles);

        // Create default admin user
        var adminRole = roles[0];
        var admin = new User
        {
            FullName = "System Admin",
            PhoneNumber = "01000000000",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            IsActive = true,
            IsProfileComplete = true
        };
        db.Users.Add(admin);
        db.UserRoles.Add(new UserRole { User = admin, Role = adminRole });

        // Create default student user
        var studentRole = roles[3];
        var student = new User
        {
            FullName = "Student User",
            PhoneNumber = "01234567890",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Student@123"),
            IsActive = true,
            IsProfileComplete = true
        };
        db.Users.Add(student);
        db.UserRoles.Add(new UserRole { User = student, Role = studentRole });

        await db.SaveChangesAsync();
    }
}

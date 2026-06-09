using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db, bool seedDefaultUsers = false)
    {
        var rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name);
        var defaultRoles = new[]
        {
            new Role { Name = "Admin", Type = RoleType.Admin },
            new Role { Name = "Teacher", Type = RoleType.Teacher },
            new Role 
            { 
                Name = "Assistant", 
                Type = RoleType.Assistant,
                PermissionsJson = "[\"comments.manage\",\"community.manage\",\"exams.manage\",\"watch_requests.manage\",\"tasks.manage\",\"chat.manage\"]"
            },
            new Role { Name = "Student", Type = RoleType.Student },
            new Role 
            { 
                Name = "Supervisor", 
                Type = RoleType.Supervisor,
                PermissionsJson = "[\"users.manage\",\"content.manage\",\"exams.manage\",\"codes.manage\",\"watch_requests.manage\",\"community.manage\",\"comments.manage\",\"hr.manage\",\"tasks.manage\",\"chat.manage\",\"crm.manage\",\"payments.manage\",\"media.manage\",\"finance.manage\",\"reports.manage\"]"
            },
            new Role 
            { 
                Name = "Staff", 
                Type = RoleType.Staff,
                PermissionsJson = "[\"users.manage\",\"watch_requests.manage\",\"community.manage\",\"comments.manage\",\"tasks.manage\",\"chat.manage\",\"crm.manage\",\"payments.manage\"]"
            }
        };

        var addedAny = false;
        foreach (var defaultRole in defaultRoles)
        {
            if (!rolesByName.ContainsKey(defaultRole.Name))
            {
                db.Roles.Add(defaultRole);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            await db.SaveChangesAsync();
            rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name);
        }

        if (!seedDefaultUsers) return;

        // Create default admin user
        var adminRole = rolesByName["Admin"];
        if (!await db.Users.AnyAsync(u => u.PhoneNumber == "01000000000"))
        {
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
        }

        // Create default student user
        var studentRole = rolesByName["Student"];
        if (!await db.Users.AnyAsync(u => u.PhoneNumber == "01234567890"))
        {
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
        }

        await db.SaveChangesAsync();
    }
}

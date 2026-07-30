using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.CRM.Commands;
using NaderGorge.Application.Features.CRM.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.CRM;

public class CrmSecurityTests
{
    private async Task<(User Student, User Assistant, Role StudentRole, Role AssistantRole)> SeedBasicUsersAsync(AppDbContext db)
    {
        var student = await TestAppDbContextFactory.SeedUserAsync(db, "Student User", "01234567800");
        var assistant = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant User", "01234567801");

        var studentRole = new Role { Id = Guid.NewGuid(), Name = "Student", Type = RoleType.Student };
        var assistantRole = new Role { Id = Guid.NewGuid(), Name = "Assistant", Type = RoleType.Assistant };

        db.Roles.AddRange(studentRole, assistantRole);
        db.UserRoles.AddRange(
            new UserRole { UserId = student.Id, RoleId = studentRole.Id },
            new UserRole { UserId = assistant.Id, RoleId = assistantRole.Id }
        );
        await db.SaveChangesAsync();

        return (student, assistant, studentRole, assistantRole);
    }

    [Fact]
    public async Task GetCrmStudents_FiltersForAssistant_ToOnlyReturnAssignedStudents()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student1, assistant1, _, _) = await SeedBasicUsersAsync(db);
        
        var student2 = await TestAppDbContextFactory.SeedUserAsync(db, "Student 2", "01234567802");
        var assistant2 = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant 2", "01234567803");

        var studentRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Student);
        var assistantRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Assistant);

        db.UserRoles.AddRange(
            new UserRole { UserId = student2.Id, RoleId = studentRole.Id },
            new UserRole { UserId = assistant2.Id, RoleId = assistantRole.Id }
        );
        await db.SaveChangesAsync();

        // Assign student1 to assistant1
        var status1 = new CrmStudentStatus
        {
            StudentId = student1.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant1.Id,
            Priority = CrmPriority.Medium
        };

        // Assign student2 to assistant2
        var status2 = new CrmStudentStatus
        {
            StudentId = student2.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant2.Id,
            Priority = CrmPriority.Medium
        };

        db.CrmStudentStatuses.AddRange(status1, status2);
        await db.SaveChangesAsync();

        var handler = new GetCrmStudentsQueryHandler(db);

        // Act - query as assistant1
        var query = new GetCrmStudentsQuery(assistant1.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal(student1.Id, result.Data.Items[0].StudentId);
    }

    [Fact]
    public async Task GetCrmStudents_ReturnsAllForAdminOrSupervisor()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student1, assistant1, _, _) = await SeedBasicUsersAsync(db);
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01234567804");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });

        // Assign student1 to assistant1
        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student1.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant1.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new GetCrmStudentsQueryHandler(db);

        // Act - query as admin
        var query = new GetCrmStudentsQuery(admin.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // Should return the student (even if unassigned / assigned to others)
        Assert.Contains(result.Data!.Items, item => item.StudentId == student1.Id);
    }

    [Fact]
    public async Task GetCrmStudentHistory_FailsForUnassignedAssistant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student1, assistant1, _, _) = await SeedBasicUsersAsync(db);
        var assistant2 = await TestAppDbContextFactory.SeedUserAsync(db, "Assistant 2", "01234567805");
        var assistantRole = await db.Roles.FirstAsync(r => r.Type == RoleType.Assistant);
        db.UserRoles.Add(new UserRole { UserId = assistant2.Id, RoleId = assistantRole.Id });

        // Assign student1 to assistant1
        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student1.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant1.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new GetCrmStudentHistoryQueryHandler(db);

        // Act - query history as assistant2
        var query = new GetCrmStudentHistoryQuery(student1.Id, assistant2.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("You are not authorized to view this student's call log history.", result.Message);
    }

    [Fact]
    public async Task GetCrmStudentHistory_SucceedsForAssignedAssistant()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student1, assistant1, _, _) = await SeedBasicUsersAsync(db);

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student1.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant1.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new GetCrmStudentHistoryQueryHandler(db);

        // Act - query history as assistant1
        var query = new GetCrmStudentHistoryQuery(student1.Id, assistant1.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetCrmStudentHistory_SucceedsForAdminOrSupervisor()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (student1, assistant1, _, _) = await SeedBasicUsersAsync(db);
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01234567806");
        var adminRole = new Role { Id = Guid.NewGuid(), Name = "Admin", Type = RoleType.Admin };
        
        db.Roles.Add(adminRole);
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });

        db.CrmStudentStatuses.Add(new CrmStudentStatus
        {
            StudentId = student1.Id,
            Status = CrmStatus.Assigned,
            AssignedAgentId = assistant1.Id,
            Priority = CrmPriority.Medium
        });
        await db.SaveChangesAsync();

        var handler = new GetCrmStudentHistoryQueryHandler(db);

        // Act - query history as admin
        var query = new GetCrmStudentHistoryQuery(student1.Id, admin.Id);
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }
}

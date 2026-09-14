using System.Collections;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public sealed class PublicTeachersControllerTests
{
    [Fact]
    public async Task List_ReturnsAllActiveTeachersForAuthenticatedStudentRegardlessOfAcademicScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db, "Scope Independent Subject");
        var matchingTeacher = await SeedTeacherAsync(db, "Matching Teacher", subject);
        var otherStageTeacher = await SeedTeacherAsync(db, "Other Stage Teacher", subject);

        db.StudentFacingAcademicScopes.AddRange(
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Teacher,
                OwnerId = matchingTeacher.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Teacher,
                OwnerId = otherStageTeacher.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.SecondSecondary
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db, student.Id);

        var result = await controller.List(ct: CancellationToken.None);

        var names = ExtractDataItems(result)
            .Select(item => GetPropertyValue<string>(item, "fullName"))
            .OrderBy(name => name)
            .ToList();

        Assert.Equal(["Matching Teacher", "Other Stage Teacher"], names);
    }

    [Fact]
    public async Task Detail_ReturnsTeacherForAuthenticatedStudentRegardlessOfAcademicScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db, "Detail Subject");
        var teacher = await SeedTeacherAsync(db, "Out Of Scope Teacher", subject);
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Teacher,
            OwnerId = teacher.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, student.Id);

        var result = await controller.Detail(teacher.Id.ToString(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetPropertyValue<object>(ok.Value!, "data");
        Assert.Equal("Out Of Scope Teacher", GetPropertyValue<string>(data, "fullName"));
    }

    private static PublicTeachersController CreateController(DbContext db, Guid userId)
    {
        var controller = new PublicTeachersController(
            (NaderGorge.Infrastructure.Data.AppDbContext)db,
            new AcademicScopeService((NaderGorge.Infrastructure.Data.AppDbContext)db));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"))
            }
        };

        return controller;
    }

    private static IEnumerable<object> ExtractDataItems(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var data = GetPropertyValue<object>(ok.Value!, "data");
        return ((IEnumerable)data).Cast<object>();
    }

    private static T GetPropertyValue<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        Assert.NotNull(property);
        var value = property!.GetValue(source);
        if (typeof(T) == typeof(object))
            return (T)value!;

        return Assert.IsType<T>(value);
    }

    private static async Task<User> SeedStudentAsync(DbContext db, EducationStage stage, GradeLevel grade)
    {
        var user = new User
        {
            FullName = $"Student {Guid.NewGuid():N}",
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hash",
            IsActive = true
        };
        db.Add(user);
        db.Add(new StudentProfile
        {
            User = user,
            DateOfBirth = DateTime.UtcNow.AddYears(-16),
            Gender = Gender.Male,
            Governorate = "Cairo",
            Address = "Address",
            EducationStage = stage,
            GradeLevel = grade
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Subject> SeedSubjectAsync(DbContext db, string name)
    {
        var subject = new Subject
        {
            Name = name,
            NormalizedName = $"{name.ToUpperInvariant().Replace(' ', '_')}_{Guid.NewGuid():N}",
            Description = name
        };
        db.Add(subject);
        await db.SaveChangesAsync();
        return subject;
    }

    private static async Task<TeacherProfile> SeedTeacherAsync(DbContext db, string fullName, Subject subject)
    {
        var user = new User
        {
            FullName = fullName,
            PhoneNumber = Guid.NewGuid().ToString("N")[..11],
            PasswordHash = "hash",
            IsActive = true
        };
        var teacher = new TeacherProfile
        {
            User = user,
            Bio = $"{fullName} bio",
            PublicBio = $"{fullName} public bio",
            Specialization = "Secondary",
            ContactInfo = $"{Guid.NewGuid():N}@example.test"
        };
        db.Add(user);
        db.Add(teacher);
        db.Add(new TeacherSubject
        {
            Teacher = teacher,
            Subject = subject
        });
        await db.SaveChangesAsync();
        return teacher;
    }
}

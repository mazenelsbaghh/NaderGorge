using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Parent.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Student;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NaderGorge.Application.Tests.Parent;

public class GetDetailsTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IMediator _mediator;

    public GetDetailsTests()
    {
        _db = TestAppDbContextFactory.Create();

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParentReports:SigningSecret"] = "FakeSigningSecretKeyForTestingReportsOnly!"
            })
            .Build();

        _mediator = new FakeMediator(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private ParentController CreateController(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var controllerContext = new ControllerContext { HttpContext = httpContext };

        return new ParentController(_mediator, _config)
        {
            ControllerContext = controllerContext
        };
    }

    [Fact]
    public async Task GetStudentDetails_MissingStudentIdClaim_ShouldReturnUnauthorized()
    {
        // Arrange
        // Principal without StudentId claim
        var claims = new List<Claim> { new Claim(ClaimTypes.Role, "Parent") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var controller = CreateController(principal);

        // Act
        var result = await controller.GetStudentDetails(CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Equal("غير مصرح بالوصول لبيانات الطالب", apiResponse.Message);
    }

    [Fact]
    public async Task GetStudentDetails_ValidParentRoleAndClaim_ShouldReturnDetails()
    {
        // Arrange
        // 1. Seed user & student profile
        var user = new User { FullName = "أحمد محمد", PhoneNumber = "01000000001", PasswordHash = "hash" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var profile = new StudentProfile
        {
            UserId = user.Id,
            GradeLevel = GradeLevel.SecondSecondary,
            SchoolName = "مدرسة الأورمان الثانوية",
            AvatarSlug = "avatar-lion",
            ParentTrackingCode = "789123"
        };
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        // 2. Seed package, term, content section, and lessons
        var subject = new Subject { Name = "Chemistry", Description = "Chem", NormalizedName = "CHEMISTRY" };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Chemistry Month 1",
            Description = "Chem 1",
            Price = 100,
            SubjectId = subject.Id,
            TargetGrade = "SecondSecondary"
        };
        _db.Packages.Add(package);
        await _db.SaveChangesAsync();

        var term = new Term { Title = "Term 1", PackageId = package.Id };
        _db.Terms.Add(term);
        await _db.SaveChangesAsync();

        var section = new ContentSection { Title = "Section 1", TermId = term.Id };
        _db.ContentSections.Add(section);
        await _db.SaveChangesAsync();

        var lesson1 = new Lesson { Title = "Lesson 1", ContentSectionId = section.Id, Order = 1 };
        var lesson2 = new Lesson { Title = "Lesson 2", ContentSectionId = section.Id, Order = 2 };
        _db.Lessons.AddRange(lesson1, lesson2);
        await _db.SaveChangesAsync();

        // 3. Grant access to package
        _db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = user.Id,
            PackageId = package.Id,
            GrantType = CodeType.Package,
            IsActive = true,
            GrantedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // 4. Mark lesson 1 as completed
        _db.LessonProgresses.Add(new LessonProgress
        {
            UserId = user.Id,
            LessonId = lesson1.Id,
            IsCompleted = true
        });
        await _db.SaveChangesAsync();

        // 5. Seed exam and pass attempt
        var exam = new Exam { Title = "اختبار الكيمياء العضوية الشامل", TotalScore = 50, PassingScore = 25 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        _db.StudentExamAttempts.Add(new StudentExamAttempt
        {
            UserId = user.Id,
            ExamId = exam.Id,
            ScoreAchieved = 45,
            IsPassed = true,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        });
        await _db.SaveChangesAsync();

        // 6. Seed homework and submission
        var homework = new Homework { Title = "واجب المحاضرة الخامسة كيمياء", LessonId = lesson1.Id, TotalScore = 10 };
        _db.Homeworks.Add(homework);
        await _db.SaveChangesAsync();

        _db.HomeworkSubmissions.Add(new HomeworkSubmission
        {
            HomeworkId = homework.Id,
            StudentId = user.Id,
            Status = SubmissionStatus.Graded,
            SubmittedAt = DateTime.UtcNow.AddDays(-2),
            OverallScore = 9,
            Evaluation = "A"
        });
        await _db.SaveChangesAsync();

        // 7. Seed warning event
        _db.WarningEvents.Add(new WarningEvent
        {
            StudentId = user.Id,
            TriggerReason = "عدم حضور المحاضرة المباشرة وتخطي الوقت المحدد للمشاهدة",
            Severity = WarningSeverity.Critical
        });
        await _db.SaveChangesAsync();

        // 8. Create controller with claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Parent"),
            new Claim("StudentId", profile.Id.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var controller = CreateController(principal);

        // Act
        var result = await controller.GetStudentDetails(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<StudentAcademicDetailsDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.NotNull(apiResponse.Data);

        var details = apiResponse.Data;
        Assert.Equal("أحمد محمد", details.StudentName);
        Assert.Equal("ثانية ثانوي", details.Grade);
        Assert.Equal("مدرسة الأورمان الثانوية", details.School);
        Assert.Equal("avatar-lion", details.AvatarSlug);

        // Attendance stats
        Assert.Equal(2, details.Attendance.TotalLessons);
        Assert.Equal(1, details.Attendance.WatchedLessons);
        Assert.Equal(50.0, details.Attendance.CompletionRate);

        // Exams
        Assert.Single(details.Exams);
        Assert.Equal("اختبار الكيمياء العضوية الشامل", details.Exams[0].ExamTitle);
        Assert.Equal(45m, details.Exams[0].Score);
        Assert.Equal(50m, details.Exams[0].TotalScore);
        Assert.Equal(90.0, details.Exams[0].Percentage);
        Assert.Equal("Passed", details.Exams[0].Status);

        // Homeworks
        Assert.Single(details.Homeworks);
        Assert.Equal("واجب المحاضرة الخامسة كيمياء", details.Homeworks[0].Title);
        Assert.True(details.Homeworks[0].IsSubmitted);
        Assert.Equal("Graded", details.Homeworks[0].SubmissionState);
        Assert.Equal("A", details.Homeworks[0].Grade);

        // Warnings
        Assert.Single(details.Warnings);
        Assert.Equal("عدم حضور المحاضرة المباشرة وتخطي الوقت المحدد للمشاهدة", details.Warnings[0].Reason);
        Assert.Equal("Critical", details.Warnings[0].Severity);
    }

    private class FakeMediator : IMediator
    {
        private readonly AppDbContext _db;

        public FakeMediator(AppDbContext db)
        {
            _db = db;
        }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetStudentAcademicDetailsQuery query)
            {
                var handler = new GetStudentAcademicDetailsQueryHandler(_db);
                var result = await handler.Handle(query, cancellationToken);
                return (TResponse)(object)result;
            }
            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
        public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
    }
}

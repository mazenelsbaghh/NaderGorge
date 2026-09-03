using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Parent.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Student;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
    public void CreateParentReportLink_UsesConfiguredHoursAndSafeExpirationPayload()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ParentReports:SigningSecret"] = "FakeSigningSecretKeyForTestingReportsOnly!",
                ["ParentReports:PublicLinkExpirationHours"] = "6"
            })
            .Build();
        var controller = new ParentController(_mediator, config)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<OkObjectResult>(controller.CreateParentReportLink(Guid.NewGuid()));
        var response = Assert.IsType<ApiResponse<object>>(result.Value);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Data);

        Assert.Contains("\"expiresInHours\":6", json);
        Assert.Contains("\"expiresAt\"", json);
    }

    [Fact]
    public async Task GetSummaryReport_ExpiredSignedToken_ReturnsUnauthorized()
    {
        var studentId = Guid.NewGuid();
        var controller = CreateController(new ClaimsPrincipal(new ClaimsIdentity()));
        var expiredToken = CreateSignedParentReportToken(studentId, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await controller.GetSummaryReport(studentId, expiredToken, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private static string CreateSignedParentReportToken(Guid studentId, DateTimeOffset expiresAt)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            StudentId = studentId,
            Purpose = "parent-report",
            Exp = expiresAt.ToUnixTimeSeconds()
        });
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("FakeSigningSecretKeyForTestingReportsOnly!"));
        var signaturePart = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart)));
        return $"{payloadPart}.{signaturePart}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
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

        var teacherUser = new User { FullName = "أ. كيمياء", PhoneNumber = "01000000999", PasswordHash = "hash" };
        _db.Users.Add(teacherUser);
        await _db.SaveChangesAsync();

        var teacher = new TeacherProfile
        {
            UserId = teacherUser.Id,
            Specialization = "كيمياء"
        };
        _db.TeacherProfiles.Add(teacher);
        await _db.SaveChangesAsync();

        var package = new Package
        {
            Id = Guid.NewGuid(),
            Name = "Chemistry Month 1",
            Description = "Chem 1",
            Price = 100,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "SecondSecondary"
        };
        _db.Packages.Add(package);
        _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
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
        var exam = new Exam
        {
            Title = "اختبار الكيمياء العضوية الشامل",
            TotalScore = 50,
            PassingScore = 25,
            CreatedByTeacherId = teacher.Id
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        lesson1.ExamId = exam.Id;
        _db.Lessons.Update(lesson1);
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

    [Fact]
    public async Task GetStudentDetails_ShouldUsePurchasedLessonTeacherForWatchExamsHomeworkAndBalance()
    {
        var student = new User { FullName = "طالب متابعة", PhoneNumber = "01000000002", PasswordHash = "hash" };
        _db.Users.Add(student);
        await _db.SaveChangesAsync();

        var profile = new StudentProfile
        {
            UserId = student.Id,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary,
            SchoolName = "مدرسة الاختبار",
            ParentTrackingCode = "111222"
        };
        _db.StudentProfiles.Add(profile);

        var teacherAUser = new User { FullName = "أ. صاحب الحصة", PhoneNumber = "01000000101", PasswordHash = "hash" };
        var teacherBUser = new User { FullName = "أ. منشئ الامتحان", PhoneNumber = "01000000102", PasswordHash = "hash" };
        _db.Users.AddRange(teacherAUser, teacherBUser);
        await _db.SaveChangesAsync();

        var teacherA = new TeacherProfile { UserId = teacherAUser.Id, Specialization = "فيزياء" };
        var teacherB = new TeacherProfile { UserId = teacherBUser.Id, Specialization = "كيمياء" };
        _db.TeacherProfiles.AddRange(teacherA, teacherB);
        await _db.SaveChangesAsync();

        var subject = new Subject { Name = "Science", Description = "Science", NormalizedName = $"SCIENCE_{Guid.NewGuid():N}" };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync();

        var packageA = new Package
        {
            Name = "Teacher A Package",
            Description = "A",
            Price = 100,
            SubjectId = subject.Id,
            TeacherId = teacherA.Id,
            TargetGrade = "SecondSecondary"
        };
        var packageB = new Package
        {
            Name = "Teacher B Package",
            Description = "B",
            Price = 100,
            SubjectId = subject.Id,
            TeacherId = teacherB.Id,
            TargetGrade = "SecondSecondary"
        };
        _db.Packages.AddRange(packageA, packageB);
        await _db.SaveChangesAsync();
        _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = packageA.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await _db.SaveChangesAsync();

        var termA = new Term { Title = "Term A", PackageId = packageA.Id };
        var termB = new Term { Title = "Term B", PackageId = packageB.Id };
        _db.Terms.AddRange(termA, termB);
        await _db.SaveChangesAsync();

        var sectionA = new ContentSection { Title = "Section A", TermId = termA.Id };
        var sectionB = new ContentSection { Title = "Section B", TermId = termB.Id };
        _db.ContentSections.AddRange(sectionA, sectionB);
        await _db.SaveChangesAsync();

        var lessonA = new Lesson { Title = "Purchased Lesson", ContentSectionId = sectionA.Id, Order = 1 };
        var lessonB = new Lesson { Title = "Hidden Lesson", ContentSectionId = sectionB.Id, Order = 1 };
        var academicallyHiddenLesson = new Lesson
        {
            Title = "Academically hidden purchased lesson",
            ContentSectionId = sectionA.Id,
            Order = 2
        };
        var archivedLesson = new Lesson
        {
            Title = "Archived purchased lesson",
            ContentSectionId = sectionA.Id,
            Order = 3,
            ArchiveMode = ContentArchiveMode.HiddenFromEveryone
        };
        _db.Lessons.AddRange(lessonA, lessonB, academicallyHiddenLesson, archivedLesson);
        await _db.SaveChangesAsync();

        var videoA = new LessonVideo
        {
            Title = "Purchased Video",
            LessonId = lessonA.Id,
            Provider = "youtube",
            ProviderVideoId = "video-a",
            IsActive = true
        };
        var visibleUnwatchedVideo = new LessonVideo
        {
            Title = "Visible unwatched video",
            LessonId = lessonA.Id,
            Provider = "youtube",
            ProviderVideoId = "visible-unwatched",
            IsActive = true
        };
        var academicallyHiddenVideo = new LessonVideo
        {
            Title = "Academically hidden video",
            LessonId = lessonA.Id,
            Provider = "youtube",
            ProviderVideoId = "academic-hidden",
            IsActive = true
        };
        var archivedVideo = new LessonVideo
        {
            Title = "Archived video",
            LessonId = lessonA.Id,
            Provider = "youtube",
            ProviderVideoId = "archive-hidden",
            IsActive = true,
            ArchiveMode = ContentArchiveMode.HiddenFromEveryone
        };
        var inactiveVideo = new LessonVideo
        {
            Title = "Inactive video",
            LessonId = lessonA.Id,
            Provider = "youtube",
            ProviderVideoId = "inactive",
            IsActive = false
        };
        var academicallyHiddenLessonVideo = new LessonVideo
        {
            Title = "Academically hidden lesson video",
            LessonId = academicallyHiddenLesson.Id,
            Provider = "youtube",
            ProviderVideoId = "academic-hidden-lesson",
            IsActive = true
        };
        var archivedLessonVideo = new LessonVideo
        {
            Title = "Archived lesson video",
            LessonId = archivedLesson.Id,
            Provider = "youtube",
            ProviderVideoId = "archived-lesson",
            IsActive = true
        };
        _db.LessonVideos.AddRange(
            videoA,
            visibleUnwatchedVideo,
            academicallyHiddenVideo,
            archivedVideo,
            inactiveVideo,
            academicallyHiddenLessonVideo,
            archivedLessonVideo);
        _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.LessonVideo,
            OwnerId = academicallyHiddenVideo.Id,
            ScopeLevel = AcademicScopeLevel.StageWide,
            EducationStage = EducationStage.Primary
        });
        _db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Lesson,
            OwnerId = academicallyHiddenLesson.Id,
            ScopeLevel = AcademicScopeLevel.StageWide,
            EducationStage = EducationStage.Primary
        });
        await _db.SaveChangesAsync();

        _db.StudentAccessGrants.AddRange(
            new StudentAccessGrant
            {
                UserId = student.Id,
                LessonVideoId = videoA.Id,
                GrantType = CodeType.Video,
                IsActive = true,
                GrantedAt = DateTime.UtcNow
            },
            new StudentAccessGrant
            {
                UserId = student.Id,
                LessonVideoId = academicallyHiddenLessonVideo.Id,
                GrantType = CodeType.Video,
                IsActive = true,
                GrantedAt = DateTime.UtcNow
            },
            new StudentAccessGrant
            {
                UserId = student.Id,
                LessonVideoId = archivedLessonVideo.Id,
                GrantType = CodeType.Video,
                IsActive = true,
                GrantedAt = DateTime.UtcNow
            },
            new StudentAccessGrant
            {
                UserId = student.Id,
                PackageId = packageB.Id,
                GrantType = CodeType.Package,
                IsActive = false,
                GrantedAt = DateTime.UtcNow
            });
        await _db.SaveChangesAsync();

        _db.VideoWatchEvents.Add(new VideoWatchEvent
        {
            UserId = student.Id,
            LessonVideoId = videoA.Id,
            TimeWatchedInSeconds = 180,
            WatchCount = 2
        });
        _db.VideoWatchEvents.AddRange(
            new VideoWatchEvent
            {
                UserId = student.Id,
                LessonVideoId = visibleUnwatchedVideo.Id,
                TimeWatchedInSeconds = 90,
                WatchCount = 0
            },
            new VideoWatchEvent
            {
                UserId = student.Id,
                LessonVideoId = academicallyHiddenVideo.Id,
                TimeWatchedInSeconds = 900,
                WatchCount = 9
            },
            new VideoWatchEvent
            {
                UserId = student.Id,
                LessonVideoId = archivedVideo.Id,
                TimeWatchedInSeconds = 800,
                WatchCount = 8
            },
            new VideoWatchEvent
            {
                UserId = student.Id,
                LessonVideoId = inactiveVideo.Id,
                TimeWatchedInSeconds = 700,
                WatchCount = 7
            });
        _db.LessonProgresses.Add(new LessonProgress
        {
            UserId = student.Id,
            LessonId = lessonA.Id,
            IsCompleted = true
        });

        var exam = new Exam
        {
            Title = "Exam Created By Other Teacher",
            TotalScore = 20,
            PassingScore = 10,
            CreatedByTeacherId = teacherB.Id
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        lessonA.ExamId = exam.Id;
        _db.Lessons.Update(lessonA);

        var purchasedLessonHomework = new Homework
        {
            LessonId = lessonA.Id,
            Title = "Purchased Lesson Homework",
            TotalScore = 10
        };
        _db.Homeworks.Add(purchasedLessonHomework);
        _db.HomeworkQuestions.Add(new HomeworkQuestion
        {
            HomeworkId = purchasedLessonHomework.Id,
            BodyText = "Published homework question",
            QuestionType = NaderGorge.Domain.Entities.Homework.QuestionType.Essay,
            PointsActive = 10
        });

        var balance = new StudentBalance
        {
            UserId = student.Id,
            CurrentBalance = 75m
        };
        _db.StudentBalances.Add(balance);
        await _db.SaveChangesAsync();

        _db.BalanceTransactions.AddRange(
            new BalanceTransaction
            {
                StudentBalanceId = balance.Id,
                Amount = 100m,
                BalanceAfter = 100m,
                TransactionType = "CodeRedemption",
                Description = "شحن رصيد",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new BalanceTransaction
            {
                StudentBalanceId = balance.Id,
                Amount = -25m,
                BalanceAfter = 75m,
                TransactionType = "ContentPurchase",
                Description = "شراء حصة",
                CreatedAt = DateTime.UtcNow
            });
        await _db.SaveChangesAsync();

        var handler = new GetStudentAcademicDetailsQueryHandler(_db, new AcademicScopeService(_db));

        var result = await handler.Handle(new GetStudentAcademicDetailsQuery(profile.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var details = result.Data;
        Assert.Equal(1, details.Attendance.TotalLessons);
        Assert.Equal(1, details.Attendance.WatchedLessons);
        Assert.Equal(100, details.Attendance.CompletionRate);
        var teacher = Assert.Single(details.Teachers);
        Assert.Equal(teacherA.Id, teacher.TeacherId);

        var watchLesson = Assert.Single(details.WatchLessons);
        Assert.Equal(teacherA.Id, watchLesson.TeacherId);
        Assert.Equal(lessonA.Id, watchLesson.LessonId);
        Assert.Equal(2, watchLesson.TotalVideos);
        Assert.Equal(1, watchLesson.WatchedVideos);
        Assert.Equal(2, watchLesson.WatchCount);
        Assert.Equal(180, watchLesson.WatchedSeconds);
        Assert.True(watchLesson.IsCompleted);

        var visibleExam = Assert.Single(details.Exams);
        Assert.Equal(exam.Id, visibleExam.ExamId);
        Assert.Null(visibleExam.AttemptId);
        Assert.Equal(teacherA.Id, visibleExam.TeacherId);
        Assert.Equal("NotStarted", visibleExam.Status);

        var visibleHomework = Assert.Single(details.Homeworks);
        Assert.Equal(teacherA.Id, visibleHomework.TeacherId);
        Assert.False(visibleHomework.IsSubmitted);
        Assert.Equal("NotSubmitted", visibleHomework.SubmissionState);

        Assert.Equal(75m, details.Balance.CurrentBalance);
        Assert.Equal(2, details.Balance.Transactions.Count);
        Assert.Equal("ContentPurchase", details.Balance.Transactions[0].TransactionType);
        Assert.Equal(75m, details.Balance.Transactions[0].BalanceAfter);
    }

    [Fact]
    public async Task GetStudentDetails_ShouldReturnEmptyCollectionsForStudentWithoutPurchases()
    {
        var student = new User { FullName = "طالب بدون مشتريات", PhoneNumber = "01000000003", PasswordHash = "hash" };
        _db.Users.Add(student);
        await _db.SaveChangesAsync();

        var profile = new StudentProfile
        {
            UserId = student.Id,
            GradeLevel = GradeLevel.SecondSecondary,
            SchoolName = "مدرسة الاختبار",
            ParentTrackingCode = "333444"
        };
        _db.StudentProfiles.Add(profile);
        await _db.SaveChangesAsync();

        var handler = new GetStudentAcademicDetailsQueryHandler(_db, new AcademicScopeService(_db));

        var result = await handler.Handle(new GetStudentAcademicDetailsQuery(profile.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Teachers);
        Assert.Empty(result.Data.WatchLessons);
        Assert.Empty(result.Data.Exams);
        Assert.Empty(result.Data.Homeworks);
        Assert.Equal(0m, result.Data.Balance.CurrentBalance);
        Assert.Empty(result.Data.Balance.Transactions);
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
                var handler = new GetStudentAcademicDetailsQueryHandler(_db, new AcademicScopeService(_db));
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

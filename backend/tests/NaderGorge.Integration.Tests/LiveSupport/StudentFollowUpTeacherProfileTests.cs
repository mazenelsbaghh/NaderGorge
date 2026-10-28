using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Assessments;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed partial class StudentFollowUpPostgresTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TeacherProfileKeepsOnlyTheirCourseHistoryAndDoesNotExposeGlobalAccountData(bool includeFinance)
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        var foreign = await Seed(fixture.Db);
        foreign.Grant.UserId = own.Student.Id;
        own.Package.Price = 150;
        var profile = await fixture.Db.StudentProfiles.SingleAsync(student => student.UserId == own.Student.Id);
        var privateParentCode = includeFinance ? "123451" : "123450";
        profile.ParentTrackingCode = privateParentCode;
        var ownExam = new Exam { Title = "امتحان المدرس", TotalScore = 10, CreatedByTeacher = own.Teacher };
        var foreignExam = new Exam { Title = "امتحان آخر", TotalScore = 10, CreatedByTeacher = foreign.Teacher };
        own.Lesson.ExamId = ownExam.Id;
        foreign.Lesson.ExamId = foreignExam.Id;
        var ownHomework = new Homework { Title = "واجب المدرس", LessonId = own.Lesson.Id, TotalScore = 10 };
        var foreignHomework = new Homework { Title = "واجب آخر", LessonId = foreign.Lesson.Id, TotalScore = 10 };
        PromotionalBalanceAllocation Credit(Guid? teacherId, decimal amount) => new()
        {
            StudentId = own.Student.Id, TeacherId = teacherId, OriginalAmount = amount, AvailableAmount = amount,
            GiftRecipient = new GiftRecipient { StudentId = own.Student.Id, GiftIssuance = new GiftIssuance
            { RequestId = Guid.NewGuid(), IssuedByUserId = own.Teacher.UserId, TeacherId = teacherId,
                TargetType = teacherId.HasValue ? GiftTargetType.TeacherBalance : GiftTargetType.GeneralBalance } }
        };
        fixture.Db.AddRange(
            new VideoWatchEvent { UserId = own.Student.Id, LessonVideoId = own.Video.Id, LearningWatchedSeconds = 50, LearningDurationSeconds = 100 },
            new VideoWatchEvent { UserId = own.Student.Id, LessonVideoId = foreign.Video.Id, LearningWatchedSeconds = 100, LearningDurationSeconds = 100 },
            new StudentExamAttempt { UserId = own.Student.Id, Exam = ownExam, ScoreAchieved = 8, Evaluation = "جيد", DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(ownExam).ToJson() },
            new StudentExamAttempt { UserId = own.Student.Id, Exam = foreignExam, DefinitionSnapshotJson = AssessmentDefinitionSnapshot.FromExam(foreignExam).ToJson() },
            new HomeworkSubmission { StudentId = own.Student.Id, Homework = ownHomework, Status = SubmissionStatus.Graded, OverallScore = 7 },
            new HomeworkSubmission { StudentId = own.Student.Id, Homework = foreignHomework, Status = SubmissionStatus.InProgress },
            new StudentNote { StudentId = own.Student.Id, AdminId = own.Teacher.UserId, Content = "ملاحظة المدرس" },
            new StudentNote { StudentId = own.Student.Id, AdminId = foreign.Teacher.UserId, Content = "ملاحظة خاصة بمدرس آخر" },
            new VideoOverride { UserId = own.Student.Id, LessonVideoId = own.Video.Id, PerformedByUserId = own.Teacher.UserId, NewLimit = 5 },
            new VideoOverride { UserId = own.Student.Id, LessonVideoId = foreign.Video.Id, PerformedByUserId = foreign.Teacher.UserId, NewLimit = 9 },
            new Device { UserId = own.Student.Id, DeviceFingerprint = Guid.NewGuid().ToString(), DeviceName = "جهاز خاص" },
            new StudentBalance { UserId = own.Student.Id, CurrentBalance = 999 },
            Credit(own.Teacher.Id, 10), Credit(foreign.Teacher.Id, 20), Credit(null, 30));
        await fixture.Db.SaveChangesAsync();
        var reader = new GetStudentProfileDetailQueryHandler(fixture.Db);

        var scoped = await reader.Handle(new(own.Student.Id) { TeacherId = own.Teacher.Id, IncludeTeacherFinance = includeFinance }, default);

        Assert.Equal(own.Package.Id, Assert.Single(scoped.Packages).Id);
        Assert.Equal(includeFinance ? 150 : 0, scoped.Packages[0].Price);
        Assert.Equal(own.Video.Id, Assert.Single(scoped.WatchTracking.Activities).LessonVideoId);
        Assert.Equal(ownExam.Id, Assert.Single(scoped.ExamHistory).ExamId);
        Assert.Equal(ownHomework.Id, Assert.Single(scoped.HomeworkHistory).HomeworkId);
        Assert.Equal("ملاحظة المدرس", Assert.Single(scoped.Notes).Content);
        Assert.Equal(own.Video.Id, Assert.Single(scoped.Overrides).VideoId);
        Assert.Empty(scoped.Devices);
        Assert.Null(scoped.ParentTrackingCode);
        Assert.Equal(0, scoped.CurrentBalance);
        Assert.Empty(scoped.BalanceTransactions);
        if (includeFinance) Assert.Equal(10, Assert.Single(scoped.PromotionalBalances).AvailableAmount);
        else Assert.Empty(scoped.PromotionalBalances);

        var admin = await reader.Handle(new(own.Student.Id), default);
        Assert.Equal(2, admin.Packages.Count);
        Assert.Equal(2, admin.WatchTracking.Activities.Count);
        Assert.Equal(2, admin.ExamHistory.Count);
        Assert.Equal(2, admin.HomeworkHistory.Count);
        Assert.Equal(2, admin.Notes.Count);
        Assert.Single(admin.Devices);
        Assert.Equal(privateParentCode, admin.ParentTrackingCode);
        Assert.Equal(999, admin.CurrentBalance);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => reader.Handle(new(foreign.Student.Id) { TeacherId = own.Teacher.Id }, default));
    }

    [Fact]
    public async Task TeacherStudentProfileEndpointRequiresStudentPermissionAndReturnsNotFoundOutsideTheirScope()
    {
        await using var fixture = new PostgresLiveSupportFixture();
        await fixture.ResetAsync();
        var own = await Seed(fixture.Db);
        var foreign = await Seed(fixture.Db);
        using var services = new ServiceCollection().AddSingleton<IAppDbContext>(fixture.Db)
            .AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<GetStudentProfileDetailQuery>())
            .BuildServiceProvider();
        TeacherController Endpoint(Guid userId) => new(services.GetRequiredService<IMediator>(), fixture.Db)
        { ControllerContext = Controller(fixture.Db, userId).ControllerContext };
        Assert.IsType<NotFoundObjectResult>(await Endpoint(own.Teacher.UserId).StudentProfile(foreign.Student.Id, default));
        var staff = User("مساعد ملف الطلاب");
        var membership = new TeacherStaffMember { User = staff, TeacherId = own.Teacher.Id, IsActive = true,
            CreatedByTeacherUserId = own.Teacher.UserId, PermissionKeys = "reports" };
        fixture.Db.Add(membership);
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<ForbidResult>(await Endpoint(staff.Id).StudentProfile(own.Student.Id, default));
        membership.PermissionKeys = "students";
        await fixture.Db.SaveChangesAsync();
        var response = Assert.IsType<OkObjectResult>(await Endpoint(staff.Id).StudentProfile(own.Student.Id, default));
        var envelope = Assert.IsType<ApiResponse<TeacherController.TeacherStudentProfileResponse>>(response.Value);
        Assert.Equal(own.Student.Id, envelope.Data!.Profile.Id);
        Assert.False(envelope.Data.CanViewFinance);
        Assert.False(envelope.Data.CanViewReports);
    }
}

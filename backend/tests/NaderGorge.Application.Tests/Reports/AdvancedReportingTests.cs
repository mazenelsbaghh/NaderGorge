using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Reporting;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Application.Tests.Reports;

public sealed class AdvancedReportingTests
{
    [Fact]
    public void Catalog_ExposesAttendanceDomain_AndHidesPrivilegedTeacherDomains()
    {
        var admin = ReportCatalog.Get(false);
        var teacher = ReportCatalog.Get(true);

        Assert.Equal(15, admin.Domains.Count);
        Assert.Contains(admin.Domains, domain => domain.Key == ReportDomains.StudentJourney);
        Assert.Contains(admin.Domains, domain => domain.Key == ReportDomains.Engagement);
        Assert.Contains(admin.Domains, domain => domain.Key == ReportDomains.Attendance);
        Assert.DoesNotContain(teacher.Domains, domain => domain.Key is ReportDomains.Support or ReportDomains.Staff or ReportDomains.OperationsSecurity);
        Assert.All(teacher.Domains, domain => Assert.DoesNotContain(domain.Fields, field => field.Key.Contains("support", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AdminReportsController_RequiresReportsManagePermission()
    {
        var permission = Assert.Single(typeof(AdminReportsController)
            .GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true));

        Assert.IsType<HasPermissionAttribute>(permission);
    }

    [Fact]
    public async Task Execute_AppliesNestedAndOrFilters()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedStudentAsync(db, "Ahmed Ali", "01000000001", true, GradeLevel.SecondaryGrade3);
        await SeedStudentAsync(db, "Mona Ali", "01000000002", false, GradeLevel.SecondaryGrade3);
        await SeedStudentAsync(db, "Sara Hassan", "01000000003", true, GradeLevel.SecondSecondary);
        var service = new ReportQueryService(db);
        var request = new ExecuteReportRequest(
            ReportDomains.Students,
            new ReportFilterGroup("and", [Filter("isActive", "eq", true)],
            [new ReportFilterGroup("or", [Filter("studentName", "contains", "Ahmed"), Filter("studentName", "contains", "Mona")])]));

        var result = await service.ExecuteAsync(request, Guid.NewGuid(), false, default);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Ahmed Ali", row["studentName"]);
    }

    [Fact]
    public async Task TeacherScope_CannotReturnAnotherTeachersStudent()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacherOne = await SeedTeacherAsync(db, "Teacher One", "01110000001");
        var teacherTwo = await SeedTeacherAsync(db, "Teacher Two", "01110000002");
        var studentOne = await SeedStudentAsync(db, "Student One", "01220000001", true, GradeLevel.SecondaryGrade3);
        var studentTwo = await SeedStudentAsync(db, "Student Two", "01220000002", true, GradeLevel.SecondaryGrade3);
        await SeedPackageGrantAsync(db, teacherOne.Profile.Id, studentOne.Id, "First package");
        await SeedPackageGrantAsync(db, teacherTwo.Profile.Id, studentTwo.Id, "Second package");
        var service = new ReportQueryService(db);

        var result = await service.ExecuteAsync(new ExecuteReportRequest(ReportDomains.Students), teacherOne.User.Id, true, default);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Student One", row["studentName"]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(new ExecuteReportRequest(ReportDomains.Support), teacherOne.User.Id, true, default));
    }

    [Fact]
    public async Task Purchases_ReturnsAcademicallyEligibleActiveStudentAsNotPurchased()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Purchase Teacher", "01140000001");
        var student = await SeedStudentAsync(db, "Eligible Student", "01240000001", true, GradeLevel.SecondaryGrade3);
        var package = await SeedScopedPackageAsync(db, teacher.Profile.Id, "Eligible Package");
        var service = new ReportQueryService(db);
        var request = new ExecuteReportRequest(ReportDomains.Purchases, new ReportFilterGroup("and", [Filter("purchaseStatus", "eq", "notPurchased")]));

        var result = await service.ExecuteAsync(request, Guid.NewGuid(), false, default);

        var row = Assert.Single(result.Rows);
        Assert.Equal(student.FullName, row["studentName"]);
        Assert.Equal(package.Name, row["packageName"]);
        Assert.Equal("notPurchased", row["purchaseStatus"]);
    }

    [Fact]
    public async Task StudentJourney_CombinesPurchaseAndAttendanceChoicesInOneReport()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Journey Teacher", "01141000001");
        var student = await SeedStudentAsync(db, "Journey Student", "01241000001", true, GradeLevel.SecondaryGrade3);
        await SeedPackageGrantAsync(db, teacher.Profile.Id, student.Id, "Journey Package");
        var service = new ReportQueryService(db);
        var request = new ExecuteReportRequest(
            ReportDomains.StudentJourney,
            new ReportFilterGroup("and",
            [
                Filter("purchaseStatus", "in", "purchased", "notPurchased"),
                Filter("attendanceStatus", "eq", "absent")
            ]));

        var result = await service.ExecuteAsync(request, teacher.User.Id, true, default);

        var row = Assert.Single(result.Rows);
        Assert.Equal(student.FullName, row["studentName"]);
        Assert.Equal("purchased", row["purchaseStatus"]);
        Assert.Equal("absent", row["attendanceStatus"]);
        Assert.Equal("notWatched", row["videoStatus"]);
        Assert.Equal("noExam", row["examStatus"]);
        Assert.Equal("noHomework", row["homeworkStatus"]);
    }

    [Fact]
    public async Task TeacherNotPurchasedRows_StayInsideTeacherAndExistingStudentScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var firstTeacher = await SeedTeacherAsync(db, "Scoped Teacher", "01150000001");
        var secondTeacher = await SeedTeacherAsync(db, "Other Teacher", "01150000002");
        var student = await SeedStudentAsync(db, "Scoped Student", "01250000001", true, GradeLevel.SecondaryGrade3);
        var ownedPackage = await SeedScopedPackageAsync(db, firstTeacher.Profile.Id, "Owned Purchased Package");
        var ownedMissingPackage = await SeedScopedPackageAsync(db, firstTeacher.Profile.Id, "Owned Missing Package");
        var otherPackage = await SeedScopedPackageAsync(db, secondTeacher.Profile.Id, "Other Missing Package");
        db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = student.Id, PackageId = ownedPackage.Id, GrantType = CodeType.Package });
        await db.SaveChangesAsync();
        var service = new ReportQueryService(db);
        var request = new ExecuteReportRequest(ReportDomains.Purchases, new ReportFilterGroup("and", [Filter("purchaseStatus", "eq", "notPurchased")]));

        var result = await service.ExecuteAsync(request, firstTeacher.User.Id, true, default);

        var row = Assert.Single(result.Rows);
        Assert.Equal(ownedMissingPackage.Name, row["packageName"]);
        Assert.DoesNotContain(result.Rows, candidate => Equals(candidate["packageName"], otherPackage.Name));
    }

    [Fact]
    public void Validator_RejectsUnknownLogicAndExcessiveNesting()
    {
        var validator = new ExecuteReportRequestValidator();
        var invalidLogic = new ExecuteReportRequest(ReportDomains.Students, new ReportFilterGroup("xor"));
        var tooDeep = new ExecuteReportRequest(ReportDomains.Students,
            new ReportFilterGroup("and", Groups: [new("and", Groups: [new("and", Groups: [new("and")])])]));

        Assert.False(validator.Validate(invalidLogic).IsValid);
        Assert.False(validator.Validate(tooDeep).IsValid);
    }

    [Fact]
    public async Task SharedValidation_RejectsWrongValueTypeForExecuteAndExport()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new ReportQueryService(db);
        var request = new ExecuteReportRequest(ReportDomains.Students,
            new ReportFilterGroup("and", [Filter("isActive", "eq", "not-a-boolean")]));

        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(request, Guid.NewGuid(), false, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteForExportAsync(request, Guid.NewGuid(), false, default));
    }

    [Fact]
    public async Task TeacherStaff_RequiresReportsPermission_AndFinanceRequiresBothPermissions()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Permission Teacher", "01160000001");
        var reportsOnlyUser = await TestAppDbContextFactory.SeedUserAsync(db, "Reports Staff", "01260000001");
        var noReportsUser = await TestAppDbContextFactory.SeedUserAsync(db, "No Reports Staff", "01260000002");
        db.TeacherStaffMembers.AddRange(
            new TeacherStaffMember { TeacherId = teacher.Profile.Id, UserId = reportsOnlyUser.Id, CreatedByTeacherUserId = teacher.User.Id, PermissionKeys = "reports" },
            new TeacherStaffMember { TeacherId = teacher.Profile.Id, UserId = noReportsUser.Id, CreatedByTeacherUserId = teacher.User.Id, PermissionKeys = "finance" });
        await db.SaveChangesAsync();
        var service = new ReportQueryService(db);

        Assert.True(await service.CanAccessTeacherReportsAsync(reportsOnlyUser.Id, default));
        Assert.False(await service.CanAccessTeacherFinanceAsync(reportsOnlyUser.Id, default));
        Assert.False(await service.CanAccessTeacherReportsAsync(noReportsUser.Id, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExecuteAsync(new ExecuteReportRequest(ReportDomains.TeachersFinance), reportsOnlyUser.Id, true, default));
    }

    [Fact]
    public async Task SavedDefinitions_AreOwnerBoundAndValidateCurrentScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var owner = await TestAppDbContextFactory.SeedUserAsync(db, "Owner", "01010000001");
        var other = await TestAppDbContextFactory.SeedUserAsync(db, "Other", "01010000002");
        var service = new ReportQueryService(db);
        var handler = new ReportDefinitionRequestHandler(db, service);
        var configuration = new ExecuteReportRequest(ReportDomains.Students);

        var created = await handler.Handle(new CreateReportDefinitionCommand(owner.Id, false, new SaveReportDefinitionRequest("طلاب نشطون", configuration)), default);
        var id = created.Data!.Id;
        var forbiddenRead = await handler.Handle(new GetReportDefinitionQuery(id, other.Id, false), default);
        var forbiddenDelete = await handler.Handle(new DeleteReportDefinitionCommand(id, other.Id), default);

        Assert.True(created.Success);
        Assert.False(forbiddenRead.Success);
        Assert.False(forbiddenDelete.Success);
        Assert.True(await db.ReportDefinitions.AnyAsync(report => report.Id == id));
    }

    [Fact]
    public async Task Export_CreatesRealXlsxAndPdfFiles()
    {
        await using var db = TestAppDbContextFactory.Create();
        await SeedStudentAsync(db, "Export Student", "01030000001", true, GradeLevel.SecondaryGrade3);
        var exporter = new ReportExportService(new ReportQueryService(db), db);
        var request = new ExecuteReportRequest(ReportDomains.Students);

        var xlsx = await exporter.ExportAsync("xlsx", request, Guid.NewGuid(), false, default);
        var pdf = await exporter.ExportAsync("pdf", request, Guid.NewGuid(), false, default);

        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(xlsx.Content, 0, 2));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));
    }

    [Fact]
    public async Task Export_AppendsStudentIdentityColumns_ToOldExplicitColumnSelections()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, "Identity Student", "01035555555", true, GradeLevel.SecondSecondary);
        var profile = await db.StudentProfiles.SingleAsync(item => item.UserId == student.Id);
        profile.EducationStage = EducationStage.Secondary;
        profile.StudyTrack = StudyTrack.Science;
        await db.SaveChangesAsync();
        var service = new ReportQueryService(db);

        var result = await service.ExecuteForExportAsync(
            new ExecuteReportRequest(ReportDomains.Students, Columns: ["studentName"]),
            Guid.NewGuid(),
            false,
            default);

        Assert.Equal(["studentName", "phone", "stage", "grade", "studyTrack"], result.Columns.Select(column => column.Key));
        var row = Assert.Single(result.Rows);
        Assert.Equal("01035555555", row["phone"]);
        Assert.Equal(EducationStage.Secondary.ToString(), row["stage"]);
        Assert.Equal(GradeLevel.SecondSecondary.ToString(), row["grade"]);
        Assert.Equal(StudyTrack.Science.ToString(), row["studyTrack"]);
    }

    [Fact]
    public async Task StudentLedger_ExportsOneSheetWithPurchasedAndMissingPackages()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Ledger Teacher", "01170000001");
        var student = await SeedStudentAsync(db, "Ledger Student", "01270000001", true, GradeLevel.SecondaryGrade3);
        await SeedPackageGrantAsync(db, teacher.Profile.Id, student.Id, "A Purchased");
        await SeedScopedPackageAsync(db, teacher.Profile.Id, "B Missing");
        var export = await new StudentLedgerExportService(db).ExportAsync(teacher.Profile.Id, teacher.User.Id, default);

        using var stream = new MemoryStream(export.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = Assert.Single(workbook.Worksheets);

        Assert.Equal("سجل الطلاب", sheet.Name);
        Assert.Equal("Ledger Student", sheet.Cell(7, 1).GetString());
        Assert.Contains("باقة / كورس: A Purchased", sheet.Row(7).CellsUsed().Select(cell => cell.GetString()));
        Assert.Contains("لم يشترِ", sheet.Row(7).CellsUsed().Select(cell => cell.GetString()));
        Assert.Equal(XLColor.FromHtml("#DCFCE7"), sheet.Cell(7, 10).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#FEE2E2"), sheet.Cell(7, 11).Style.Fill.BackgroundColor);
    }

    [Fact]
    public async Task StudentLedger_StageFilterIncludesParentContactsAndAcademicIdentity()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Filtered Ledger Teacher", "01171000001");
        var included = await SeedStudentAsync(db, "Included Student", "01271000001", true, GradeLevel.SecondSecondary);
        var excluded = await SeedStudentAsync(db, "Excluded Student", "01271000002", true, GradeLevel.PrimaryGrade6);
        var profile = await db.StudentProfiles.SingleAsync(studentProfile => studentProfile.UserId == included.Id);
        profile.EducationStage = EducationStage.Secondary;
        profile.StudyTrack = StudyTrack.Science;
        profile.ParentPhone = "01010000001";
        profile.SecondaryParentPhone = "01010000002";
        profile.MotherPhone = "01010000003";
        await db.SaveChangesAsync();
        await SeedPackageGrantAsync(db, teacher.Profile.Id, included.Id, "Secondary Package");
        await SeedPackageGrantAsync(db, teacher.Profile.Id, excluded.Id, "Primary Package");

        var export = await new StudentLedgerExportService(db).ExportAsync(
            teacher.Profile.Id,
            teacher.User.Id,
            new StudentLedgerFilter(EducationStage.Secondary, StudyTrack.Science),
            default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheet("سجل الطلاب");
        Assert.Equal("هاتف الأب", sheet.Cell(3, 3).GetString());
        Assert.Equal("هاتف ولي الأمر الإضافي", sheet.Cell(3, 4).GetString());
        Assert.Equal("هاتف الأم", sheet.Cell(3, 5).GetString());
        Assert.Equal("Included Student", sheet.Cell(7, 1).GetString());
        Assert.Equal("01010000001", sheet.Cell(7, 3).GetString());
        Assert.Equal("01010000002", sheet.Cell(7, 4).GetString());
        Assert.Equal("01010000003", sheet.Cell(7, 5).GetString());
        Assert.Equal("ثانوي", sheet.Cell(7, 6).GetString());
        Assert.Equal("علمي", sheet.Cell(7, 8).GetString());
        Assert.True(sheet.Cell(8, 1).IsEmpty());
    }

    [Fact]
    public async Task StudentLedger_TeacherWithoutPackages_ExportsBaseHeaders()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Empty Ledger Teacher", "01180000001");

        var export = await new StudentLedgerExportService(db).ExportAsync(teacher.Profile.Id, teacher.User.Id, default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheet("سجل الطلاب");
        Assert.Equal("اسم الطالب", sheet.Cell(3, 1).GetString());
        Assert.Equal(9, sheet.LastColumnUsed()!.ColumnNumber());
    }

    [Fact]
    public async Task StudentLedger_SeparatesVideoMetrics_AndListsEveryPlaybackRate()
    {
        await using var db = TestAppDbContextFactory.Create();
        var teacher = await SeedTeacherAsync(db, "Video Ledger Teacher", "01190000001");
        var student = await SeedStudentAsync(db, "Video Ledger Student", "01290000001", true, GradeLevel.SecondaryGrade3);
        var package = await SeedPackageWithVideoAsync(db, teacher.Profile.Id, student.Id);
        var termGrant = await db.StudentAccessGrants.SingleAsync(grant => grant.UserId == student.Id);
        termGrant.PackageId = null;
        termGrant.TermId = package.Terms.Single().Id;
        termGrant.GrantType = CodeType.Term;
        await db.SaveChangesAsync();
        var export = await new StudentLedgerExportService(db).ExportAsync(teacher.Profile.Id, teacher.User.Id, default);

        using var workbook = new XLWorkbook(new MemoryStream(export.Content));
        var sheet = workbook.Worksheet("سجل الطلاب");
        var headers = sheet.Row(6).CellsUsed().ToDictionary(cell => cell.GetString(), cell => cell.Address.ColumnNumber);

        Assert.Contains("الحصة الأولى - الحضور", headers.Keys);
        Assert.Contains("الفيديو الأول - دقائق المشاهدة", headers.Keys);
        Assert.Contains("الفيديو الأول - عدد المشاهدات", headers.Keys);
        Assert.Contains("الفيديو الأول - آخر مشاهدة", headers.Keys);
        Assert.Equal("ترم: الترم الأول", sheet.Cell(7, 10).GetString());
        Assert.Equal("1×، 1.5×، 2×", sheet.Cell(7, headers["الفيديو الأول - سرعات المشاهدة"]).GetString());
        var speedPackageHeader = sheet.MergedRanges.Single(range => range.Contains(sheet.Cell(3, headers["الفيديو الأول - سرعات المشاهدة"]))).FirstCell();
        Assert.Equal(package.Name, speedPackageHeader.GetString());
        Assert.Equal(XLColor.FromHtml("#BBF7D0"), sheet.Cell(6, headers["الحصة الأولى - الحضور"]).Style.Fill.BackgroundColor);
        Assert.Equal(XLColor.FromHtml("#99F6E4"), sheet.Cell(6, headers["الفيديو الأول - سرعات المشاهدة"]).Style.Fill.BackgroundColor);
    }

    private static ReportFilter Filter(string field, string operation, params object[] values) =>
        new(field, operation, values.Select(value => JsonSerializer.SerializeToElement(value)).ToArray());

    private static async Task<User> SeedStudentAsync(Microsoft.EntityFrameworkCore.DbContext dbContext, string name, string phone, bool active, GradeLevel grade)
    {
        var db = (NaderGorge.Infrastructure.Data.AppDbContext)dbContext;
        var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "hash", IsActive = active };
        db.Users.Add(user);
        db.StudentProfiles.Add(new StudentProfile { UserId = user.Id, User = user, GradeLevel = grade, Governorate = "Cairo", Address = "Address" });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<(User User, TeacherProfile Profile)> SeedTeacherAsync(NaderGorge.Infrastructure.Data.AppDbContext db, string name, string phone)
    {
        var user = new User { FullName = name, PhoneNumber = phone, PasswordHash = "hash" };
        var profile = new TeacherProfile { User = user, UserId = user.Id, Bio = "Bio", Specialization = "Math", ContactInfo = phone };
        db.Users.Add(user);
        db.TeacherProfiles.Add(profile);
        await db.SaveChangesAsync();
        return (user, profile);
    }

    private static async Task SeedPackageGrantAsync(NaderGorge.Infrastructure.Data.AppDbContext db, Guid teacherId, Guid studentId, string packageName)
    {
        var subject = new Subject { Name = $"{packageName} subject", NormalizedName = Guid.NewGuid().ToString("N"), Description = "Description" };
        var package = new Package { Name = packageName, Description = "Description", Subject = subject, SubjectId = subject.Id, TeacherId = teacherId, TargetGrade = "ThirdSecondary" };
        db.Subjects.Add(subject);
        db.Packages.Add(package);
        db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = studentId, PackageId = package.Id, GrantType = CodeType.Package });
        await db.SaveChangesAsync();
    }

    private static async Task<Package> SeedScopedPackageAsync(NaderGorge.Infrastructure.Data.AppDbContext db, Guid teacherId, string packageName)
    {
        var subject = new Subject { Name = $"{packageName} subject", NormalizedName = Guid.NewGuid().ToString("N"), Description = "Description" };
        var package = new Package { Name = packageName, Description = "Description", Subject = subject, SubjectId = subject.Id, TeacherId = teacherId, TargetGrade = "SecondaryGrade3", IsActive = true };
        db.Subjects.Add(subject);
        db.Packages.Add(package);
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility { EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3, SubjectId = subject.Id, Subject = subject });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope { OwnerType = StudentFacingScopeOwnerType.Package, OwnerId = package.Id, ScopeLevel = AcademicScopeLevel.Exact, EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondaryGrade3, SubjectId = subject.Id, Subject = subject });
        await db.SaveChangesAsync();
        return package;
    }

    private static async Task<Package> SeedPackageWithVideoAsync(NaderGorge.Infrastructure.Data.AppDbContext db, Guid teacherId, Guid studentId)
    {
        var subject = new Subject { Name = "Ledger video subject", NormalizedName = Guid.NewGuid().ToString("N"), Description = "Description" };
        var package = new Package { Name = "باقة الفيديو", Description = "Description", Subject = subject, TeacherId = teacherId, TargetGrade = "SecondaryGrade3" };
        var term = new Term { Title = "الترم الأول", Package = package };
        var section = new ContentSection { Title = "القسم الأول", Term = term };
        var lesson = new Lesson { Title = "الحصة الأولى", Summary = "Summary", ContentSection = section };
        var videoType = new VideoType { Name = "شرح", NormalizedName = "شرح" };
        var video = new LessonVideo { Title = "الفيديو الأول", Provider = "youtube", ProviderVideoId = "ledger-video", Lesson = lesson, VideoType = videoType };
        db.AddRange(subject, package, term, section, lesson, videoType, video);
        db.StudentAccessGrants.Add(new StudentAccessGrant { UserId = studentId, PackageId = package.Id, GrantType = CodeType.Package });
        db.VideoWatchEvents.Add(new VideoWatchEvent { UserId = studentId, LessonVideo = video, ActualWatchedSeconds = 180, WatchCount = 3, LastPlaybackRate = 2, PlaybackRateBreakdownJson = "{\"1\":30,\"1.5\":90,\"2\":60}" });
        await db.SaveChangesAsync();
        return package;
    }
}

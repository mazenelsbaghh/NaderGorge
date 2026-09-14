using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public class AcademicScopeServiceTests
{
    [Fact]
    public async Task PlatformWideScope_AllowsStudentRegardlessOfStageGradeOrSubject()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var ownerId = Guid.NewGuid();
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);

        Assert.True(await service.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Package, ownerId, student.Id));
    }

    [Fact]
    public async Task ExactScope_AllowsOnlyWhenSubjectIsAllowedForStudentGrade()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db);
        var ownerId = Guid.NewGuid();
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            SubjectId = subject.Id
        });
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.Exact,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            SubjectId = subject.Id
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);

        Assert.True(await service.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Package, ownerId, student.Id));
    }

    [Fact]
    public async Task ExactScope_DeniesWhenSubjectIsNotAllowedForStudentGrade()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db);
        var ownerId = Guid.NewGuid();
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.Exact,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            SubjectId = subject.Id
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);
        var result = await service.ValidateStudentCanUseTargetAsync(StudentFacingScopeOwnerType.Package, ownerId, student.Id);

        Assert.False(result.IsEligible);
        Assert.Equal("ACADEMIC_SCOPE_DENIED", result.ErrorCode);
    }

    [Theory]
    [InlineData(AcademicScopeLevel.StageWide)]
    [InlineData(AcademicScopeLevel.GradeAllSubjects)]
    public async Task GeneralScopeLevels_AllowMatchingStudent(AcademicScopeLevel level)
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var ownerId = Guid.NewGuid();
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = ownerId,
            ScopeLevel = level,
            EducationStage = EducationStage.Secondary,
            GradeLevel = level == AcademicScopeLevel.GradeAllSubjects ? GradeLevel.FirstSecondary : null
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);

        Assert.True(await service.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Package, ownerId, student.Id));
    }

    [Fact]
    public async Task MultipleScopes_AllowWhenAnyScopeMatches()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var ownerId = Guid.NewGuid();
        db.StudentFacingAcademicScopes.AddRange(
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = ownerId,
                ScopeLevel = AcademicScopeLevel.StageWide,
                EducationStage = EducationStage.Primary
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = ownerId,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary
            });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);

        Assert.True(await service.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Package, ownerId, student.Id));
    }

    [Fact]
    public async Task MissingProfile_FailsClosed()
    {
        await using var db = TestAppDbContextFactory.Create();
        var ownerId = Guid.NewGuid();
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);
        var result = await service.ValidateStudentCanUseTargetAsync(StudentFacingScopeOwnerType.Package, ownerId, Guid.NewGuid());

        Assert.False(result.IsEligible);
        Assert.Equal("STUDENT_PROFILE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task LessonWithoutExplicitScope_InheritsNearestParentScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db);
        var teacher = await SeedTeacherAsync(db);
        var package = new Package
        {
            Name = "Scoped Package",
            Description = "Scoped",
            Price = 100,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary"
        };
        var term = new Term { Title = "Term", Order = 1, Package = package };
        var section = new ContentSection { Title = "Section", Order = 1, Term = term };
        var lesson = new Lesson { Title = "Lesson", Summary = "Summary", Order = 1, ContentSection = section };
        db.Packages.Add(package);
        db.Terms.Add(term);
        db.ContentSections.Add(section);
        db.Lessons.Add(lesson);
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);

        Assert.True(await service.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Lesson, lesson.Id, student.Id));
    }

    [Fact]
    public async Task LessonVideoBatch_UsesNearestScopeAndChildOverrideFailsClosed()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var subject = await SeedSubjectAsync(db);
        var teacher = await SeedTeacherAsync(db);
        var package = new Package
        {
            Name = "Batch scoped package",
            Description = "Scoped",
            Subject = subject,
            Teacher = teacher,
            TargetGrade = "FirstSecondary"
        };
        var term = new Term { Title = "Term", Package = package };
        var section = new ContentSection { Title = "Section", Term = term };
        var lesson = new Lesson { Title = "Lesson", ContentSection = section };
        var videoType = new VideoType
        {
            Name = "شرح",
            NormalizedName = $"EXPLANATION_{Guid.NewGuid():N}"
        };
        var inheritedVideo = new LessonVideo
        {
            Title = "Inherited",
            Provider = "youtube",
            ProviderVideoId = "inherited",
            Lesson = lesson,
            VideoType = videoType
        };
        var overriddenVideo = new LessonVideo
        {
            Title = "Overridden",
            Provider = "youtube",
            ProviderVideoId = "overridden",
            Lesson = lesson,
            VideoType = videoType
        };
        db.AddRange(package, term, section, lesson, videoType, inheritedVideo, overriddenVideo);
        db.StudentFacingAcademicScopes.AddRange(
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = package.Id,
                ScopeLevel = AcademicScopeLevel.PlatformWide
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.LessonVideo,
                OwnerId = overriddenVideo.Id,
                ScopeLevel = AcademicScopeLevel.StageWide,
                EducationStage = EducationStage.Primary
            });
        await db.SaveChangesAsync();

        var service = new AcademicScopeService(db);
        var eligibleIds = await service.GetEligibleLessonVideoIdsForStudentAsync(
            [inheritedVideo.Id, overriddenVideo.Id, inheritedVideo.Id, Guid.NewGuid()],
            student.Id);

        Assert.Contains(inheritedVideo.Id, eligibleIds);
        Assert.DoesNotContain(overriddenVideo.Id, eligibleIds);
        Assert.Single(eligibleIds);
        Assert.True(await service.IsOwnerEligibleForStudentAsync(
            StudentFacingScopeOwnerType.LessonVideo,
            inheritedVideo.Id,
            student.Id));
        Assert.False(await service.IsOwnerEligibleForStudentAsync(
            StudentFacingScopeOwnerType.LessonVideo,
            overriddenVideo.Id,
            student.Id));
    }

    [Theory]
    [InlineData("FirstSecondary", GradeLevel.FirstSecondary)]
    [InlineData("1st Secondary", GradeLevel.FirstSecondary)]
    [InlineData("SecondSecondary", GradeLevel.SecondSecondary)]
    [InlineData("3rd Secondary", GradeLevel.SecondaryGrade3)]
    public void TryNormalizeGradeAlias_MapsKnownAliases(string alias, GradeLevel expected)
    {
        Assert.True(AcademicScopeService.TryNormalizeGradeAlias(alias, out var actual));
        Assert.Equal(expected, actual);
    }

    private static async Task<User> SeedStudentAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        EducationStage stage,
        GradeLevel grade)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Student {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
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

    private static async Task<Subject> SeedSubjectAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var subject = new Subject
        {
            Name = $"Subject {Guid.NewGuid():N}",
            NormalizedName = Guid.NewGuid().ToString("N"),
            Description = "Subject"
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject;
    }

    private static async Task<TeacherProfile> SeedTeacherAsync(NaderGorge.Infrastructure.Data.AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Teacher {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var teacher = new TeacherProfile
        {
            UserId = user.Id,
            Specialization = "English",
            Bio = "Bio",
            ContactInfo = "Contact"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        return teacher;
    }
}

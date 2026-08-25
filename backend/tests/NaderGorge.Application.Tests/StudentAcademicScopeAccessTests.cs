using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Features.Community.Commands;
using NaderGorge.Application.Features.Community.Queries;
using NaderGorge.Application.Features.Content.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Application.Features.Student.Commands;
using NaderGorge.Application.Features.Student.Queries;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests;

public class StudentAcademicScopeAccessTests
{
    [Fact]
    public async Task GetPackagesQuery_ReturnsOnlyAcademicallyEligiblePackages()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var otherStudent = await SeedStudentAsync(db, EducationStage.Preparatory, GradeLevel.PrepGrade1);
        var teacher = await SeedTeacherAsync(db);
        var exactSubject = await SeedSubjectAsync(db, "Exact English");
        var generalSubject = await SeedSubjectAsync(db, "General Math");
        var hiddenSubject = await SeedSubjectAsync(db, "Hidden Physics");

        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            SubjectId = exactSubject.Id
        });

        var exact = SeedPackage(db, teacher, exactSubject, "Exact package");
        var platformWide = SeedPackage(db, teacher, generalSubject, "Platform package");
        platformWide.AllowFullPackagePurchase = false;
        var stageWide = SeedPackage(db, teacher, generalSubject, "Stage package");
        var gradeAllSubjects = SeedPackage(db, teacher, hiddenSubject, "Grade package");
        var nonMatching = SeedPackage(db, teacher, hiddenSubject, "Second secondary package");
        _ = SeedPackage(db, teacher, hiddenSubject, "Unscoped package");

        db.StudentFacingAcademicScopes.AddRange(
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = exact.Id,
                ScopeLevel = AcademicScopeLevel.Exact,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary,
                SubjectId = exactSubject.Id
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = platformWide.Id,
                ScopeLevel = AcademicScopeLevel.PlatformWide
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = stageWide.Id,
                ScopeLevel = AcademicScopeLevel.StageWide,
                EducationStage = EducationStage.Secondary
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = gradeAllSubjects.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = nonMatching.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.SecondSecondary
            });

        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var handler = new GetPackagesQueryHandler(db, new AccessCheckService(db, academicScope), academicScope);

        var result = await handler.Handle(new GetPackagesQuery(student.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var names = result.Data.Select(x => x.Name).OrderBy(x => x).ToList();
        Assert.Equal(
            ["Exact package", "Grade package", "Platform package", "Stage package"],
            names);
        Assert.False(result.Data.Single(package => package.Name == "Platform package").AllowFullPackagePurchase);
        Assert.True(result.Data.Single(package => package.Name == "Exact package").AllowFullPackagePurchase);

        var otherResult = await handler.Handle(new GetPackagesQuery(otherStudent.Id), CancellationToken.None);

        Assert.True(otherResult.Success);
        Assert.NotNull(otherResult.Data);
        Assert.Equal(["Platform package"], otherResult.Data.Select(x => x.Name).ToList());
    }

    [Fact]
    public async Task AccessCheckService_AllowsLessonWhenItInheritsMatchingPackageScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Inherited English");
        var package = SeedPackage(db, teacher, subject, "Inherited package");
        var lesson = SeedLessonHierarchy(db, package);

        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        });
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, academicScope);

        Assert.True(await access.HasAccessToLessonAsync(student.Id, lesson.Id));
    }

    [Fact]
    public async Task AccessCheckService_DeniesLessonWhenExplicitLessonScopeDoesNotMatch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Explicit English");
        var package = SeedPackage(db, teacher, subject, "Explicit package");
        var lesson = SeedLessonHierarchy(db, package);

        db.StudentFacingAcademicScopes.AddRange(
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Package,
                OwnerId = package.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary
            },
            new StudentFacingAcademicScope
            {
                OwnerType = StudentFacingScopeOwnerType.Lesson,
                OwnerId = lesson.Id,
                ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.SecondSecondary
            });
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, academicScope);

        Assert.False(await access.HasAccessToLessonAsync(student.Id, lesson.Id));
    }

    [Fact]
    public async Task GetPackageByIdQuery_DeniesDirectStudentRequestWhenPackageScopeDoesNotMatch()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Direct Physics");
        var package = SeedPackage(db, teacher, subject, "Direct denied package");

        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Package,
            OwnerId = package.Id,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var handler = new GetPackageByIdQueryHandler(db, new TeacherAuthorizationService(db), academicScope);

        var result = await handler.Handle(new GetPackageByIdQuery(package.Id, student.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", result.Errors ?? []);
    }

    [Fact]
    public async Task HierarchyQueries_FilterInheritedAndExplicitScopes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Hierarchy English");
        var package = SeedPackage(db, teacher, subject, "Hierarchy package");
        var allowedTerm = new Term { Title = "Allowed term", Order = 1, PackageId = package.Id, Package = package };
        var deniedTerm = new Term { Title = "Denied term", Order = 2, PackageId = package.Id, Package = package };
        var allowedSection = new ContentSection { Title = "Allowed section", Order = 1, TermId = allowedTerm.Id, Term = allowedTerm };
        var deniedSection = new ContentSection { Title = "Denied section", Order = 2, TermId = allowedTerm.Id, Term = allowedTerm };
        var allowedLesson = new Lesson { Title = "Allowed lesson", Summary = "Allowed", Order = 1, ContentSectionId = allowedSection.Id, ContentSection = allowedSection };
        var deniedLesson = new Lesson { Title = "Denied lesson", Summary = "Denied", Order = 2, ContentSectionId = allowedSection.Id, ContentSection = allowedSection };
        var inheritedExam = new Exam
        {
            Title = "Inherited exam",
            Description = "Inherited",
            PassingScore = 1,
            TotalScore = 1,
            CreatedByTeacherId = teacher.Id,
            CreatedByTeacher = teacher
        };
        var deniedExam = new Exam
        {
            Title = "Denied exam",
            Description = "Denied",
            PassingScore = 1,
            TotalScore = 1,
            CreatedByTeacherId = teacher.Id,
            CreatedByTeacher = teacher
        };
        allowedLesson.ExamId = inheritedExam.Id;
        var videoType = new VideoType { Name = "Main", NormalizedName = $"MAIN_{Guid.NewGuid():N}" };
        var allowedVideo = new LessonVideo
        {
            Title = "Allowed video",
            Provider = "test",
            ProviderVideoId = "allowed",
            Order = 1,
            VideoTypeId = videoType.Id,
            VideoType = videoType,
            LessonId = allowedLesson.Id,
            Lesson = allowedLesson
        };
        var deniedVideo = new LessonVideo
        {
            Title = "Denied video",
            Provider = "test",
            ProviderVideoId = "denied",
            Order = 2,
            VideoTypeId = videoType.Id,
            VideoType = videoType,
            LessonId = allowedLesson.Id,
            Lesson = allowedLesson
        };

        db.Terms.AddRange(allowedTerm, deniedTerm);
        db.ContentSections.AddRange(allowedSection, deniedSection);
        db.Lessons.AddRange(allowedLesson, deniedLesson);
        db.Exams.AddRange(inheritedExam, deniedExam);
        db.VideoTypes.Add(videoType);
        db.LessonVideos.AddRange(allowedVideo, deniedVideo);
        db.StudentFacingAcademicScopes.AddRange(
            MatchingScope(StudentFacingScopeOwnerType.Package, package.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.Term, deniedTerm.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.ContentSection, deniedSection.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.Lesson, deniedLesson.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.LessonVideo, deniedVideo.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.Exam, deniedExam.Id));
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, academicScope);
        var terms = await new GetTermsQueryHandler(db, academicScope)
            .Handle(new GetTermsQuery(package.Id, student.Id), CancellationToken.None);
        var sections = await new GetSectionsQueryHandler(db, academicScope)
            .Handle(new GetSectionsQuery(allowedTerm.Id, student.Id), CancellationToken.None);
        var lessons = await new GetLessonsQueryHandler(db, access, academicScope)
            .Handle(new GetLessonsQuery(allowedSection.Id, student.Id), CancellationToken.None);
        var detail = await new GetLessonDetailQueryHandler(db, access, new TeacherAuthorizationService(db), academicScope)
            .Handle(new GetLessonDetailQuery(deniedLesson.Id, student.Id), CancellationToken.None);

        Assert.Equal(["Allowed term"], terms.Data?.Select(x => x.Title).ToList());
        Assert.Equal(["Allowed section"], sections.Data?.Select(x => x.Title).ToList());
        var lesson = Assert.Single(lessons.Data ?? []);
        Assert.Equal("Allowed lesson", lesson.Title);
        Assert.Equal(["Allowed video"], lesson.Videos?.Select(x => x.Title).ToList());
        Assert.True(await academicScope.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Exam, inheritedExam.Id, student.Id));
        Assert.False(await academicScope.IsOwnerEligibleForStudentAsync(StudentFacingScopeOwnerType.Exam, deniedExam.Id, student.Id));
        Assert.False(detail.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", detail.Errors ?? []);
    }

    [Fact]
    public async Task LessonResourcesAndComments_DenyWhenCurrentAcademicScopeNoLongerMatches()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Comment English");
        var package = SeedPackage(db, teacher, subject, "Comment package");
        var lesson = SeedLessonHierarchy(db, package);

        db.StudentFacingAcademicScopes.AddRange(
            MatchingScope(StudentFacingScopeOwnerType.Package, package.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.Lesson, lesson.Id));
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        db.LessonResources.Add(new LessonResource
        {
            LessonId = lesson.Id,
            Lesson = lesson,
            Title = "Sheet",
            FileUrl = "/sheet.pdf",
            ResourceType = "PDF"
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, academicScope);
        var resources = await new GetLessonResourcesQueryHandler(db, access)
            .Handle(new GetLessonResourcesQuery(lesson.Id, student.Id), CancellationToken.None);
        var comment = await new CreateLessonCommentCommandHandler(db, access)
            .Handle(new CreateLessonCommentCommand(lesson.Id, student.Id, "Question"), CancellationToken.None);

        Assert.False(resources.Success);
        Assert.Contains("FORBIDDEN", resources.Errors ?? []);
        Assert.False(comment.Success);
        Assert.Contains("FORBIDDEN", comment.Errors ?? []);
        Assert.False(await db.LessonComments.AnyAsync(c => c.LessonId == lesson.Id));
    }

    [Fact]
    public async Task CommunityPosts_FilterAndDenyActionsOutsideAcademicScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var author = await TestAppDbContextFactory.SeedUserAsync(db, $"Author {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var visiblePost = new CommunityPost
        {
            AuthorUserId = author.Id,
            AuthorUser = author,
            Body = "Visible post",
            Status = CommunityPostStatus.Approved
        };
        var hiddenPost = new CommunityPost
        {
            AuthorUserId = author.Id,
            AuthorUser = author,
            Body = "Hidden post",
            Status = CommunityPostStatus.Approved,
            IsPoll = true
        };
        var hiddenOption = new CommunityPostPollOption
        {
            PostId = hiddenPost.Id,
            Post = hiddenPost,
            Text = "No"
        };

        db.CommunityPosts.AddRange(visiblePost, hiddenPost);
        db.CommunityPostPollOptions.Add(hiddenOption);
        db.StudentFacingAcademicScopes.AddRange(
            MatchingScope(StudentFacingScopeOwnerType.CommunityPost, visiblePost.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.CommunityPost, hiddenPost.Id));
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var feed = await new GetCommunityPostsQueryHandler(db, academicScope)
            .Handle(new GetCommunityPostsQuery(student.Id), CancellationToken.None);
        var comment = await new CreateCommunityPostCommentCommandHandler(db, academicScope)
            .Handle(new CreateCommunityPostCommentCommand(hiddenPost.Id, student.Id, "Comment"), CancellationToken.None);
        var like = await new ToggleCommunityPostLikeCommandHandler(db, academicScope)
            .Handle(new ToggleCommunityPostLikeCommand(hiddenPost.Id, student.Id), CancellationToken.None);
        var vote = await new ToggleCommunityPostVoteCommandHandler(db, academicScope)
            .Handle(new ToggleCommunityPostVoteCommand(hiddenPost.Id, hiddenOption.Id, student.Id), CancellationToken.None);

        Assert.Equal(["Visible post"], feed.Data?.Select(x => x.Body).ToList());
        Assert.False(comment.Success);
        Assert.False(like.Success);
        Assert.False(vote.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", comment.Errors ?? []);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", like.Errors ?? []);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", vote.Errors ?? []);
        Assert.False(await db.CommunityPostComments.AnyAsync(c => c.PostId == hiddenPost.Id));
        Assert.False(await db.CommunityPostLikes.AnyAsync(l => l.PostId == hiddenPost.Id));
        Assert.False(await db.CommunityPostPollVotes.AnyAsync(v => v.PostId == hiddenPost.Id));
    }

    [Fact]
    public async Task PublicExamProducts_FilterOutsideAcademicScopeBeforePaymentProjection()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var visibleExam = SeedExam(teacher, "Visible exam");
        var hiddenExam = SeedExam(teacher, "Hidden exam");
        var visibleProduct = new PublicExamProduct
        {
            ExamId = visibleExam.Id,
            Exam = visibleExam,
            Slug = $"visible-{Guid.NewGuid():N}",
            IsPublished = true,
            IsPaid = true,
            Price = 50
        };
        var hiddenProduct = new PublicExamProduct
        {
            ExamId = hiddenExam.Id,
            Exam = hiddenExam,
            Slug = $"hidden-{Guid.NewGuid():N}",
            IsPublished = true,
            IsPaid = false,
            Price = 0
        };

        db.Exams.AddRange(visibleExam, hiddenExam);
        db.PublicExamProducts.AddRange(visibleProduct, hiddenProduct);
        db.StudentFacingAcademicScopes.AddRange(
            MatchingScope(StudentFacingScopeOwnerType.PublicExamProduct, visibleProduct.Id),
            NonMatchingScope(StudentFacingScopeOwnerType.PublicExamProduct, hiddenProduct.Id));
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var result = await new GetPublicExamProductsQueryHandler(db, academicScope)
            .Handle(new GetPublicExamProductsQuery(PublishedOnly: true, StudentId: student.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["Visible exam"], result.Data?.Select(x => x.ExamTitle).ToList());
    }

    [Fact]
    public async Task ExistingGrant_StopsAuthorizingAfterStudentGradeNoLongerMatches()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Grant English");
        var package = SeedPackage(db, teacher, subject, "Granted package");

        db.StudentFacingAcademicScopes.Add(MatchingScope(StudentFacingScopeOwnerType.Package, package.Id));
        db.StudentAccessGrants.Add(new StudentAccessGrant
        {
            UserId = student.Id,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var academicScope = new AcademicScopeService(db);
        var access = new AccessCheckService(db, academicScope);
        Assert.True(await access.HasAccessToPackageAsync(student.Id, package.Id));

        var profile = await db.StudentProfiles.SingleAsync(x => x.UserId == student.Id);
        profile.GradeLevel = GradeLevel.SecondSecondary;
        await db.SaveChangesAsync();

        Assert.False(await access.HasAccessToPackageAsync(student.Id, package.Id));
        Assert.True(await db.StudentAccessGrants.AnyAsync(g => g.UserId == student.Id && g.PackageId == package.Id && g.IsActive));
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

    private static async Task<Subject> SeedSubjectAsync(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        string name)
    {
        var subject = new Subject
        {
            Name = name,
            NormalizedName = $"{name.ToUpperInvariant().Replace(' ', '_')}_{Guid.NewGuid():N}",
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
            User = user,
            Specialization = "English",
            Bio = "Bio",
            ContactInfo = "Contact"
        };
        db.TeacherProfiles.Add(teacher);
        await db.SaveChangesAsync();
        return teacher;
    }

    private static Package SeedPackage(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        TeacherProfile teacher,
        Subject subject,
        string name)
    {
        var package = new Package
        {
            Name = name,
            Description = $"{name} description",
            Price = 100,
            IsActive = true,
            SubjectId = subject.Id,
            Subject = subject,
            TeacherId = teacher.Id,
            Teacher = teacher,
            TargetGrade = "FirstSecondary"
        };
        db.Packages.Add(package);
        return package;
    }

    private static Lesson SeedLessonHierarchy(
        NaderGorge.Infrastructure.Data.AppDbContext db,
        Package package)
    {
        var term = new Term
        {
            Title = $"{package.Name} term",
            Order = 1,
            PackageId = package.Id,
            Package = package
        };
        var section = new ContentSection
        {
            Title = $"{package.Name} section",
            Order = 1,
            TermId = term.Id,
            Term = term
        };
        var lesson = new Lesson
        {
            Title = $"{package.Name} lesson",
            Summary = "Lesson summary",
            Order = 1,
            ContentSectionId = section.Id,
            ContentSection = section
        };

        db.Terms.Add(term);
        db.ContentSections.Add(section);
        db.Lessons.Add(lesson);
        return lesson;
    }

    [Fact]
    public async Task StudentNotifications_FilterAndDenyScopedNotificationsOutsideAcademicScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var student = await SeedStudentAsync(db, EducationStage.Secondary, GradeLevel.FirstSecondary);
        var teacher = await SeedTeacherAsync(db);
        var subject = await SeedSubjectAsync(db, "Notification Subject");
        var package = SeedPackage(db, teacher, subject, "Notification package");
        db.StudentFacingAcademicScopes.Add(NonMatchingScope(StudentFacingScopeOwnerType.Package, package.Id));
        var visibleNotification = new NotificationEvent
        {
            UserId = student.Id,
            ChannelType = NotificationChannelType.InApp,
            Title = "Visible",
            Body = "Visible notification"
        };
        var hiddenNotification = new NotificationEvent
        {
            UserId = student.Id,
            ChannelType = NotificationChannelType.InApp,
            Title = "Hidden",
            Body = "Hidden notification",
            AcademicScopeOwnerType = StudentFacingScopeOwnerType.Package,
            AcademicScopeOwnerId = package.Id
        };
        db.NotificationEvents.AddRange(visibleNotification, hiddenNotification);
        await db.SaveChangesAsync();
        var academicScope = new AcademicScopeService(db);
        var queryHandler = new GetStudentNotificationsQueryHandler(db, academicScope);
        var markHandler = new MarkNotificationAsReadCommandHandler(db, academicScope);

        var listResult = await queryHandler.Handle(new GetStudentNotificationsQuery(student.Id), CancellationToken.None);
        var markResult = await markHandler.Handle(new MarkNotificationAsReadCommand(hiddenNotification.Id, student.Id), CancellationToken.None);

        Assert.True(listResult.Success);
        Assert.Single(listResult.Data!);
        Assert.Equal(visibleNotification.Id, listResult.Data![0].Id);
        Assert.False(markResult.Success);
        Assert.Contains("ACADEMIC_SCOPE_DENIED", markResult.Errors ?? []);
        Assert.Null(await db.NotificationEvents.Where(x => x.Id == hiddenNotification.Id).Select(x => x.ReadAt).SingleAsync());
    }

    private static StudentFacingAcademicScope MatchingScope(StudentFacingScopeOwnerType ownerType, Guid ownerId)
    {
        return new StudentFacingAcademicScope
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary
        };
    }

    private static StudentFacingAcademicScope NonMatchingScope(StudentFacingScopeOwnerType ownerType, Guid ownerId)
    {
        return new StudentFacingAcademicScope
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            ScopeLevel = AcademicScopeLevel.GradeAllSubjects,
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.SecondSecondary
        };
    }

    private static Exam SeedExam(TeacherProfile teacher, string title)
    {
        return new Exam
        {
            Title = title,
            Description = $"{title} description",
            PassingScore = 1,
            TotalScore = 1,
            CreatedByTeacherId = teacher.Id,
            CreatedByTeacher = teacher
        };
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class StudentAcademicScopeAdminValidationTests
{
    [Fact]
    public async Task GetAcademicSubjectEligibilities_ReturnsOnlyActiveCombinations()
    {
        await using var db = TestAppDbContextFactory.Create();
        var subject = await SeedSubjectAsync(db);
        db.AcademicSubjectEligibilities.AddRange(
            new AcademicSubjectEligibility { EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.FirstSecondary, SubjectId = subject.Id, IsActive = true },
            new AcademicSubjectEligibility { EducationStage = EducationStage.Secondary, GradeLevel = GradeLevel.SecondSecondary, SubjectId = subject.Id, IsActive = false });
        await db.SaveChangesAsync();
        var handler = new GetAcademicSubjectEligibilitiesQueryHandler(db);

        var response = await handler.Handle(new GetAcademicSubjectEligibilitiesQuery(), default);

        var eligibility = Assert.Single(response.Data!);
        Assert.Equal(GradeLevel.FirstSecondary, eligibility.GradeLevel);
        Assert.Equal(subject.Name, eligibility.SubjectName);
    }

    [Fact]
    public async Task ValidateScopeDtos_RejectsMissingScopeList()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new AcademicScopeService(db);

        var result = await service.ValidateScopeDtosAsync([]);

        Assert.False(result.IsValid);
        Assert.Equal("ACADEMIC_SCOPE_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateScopeDtos_RejectsInvalidStageGradePair()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new AcademicScopeService(db);

        var result = await service.ValidateScopeDtosAsync([
            new AcademicScopeDto(
                AcademicScopeLevel.GradeAllSubjects,
                EducationStage.Secondary,
                GradeLevel.PrimaryGrade1)
        ]);

        Assert.False(result.IsValid);
        Assert.Equal("ACADEMIC_SCOPE_INVALID_GRADE", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateScopeDtos_RejectsExactScopeWhenSubjectIsNotEligibleForGrade()
    {
        await using var db = TestAppDbContextFactory.Create();
        var subject = await SeedSubjectAsync(db);
        var service = new AcademicScopeService(db);

        var result = await service.ValidateScopeDtosAsync([
            new AcademicScopeDto(
                AcademicScopeLevel.Exact,
                EducationStage.Secondary,
                GradeLevel.FirstSecondary,
                subject.Id)
        ]);

        Assert.False(result.IsValid);
        Assert.Equal("ACADEMIC_SCOPE_INVALID_SUBJECT", result.ErrorCode);
    }

    [Theory]
    [InlineData(AcademicScopeLevel.PlatformWide)]
    [InlineData(AcademicScopeLevel.StageWide)]
    [InlineData(AcademicScopeLevel.GradeAllSubjects)]
    [InlineData(AcademicScopeLevel.Exact)]
    public async Task ValidateScopeDtos_AcceptsEachValidScopeLevel(AcademicScopeLevel level)
    {
        await using var db = TestAppDbContextFactory.Create();
        var subject = await SeedSubjectAsync(db);
        db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
        {
            EducationStage = EducationStage.Secondary,
            GradeLevel = GradeLevel.FirstSecondary,
            SubjectId = subject.Id
        });
        await db.SaveChangesAsync();
        var service = new AcademicScopeService(db);

        var result = await service.ValidateScopeDtosAsync([BuildValidScope(level, subject.Id)]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SyncOwnerScopes_ReplacesExistingScopesWithValidatedRows()
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

        var result = await service.SyncOwnerScopesAsync(
            StudentFacingScopeOwnerType.Package,
            ownerId,
            [
                new AcademicScopeDto(
                    AcademicScopeLevel.StageWide,
                    EducationStage.Secondary)
            ]);

        Assert.True(result.IsValid);
        var rows = await db.StudentFacingAcademicScopes.Where(x => x.OwnerId == ownerId).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(AcademicScopeLevel.StageWide, rows[0].ScopeLevel);
        Assert.Equal(EducationStage.Secondary, rows[0].EducationStage);
    }

    [Fact]
    public async Task CreatePublicExamProduct_RejectsExplicitEmptyAcademicScopesBeforeSaving()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var handler = new CreatePublicExamProductCommandHandler(db);

        var result = await handler.Handle(new CreatePublicExamProductCommand(
            new CreatePublicExamRequest(
                "Scoped public exam",
                "Description",
                $"exam-{Guid.NewGuid():N}",
                teacher.Id,
                subject.Id,
                "FirstSecondary",
                true,
                false,
                0m,
                50m,
                100m,
                60,
                false,
                null,
                null,
                []),
            teacher.UserId),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_REQUIRED", result.Errors ?? []);
        Assert.Empty(db.PublicExamProducts);
        Assert.Empty(db.Exams);
    }

    [Fact]
    public async Task CreatePublicExamProduct_PersistsAcademicScopesForNewProduct()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var handler = new CreatePublicExamProductCommandHandler(db);

        var result = await handler.Handle(new CreatePublicExamProductCommand(
            new CreatePublicExamRequest(
                "Scoped public exam",
                "Description",
                $"exam-{Guid.NewGuid():N}",
                teacher.Id,
                subject.Id,
                "FirstSecondary",
                true,
                false,
                0m,
                50m,
                100m,
                60,
                false,
                null,
                null,
                [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subject.Id)]),
            teacher.UserId),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data.AcademicScopes ?? [], x => x.ScopeLevel == AcademicScopeLevel.Exact && x.SubjectId == subject.Id);
        Assert.True(await db.StudentFacingAcademicScopes.AnyAsync(x =>
            x.OwnerType == StudentFacingScopeOwnerType.PublicExamProduct &&
            x.OwnerId == result.Data.Id &&
            x.SubjectId == subject.Id));
    }

    [Fact]
    public async Task CreatePublicExamProduct_AllowsAdminSubjectOnlyWithoutProductTeacher()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, $"Admin {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var handler = new CreatePublicExamProductCommandHandler(db);

        var result = await handler.Handle(new CreatePublicExamProductCommand(
            new CreatePublicExamRequest(
                "Subject only public exam",
                "Description",
                $"exam-{Guid.NewGuid():N}",
                null,
                subject.Id,
                "FirstSecondary",
                true,
                false,
                0m,
                50m,
                100m,
                60,
                false,
                null,
                null,
                [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subject.Id)]),
            admin.Id),
            CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.TeacherId);
        var exam = await db.Exams.SingleAsync(x => x.Id == result.Data.ExamId);
        Assert.Equal(teacher.Id, exam.CreatedByTeacherId);
        var product = await db.PublicExamProducts.SingleAsync(x => x.Id == result.Data.Id);
        Assert.Null(product.TeacherId);
        Assert.Equal(subject.Id, product.SubjectId);
    }

    [Fact]
    public async Task CreateSalesCoupon_PersistsProvidedAcademicScopes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (_, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, $"Admin {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var handler = new CreateSalesCouponCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new CreateSalesCouponCommand(
            new SalesCouponRequest(
                $"C{Guid.NewGuid():N}"[..10],
                "Scoped coupon",
                DiscountType.Percentage,
                10m,
                SalesTargetType.Platform,
                null,
                SalesOwnerType.Platform,
                null,
                null,
                null,
                null,
                null,
                null,
                SalesStatus.Active,
                [new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subject.Id)]),
            actor.Id),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(await db.StudentFacingAcademicScopes.AnyAsync(x =>
            x.OwnerType == StudentFacingScopeOwnerType.SalesCoupon &&
            x.OwnerId == result.Data.Id &&
            x.SubjectId == subject.Id));
    }

    [Fact]
    public async Task CreatePrintableBatch_RejectsInvalidAcademicScopesBeforeCreatingCodes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, $"Admin {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var handler = new CreatePrintableBatchCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new CreatePrintableBatchCommand(
            new PrintableBatchRequest(
                "Invalid scoped printable batch",
                PrintableCodeBehavior.Discount,
                DiscountType.FixedAmount,
                20m,
                null,
                SalesTargetType.Platform,
                null,
                SalesOwnerType.Platform,
                null,
                null,
                null,
                3,
                1,
                null,
                null,
                SalesStatus.Active,
                []),
            actor.Id),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_REQUIRED", result.Errors ?? []);
        Assert.Empty(db.PrintableCodeBatches);
        Assert.Empty(db.PrintableSalesCodes);
    }

    [Fact]
    public async Task BulkGenerateCodes_RejectsInvalidAcademicScopesBeforeCreatingCodes()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var handler = new BulkGenerateCodesCommandHandler(db, new NoOpAuditService(), new AcademicScopeService(db));

        var result = await handler.Handle(new BulkGenerateCodesCommand(
            GroupName: "Invalid scoped balance codes",
            CodeType: CodeType.Balance,
            Count: 2,
            CodeLength: 8,
            AdminId: admin.Id,
            BalanceAmount: 100m,
            AcademicScopes: []),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_REQUIRED", result.Errors ?? []);
        Assert.Empty(db.CodeGroups);
        Assert.Empty(db.AccessCodes);
    }

    [Fact]
    public async Task BulkGenerateCodes_PersistsProvidedAcademicScopesOnCodeGroup()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var handler = new BulkGenerateCodesCommandHandler(db, new NoOpAuditService(), new AcademicScopeService(db));

        var result = await handler.Handle(new BulkGenerateCodesCommand(
            GroupName: "Platform scoped balance codes",
            CodeType: CodeType.Balance,
            Count: 2,
            CodeLength: 8,
            AdminId: admin.Id,
            BalanceAmount: 100m,
            AcademicScopes: [new AcademicScopeDto(AcademicScopeLevel.PlatformWide)]),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(await db.StudentFacingAcademicScopes.AnyAsync(x =>
            x.OwnerType == StudentFacingScopeOwnerType.CodeGroup &&
            x.OwnerId == result.Data.CodeGroupId &&
            x.ScopeLevel == AcademicScopeLevel.PlatformWide));
        Assert.Equal(2, await db.AccessCodes.CountAsync());
    }

    [Fact]
    public async Task ApproveCommunityPost_RejectsUnscopedPostBeforePublishing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var author = await TestAppDbContextFactory.SeedUserAsync(db, $"Author {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author, "Pending post");
        post.Status = CommunityPostStatus.Pending;
        await db.SaveChangesAsync();
        var handler = new ApproveCommunityPostCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new ApproveCommunityPostCommand(post.Id, admin.Id), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_TARGET_UNSCOPED", result.Errors ?? []);
        Assert.Equal(CommunityPostStatus.Pending, await db.CommunityPosts.Where(x => x.Id == post.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ApproveCommunityPost_AllowsPostWithValidScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var author = await TestAppDbContextFactory.SeedUserAsync(db, $"Author {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author, "Pending scoped post");
        post.Status = CommunityPostStatus.Pending;
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.CommunityPost,
            OwnerId = post.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();
        var handler = new ApproveCommunityPostCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new ApproveCommunityPostCommand(post.Id, admin.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CommunityPostStatus.Approved, await db.CommunityPosts.Where(x => x.Id == post.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ApproveCommunityPost_AllowsTeacherPostInheritingTeacherScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var author = await TestAppDbContextFactory.SeedUserAsync(db, $"Author {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var (teacher, _) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author, "Pending teacher post");
        post.Status = CommunityPostStatus.Pending;
        post.TeacherId = teacher.Id;
        db.StudentFacingAcademicScopes.Add(new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.Teacher,
            OwnerId = teacher.Id,
            ScopeLevel = AcademicScopeLevel.PlatformWide
        });
        await db.SaveChangesAsync();
        var handler = new ApproveCommunityPostCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new ApproveCommunityPostCommand(post.Id, admin.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CommunityPostStatus.Approved, await db.CommunityPosts.Where(x => x.Id == post.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task ApproveCommunityPost_AllowsTeacherPostWithoutAcademicScope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await SeedAdminAsync(db);
        var author = await TestAppDbContextFactory.SeedUserAsync(db, $"Author {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var (teacher, _) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var post = await TestAppDbContextFactory.SeedApprovedCommunityPostAsync(db, author, "Pending unscoped teacher post");
        post.Status = CommunityPostStatus.Pending;
        post.TeacherId = teacher.Id;
        await db.SaveChangesAsync();
        var handler = new ApproveCommunityPostCommandHandler(db, new AcademicScopeService(db));

        var result = await handler.Handle(new ApproveCommunityPostCommand(post.Id, admin.Id), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CommunityPostStatus.Approved, await db.CommunityPosts.Where(x => x.Id == post.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task CreateSharedPackage_RejectsExplicitEmptyAcademicScopesBeforeSaving()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var package = new Package
        {
            Name = "Shared package source",
            Description = "Package",
            Price = 100m,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary",
            IsActive = true
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        var controller = new AdminSharedPackagesController(db);

        var result = await controller.Create(new SaveSharedPackageDto(
            Name: "Invalid shared package",
            Slug: null,
            Description: "Description",
            ImageUrl: null,
            Price: 100m,
            DistributionMode: SharedPackageDistributionMode.Percentage,
            IsPublished: true,
            EducationStage: EducationStage.Secondary,
            GradeLevel: GradeLevel.FirstSecondary,
            AvailableFrom: null,
            AvailableUntil: null,
            Teachers:
            [
                new SharedPackageTeacherDto(
                    teacher.Id,
                    subject.Id,
                    TeacherAllocationMode.Percentage,
                    100m,
                    1)
            ],
            Items:
            [
                new SharedPackageItemDto(
                    teacher.Id,
                    subject.Id,
                    SalesTargetType.Package,
                    package.Id,
                    100m)
            ],
            AcademicScopes: []),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.SharedTeacherPackages);
        Assert.False(await db.StudentFacingAcademicScopes.AnyAsync(x => x.OwnerType == StudentFacingScopeOwnerType.SharedTeacherPackage));
    }

    [Fact]
    public async Task CreatePackage_RejectsExplicitEmptyAcademicScopesBeforeSaving()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var handler = new CreatePackageCommandHandler(db, new TeacherAuthorizationService(db));

        var result = await handler.Handle(new CreatePackageCommand(
            "Invalid scoped package",
            "Description",
            100m,
            subject.Id,
            "FirstSecondary",
            teacher.Id,
            teacher.UserId,
            []),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("ACADEMIC_SCOPE_REQUIRED", result.Errors ?? []);
        Assert.Empty(db.Packages);
    }

    [Fact]
    public async Task CreateTerm_PersistsExplicitAcademicScopesWhenProvided()
    {
        await using var db = TestAppDbContextFactory.Create();
        var (teacher, subject) = await SeedTeacherAndSubjectAsync(db, includeSubjectEligibility: true);
        var package = new Package
        {
            Name = "Scoped parent package",
            Description = "Package",
            Price = 100m,
            SubjectId = subject.Id,
            TeacherId = teacher.Id,
            TargetGrade = "FirstSecondary",
            IsActive = true
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        var handler = new CreateTermCommandHandler(db, new TeacherAuthorizationService(db));

        var result = await handler.Handle(new CreateTermCommand(
            "Scoped term",
            1,
            package.Id,
            50m,
            teacher.UserId,
            [new AcademicScopeDto(AcademicScopeLevel.PlatformWide)]),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(await db.StudentFacingAcademicScopes.AnyAsync(x =>
            x.OwnerType == StudentFacingScopeOwnerType.Term &&
            x.OwnerId == result.Data &&
            x.ScopeLevel == AcademicScopeLevel.PlatformWide));
    }

    private static AcademicScopeDto BuildValidScope(AcademicScopeLevel level, Guid subjectId)
    {
        return level switch
        {
            AcademicScopeLevel.PlatformWide => new AcademicScopeDto(AcademicScopeLevel.PlatformWide),
            AcademicScopeLevel.StageWide => new AcademicScopeDto(AcademicScopeLevel.StageWide, EducationStage.Secondary),
            AcademicScopeLevel.GradeAllSubjects => new AcademicScopeDto(AcademicScopeLevel.GradeAllSubjects, EducationStage.Secondary, GradeLevel.FirstSecondary),
            AcademicScopeLevel.Exact => new AcademicScopeDto(AcademicScopeLevel.Exact, EducationStage.Secondary, GradeLevel.FirstSecondary, subjectId),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
        };
    }

    private static async Task<Subject> SeedSubjectAsync(AppDbContext db)
    {
        var subject = new Subject
        {
            Name = $"Admin Validation Subject {Guid.NewGuid():N}",
            NormalizedName = Guid.NewGuid().ToString("N"),
            Description = "Subject"
        };
        db.Subjects.Add(subject);
        await db.SaveChangesAsync();
        return subject;
    }

    private static async Task<(TeacherProfile Teacher, Subject Subject)> SeedTeacherAndSubjectAsync(AppDbContext db, bool includeSubjectEligibility)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Teacher {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var teacher = new TeacherProfile
        {
            UserId = user.Id,
            Bio = "Bio",
            Specialization = "Math",
            CommissionRate = 0.2m,
            ContactInfo = "contact"
        };
        var subject = await SeedSubjectAsync(db);
        db.TeacherProfiles.Add(teacher);
        db.TeacherSubjects.Add(new TeacherSubject
        {
            Teacher = teacher,
            SubjectId = subject.Id
        });
        if (includeSubjectEligibility)
        {
            db.AcademicSubjectEligibilities.Add(new AcademicSubjectEligibility
            {
                EducationStage = EducationStage.Secondary,
                GradeLevel = GradeLevel.FirstSecondary,
                SubjectId = subject.Id
            });
        }

        await db.SaveChangesAsync();
        return (teacher, subject);
    }

    private static async Task<User> SeedAdminAsync(AppDbContext db)
    {
        var user = await TestAppDbContextFactory.SeedUserAsync(db, $"Admin {Guid.NewGuid():N}", Guid.NewGuid().ToString("N")[..11]);
        var role = new Role
        {
            Name = "Admin",
            Type = RoleType.Admin,
            PermissionsJson = """["codes.manage"]"""
        };
        db.Roles.Add(role);
        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            Role = role
        });
        await db.SaveChangesAsync();
        return user;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(
            string action,
            string entityType,
            Guid? entityId,
            Guid? userId,
            object? oldValues = null,
            object? newValues = null,
            string? ipAddress = null,
            string? correlationId = null)
        {
            return Task.CompletedTask;
        }
    }
}

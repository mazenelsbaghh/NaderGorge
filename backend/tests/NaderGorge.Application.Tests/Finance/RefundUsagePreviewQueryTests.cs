using NaderGorge.Application.Features.Admin.PlatformFinance.Refunds;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Finance;

public sealed class RefundUsagePreviewQueryTests
{
    [Fact]
    public async Task Historical_direct_grant_uses_content_price_as_a_manual_refund_ceiling()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var package = new Package
        {
            Name = "Historical package",
            Description = "Production refund regression",
            Price = 1350m,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            TargetGrade = "SecondSecondary"
        };
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            IsActive = true
        };
        db.AddRange(package, grant);
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsHistoricalSource);
        Assert.Equal(1350m, result.PaidAmount);
        Assert.Equal(1350m, result.RemainingRefundableAmount);
        Assert.Contains("بلا عملية شراء", result.HistoricalUsageNote);
    }

    [Fact]
    public async Task Access_code_grant_uses_content_price_as_a_manual_external_refund_ceiling()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var package = new Package
        {
            Name = "Code package",
            Description = "Manual external refund regression",
            Price = 500m,
            SubjectId = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            TargetGrade = "SecondSecondary"
        };
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Package,
            PackageId = package.Id,
            AccessCodeId = Guid.NewGuid(),
            IsActive = true
        };
        db.AddRange(package, grant);
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, null), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.IsHistoricalSource);
        Assert.Equal(500m, result.PaidAmount);
        Assert.Equal(500m, result.RemainingRefundableAmount);
    }

    [Fact]
    public async Task Rejects_sale_target_type_mismatch_even_when_target_id_matches()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Package,
            PackageId = targetId,
            IsActive = true
        };
        db.StudentAccessGrants.Add(grant);
        db.SalesFinancialEffects.Add(new SalesFinancialEffect
        {
            PurchaseOperationId = purchaseId,
            StudentId = studentId,
            TargetType = SalesTargetType.Term,
            TargetId = targetId,
            PaidAmount = 100m,
            GrossAmount = 100m,
            PlatformShareImpact = 100m
        });
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, purchaseId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Public_exam_uses_product_for_sale_identity_and_exam_for_usage_scope()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Exam,
            PublicExamProductId = productId,
            ExamId = examId,
            IsActive = true
        };
        db.StudentAccessGrants.Add(grant);
        db.SalesFinancialEffects.Add(Sale(purchaseId, studentId, SalesTargetType.PublicExam, productId, 80m));
        db.StudentExamAttempts.AddRange(
            new StudentExamAttempt { UserId = studentId, ExamId = examId, StartedAt = DateTime.UtcNow },
            new StudentExamAttempt { UserId = studentId, ExamId = examId, StartedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, purchaseId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalExams);
        Assert.Equal(1, result.AttemptedExams);
        Assert.Equal(2, result.TotalAttempts);
        Assert.False(result.VideosAvailable);
    }

    [Fact]
    public async Task Video_type_returns_financial_ceiling_and_explicitly_unavailable_usage()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var videoTypeId = Guid.NewGuid();
        var purchaseId = Guid.NewGuid();
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Video,
            VideoTypeId = videoTypeId,
            IsActive = true
        };
        db.StudentAccessGrants.Add(grant);
        db.SalesFinancialEffects.Add(Sale(purchaseId, studentId, SalesTargetType.VideoType, videoTypeId, 120m));
        db.PlatformRefunds.AddRange(
            Refund(purchaseId, studentId, 25m, PlatformRefundStatus.Posted, "legacy-alias"),
            Refund(purchaseId, studentId, 15m, PlatformRefundStatus.Reversed, "PurchaseOperation"));
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, purchaseId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result.UsageAvailable);
        Assert.Equal(25m, result.PreviouslyRefundedAmount);
        Assert.Equal(95m, result.RemainingRefundableAmount);
        Assert.NotEmpty(result.UnavailableReason!);
    }

    [Fact]
    public async Task Direct_video_excludes_parent_exam_and_counts_legacy_learning_progress()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var lesson = new Lesson { Title = "Parent", ContentSectionId = Guid.NewGuid(), ExamId = Guid.NewGuid() };
        var video = new LessonVideo
        {
            Title = "Selected video",
            LessonId = lesson.Id,
            VideoTypeId = Guid.NewGuid(),
            Provider = "custom",
            ProviderVideoId = "legacy"
        };
        var purchaseId = Guid.NewGuid();
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Video,
            LessonVideoId = video.Id,
            IsActive = true
        };
        db.AddRange(lesson, video, grant,
            Sale(purchaseId, studentId, SalesTargetType.SpecificVideo, video.Id, 60m),
            new VideoWatchEvent
            {
                UserId = studentId,
                LessonVideoId = video.Id,
                LearningWatchedSeconds = 60m,
                LearningDurationSeconds = 60,
                ActualWatchedSeconds = 0m
            });
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, purchaseId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalExams);
        Assert.Equal(1, result.WatchedVideos);
        Assert.Equal(1, result.CompletedVideos);
        Assert.Equal(0, result.UnknownDurationVideos);
    }

    [Fact]
    public async Task Missing_video_duration_is_reported_as_unknown_not_incomplete()
    {
        await using var db = TestAppDbContextFactory.Create();
        var studentId = Guid.NewGuid();
        var video = new LessonVideo
        {
            Title = "Unknown duration",
            LessonId = Guid.NewGuid(),
            VideoTypeId = Guid.NewGuid(),
            Provider = "custom",
            ProviderVideoId = "unknown"
        };
        var purchaseId = Guid.NewGuid();
        var grant = new StudentAccessGrant
        {
            UserId = studentId,
            GrantType = CodeType.Video,
            LessonVideoId = video.Id,
            IsActive = true
        };
        db.AddRange(video, grant,
            Sale(purchaseId, studentId, SalesTargetType.SpecificVideo, video.Id, 40m),
            new VideoWatchEvent
            {
                UserId = studentId,
                LessonVideoId = video.Id,
                LearningWatchedSeconds = 10m,
                ActualWatchedSeconds = 10m
            });
        await db.SaveChangesAsync();

        var result = await new GetRefundUsagePreviewQueryHandler(db).Handle(
            new(studentId, grant.Id, purchaseId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.WatchedVideos);
        Assert.Equal(0, result.CompletedVideos);
        Assert.Equal(1, result.UnknownDurationVideos);
    }

    private static SalesFinancialEffect Sale(Guid purchaseId, Guid studentId, SalesTargetType type, Guid targetId, decimal amount) => new()
    {
        PurchaseOperationId = purchaseId,
        StudentId = studentId,
        TargetType = type,
        TargetId = targetId,
        PaidAmount = amount,
        GrossAmount = amount,
        PlatformShareImpact = amount
    };

    private static PlatformRefund Refund(Guid purchaseId, Guid studentId, decimal amount, PlatformRefundStatus status, string sourceType) => new()
    {
        OriginalSourceId = purchaseId,
        OriginalSourceType = sourceType,
        StudentId = studentId,
        PlatformAmount = amount,
        Status = status,
        Reason = "test",
        CreatedByUserId = Guid.NewGuid()
    };
}

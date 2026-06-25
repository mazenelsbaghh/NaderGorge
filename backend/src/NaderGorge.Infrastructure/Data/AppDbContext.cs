using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CodeGroup> CodeGroups => Set<CodeGroup>();
    public DbSet<AccessCode> AccessCodes => Set<AccessCode>();
    public DbSet<StudentAccessGrant> StudentAccessGrants => Set<StudentAccessGrant>();

    // Content
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageCodePageProfile> PackageCodePageProfiles => Set<PackageCodePageProfile>();
    public DbSet<ContentSection> ContentSections => Set<ContentSection>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<BunnyVideoAsset> BunnyVideoAssets => Set<BunnyVideoAsset>();
    public DbSet<BunnyUsageSnapshot> BunnyUsageSnapshots => Set<BunnyUsageSnapshot>();
    public DbSet<VideoChapter> VideoChapters => Set<VideoChapter>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    public DbSet<LessonComment> LessonComments => Set<LessonComment>();
    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityPostComment> CommunityPostComments => Set<CommunityPostComment>();
    public DbSet<CommunityPostLike> CommunityPostLikes => Set<CommunityPostLike>();
    public DbSet<CommunityPostPollOption> CommunityPostPollOptions => Set<CommunityPostPollOption>();
    public DbSet<CommunityPostPollVote> CommunityPostPollVotes => Set<CommunityPostPollVote>();
    public DbSet<TeacherPhoto> TeacherPhotos => Set<TeacherPhoto>();
    public DbSet<CustomForm> CustomForms => Set<CustomForm>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    // Phase 3: Term, Balance, Code extensions
    public DbSet<Term> Terms => Set<Term>();
    public DbSet<StudentBalance> StudentBalances => Set<StudentBalance>();
    public DbSet<BalanceTransaction> BalanceTransactions => Set<BalanceTransaction>();
    public DbSet<CodeVideoTarget> CodeVideoTargets => Set<CodeVideoTarget>();

    // Tracking
    public DbSet<VideoWatchEvent> VideoWatchEvents => Set<VideoWatchEvent>();
    public DbSet<ExtraWatchRequest> ExtraWatchRequests => Set<ExtraWatchRequest>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    public DbSet<VideoPlaybackSession> VideoPlaybackSessions => Set<VideoPlaybackSession>();
    public DbSet<VideoOverride> VideoOverrides => Set<VideoOverride>();

    // Exams
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<StudentExamAttempt> StudentExamAttempts => Set<StudentExamAttempt>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
    public DbSet<EssaySubmission> EssaySubmissions => Set<EssaySubmission>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    // Phase 2: Homework & Academic Ops
    public DbSet<Homework> Homeworks => Set<Homework>();
    public DbSet<HomeworkQuestion> HomeworkQuestions => Set<HomeworkQuestion>();
    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();
    public DbSet<HomeworkAnswer> HomeworkAnswers => Set<HomeworkAnswer>();

    // Phase 2: Gamification
    public DbSet<StudentGamification> StudentGamifications => Set<StudentGamification>();
    public DbSet<GamificationActionLog> GamificationActionLogs => Set<GamificationActionLog>();
    public DbSet<StudentBadge> StudentBadges => Set<StudentBadge>();

    // Phase 2: Student Tracking
    public DbSet<StudentStatusTracker> StudentStatusTrackers => Set<StudentStatusTracker>();
    public DbSet<WarningEvent> WarningEvents => Set<WarningEvent>();

    // Phase 2: Assistant Ops
    public DbSet<AssistantTaskQueue> AssistantTasks => Set<AssistantTaskQueue>();

    // Phase 2: Notifications
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<ParentDeviceToken> ParentDeviceTokens => Set<ParentDeviceToken>();
    public DbSet<StudentNote> StudentNotes => Set<StudentNote>();

    // Phase 2: HR Core
    public DbSet<EmployeeProfile> EmployeeProfiles => Set<EmployeeProfile>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<EmployeeVacation> EmployeeVacations => Set<EmployeeVacation>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    // Phase 5: Internal Chat
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatParticipant> ChatParticipants => Set<ChatParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatMessageReadState> ChatMessageReadStates => Set<ChatMessageReadState>();

    // Live Support Command Center
    public DbSet<LiveSupportConversation> LiveSupportConversations => Set<LiveSupportConversation>();
    public DbSet<LiveSupportGuestSession> LiveSupportGuestSessions => Set<LiveSupportGuestSession>();
    public DbSet<LiveSupportStaffConfig> LiveSupportStaffConfigs => Set<LiveSupportStaffConfig>();
    public DbSet<LiveSupportScheduleWindow> LiveSupportScheduleWindows => Set<LiveSupportScheduleWindow>();
    public DbSet<LiveSupportQueueEntry> LiveSupportQueueEntries => Set<LiveSupportQueueEntry>();
    public DbSet<LiveSupportAssignment> LiveSupportAssignments => Set<LiveSupportAssignment>();
    public DbSet<LiveSupportMessage> LiveSupportMessages => Set<LiveSupportMessage>();
    public DbSet<LiveSupportAttachment> LiveSupportAttachments => Set<LiveSupportAttachment>();
    public DbSet<LiveSupportStudentLinkHistory> LiveSupportStudentLinkHistories => Set<LiveSupportStudentLinkHistory>();
    public DbSet<LiveSupportEvent> LiveSupportEvents => Set<LiveSupportEvent>();
    public DbSet<LiveSupportActionExecution> LiveSupportActionExecutions => Set<LiveSupportActionExecution>();
    public DbSet<LiveSupportRating> LiveSupportRatings => Set<LiveSupportRating>();
    public DbSet<LiveSupportAIPolicyVersion> LiveSupportAIPolicyVersions => Set<LiveSupportAIPolicyVersion>();
    public DbSet<LiveSupportAIKnowledgeEntry> LiveSupportAIKnowledgeEntries => Set<LiveSupportAIKnowledgeEntry>();
    public DbSet<LiveSupportAIKnowledgeRevision> LiveSupportAIKnowledgeRevisions => Set<LiveSupportAIKnowledgeRevision>();
    public DbSet<LiveSupportAIPolicyKnowledgeRevision> LiveSupportAIPolicyKnowledgeRevisions => Set<LiveSupportAIPolicyKnowledgeRevision>();
    public DbSet<LiveSupportAIConversationState> LiveSupportAIConversationStates => Set<LiveSupportAIConversationState>();
    public DbSet<LiveSupportAITurn> LiveSupportAITurns => Set<LiveSupportAITurn>();
    public DbSet<LiveSupportAIPendingAction> LiveSupportAIPendingActions => Set<LiveSupportAIPendingAction>();
    public DbSet<LiveSupportAIVerificationPolicyQuestion> LiveSupportAIVerificationPolicyQuestions => Set<LiveSupportAIVerificationPolicyQuestion>();
    public DbSet<LiveSupportAIVerificationSession> LiveSupportAIVerificationSessions => Set<LiveSupportAIVerificationSession>();
    public DbSet<LiveSupportAIVerificationAttempt> LiveSupportAIVerificationAttempts => Set<LiveSupportAIVerificationAttempt>();

    // Phase 6: Call Center CRM
    public DbSet<CrmStudentStatus> CrmStudentStatuses => Set<CrmStudentStatus>();
    public DbSet<CrmCallLog> CrmCallLogs => Set<CrmCallLog>();

    // Phase 8: Media Production & Social Planner
    public DbSet<MediaProductionPipeline> MediaProductionPipelines => Set<MediaProductionPipeline>();
    public DbSet<SocialMediaPlan> SocialMediaPlans => Set<SocialMediaPlan>();

    // Phase 9: Payroll & Teacher Finance
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<PayrollAdjustment> PayrollAdjustments => Set<PayrollAdjustment>();
    public DbSet<TeacherAccount> TeacherAccounts => Set<TeacherAccount>();
    public DbSet<TeacherPayout> TeacherPayouts => Set<TeacherPayout>();
    public DbSet<AccessCodeActivationLog> AccessCodeActivationLogs => Set<AccessCodeActivationLog>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<WebVitalsMetric> WebVitalsMetrics => Set<WebVitalsMetric>();

    // SMS Payment Auto-Matcher
    public DbSet<DigitalWallet> DigitalWallets => Set<DigitalWallet>();
    public DbSet<RechargeRequest> RechargeRequests => Set<RechargeRequest>();
    public DbSet<IncomingSmsLog> IncomingSmsLogs => Set<IncomingSmsLog>();

    public Task<StudentAnswer?> FindStudentAnswerAsync(
        Guid studentExamAttemptId,
        Guid examQuestionId,
        CancellationToken cancellationToken = default)
    {
        return StudentAnswers.FirstOrDefaultAsync(
            answer => answer.StudentExamAttemptId == studentExamAttemptId && answer.ExamQuestionId == examQuestionId,
            cancellationToken);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.PhoneNumber).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            e.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        // Subject
        modelBuilder.Entity<Subject>(e =>
        {
            e.ToTable("subjects");
            e.HasKey(s => s.Id);
            e.Property(s => s.Name).HasMaxLength(200).IsRequired();
            e.Property(s => s.NormalizedName).HasMaxLength(200).IsRequired();
            e.HasIndex(s => s.NormalizedName).IsUnique();
        });

        // TeacherProfile
        modelBuilder.Entity<TeacherProfile>(e =>
        {
            e.ToTable("teacher_profiles");
            e.HasKey(tp => tp.Id);
            e.HasIndex(tp => tp.UserId).IsUnique();
            e.HasOne(tp => tp.User).WithOne(u => u.TeacherProfile).HasForeignKey<TeacherProfile>(tp => tp.UserId);
            e.Property(tp => tp.Specialization).HasMaxLength(200).IsRequired();
            e.Property(tp => tp.ProfileImageUrl).HasMaxLength(1000);
            e.Property(tp => tp.ContactInfo).HasMaxLength(500).IsRequired();
            e.Property(tp => tp.CommissionRate).HasPrecision(18, 2);
        });

        // TeacherSubject
        modelBuilder.Entity<TeacherSubject>(e =>
        {
            e.ToTable("teacher_subjects");
            e.HasKey(ts => new { ts.TeacherId, ts.SubjectId });
            e.HasOne(ts => ts.Teacher).WithMany(t => t.TeacherSubjects).HasForeignKey(ts => ts.TeacherId);
            e.HasOne(ts => ts.Subject).WithMany(s => s.TeacherSubjects).HasForeignKey(ts => ts.SubjectId);
        });

        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
            e.Property(r => r.PermissionsJson).HasMaxLength(4000).HasDefaultValue("[]");
            e.Property(r => r.AllowedDomain).HasMaxLength(50).HasDefaultValue("all");
            e.Property(r => r.AllowedNavbarItemsJson).HasMaxLength(4000).HasDefaultValue("[]");
        });

        // UserRole (many-to-many)
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(ur => new { ur.UserId, ur.RoleId });
            e.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            e.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.Action);
            e.HasIndex(a => a.EntityType);
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => new { a.PerformedByUserId, a.CreatedAt });
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            e.Property(a => a.IpAddress).HasMaxLength(45);
            e.Property(a => a.CorrelationId).HasMaxLength(64);
            e.HasOne(a => a.PerformedByUser).WithMany().HasForeignKey(a => a.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // StudentProfile
        modelBuilder.Entity<StudentProfile>(e =>
        {
            e.ToTable("student_profiles");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId).IsUnique();
            e.HasOne(s => s.User).WithOne(u => u.StudentProfile).HasForeignKey<StudentProfile>(s => s.UserId);
            e.Property(s => s.StudentCode).HasMaxLength(100);    // No longer IsRequired()
            e.Property(s => s.Governorate).HasMaxLength(100).IsRequired();
            e.Property(s => s.District).HasMaxLength(200);           // NEW
            e.Property(s => s.Address).HasMaxLength(500).IsRequired();
            e.Property(s => s.ParentPhone).HasMaxLength(20);
            e.Property(s => s.SecondaryPhone).HasMaxLength(20);         // NEW
            e.Property(s => s.SecondaryParentPhone).HasMaxLength(20);   // NEW
            e.Property(s => s.EducationStage).HasConversion<int>();
            e.Property(s => s.GradeLevel).HasConversion<int>();
            e.Property(s => s.StudyTrack).HasConversion<int?>();
            e.Property(s => s.Gender).HasConversion<int>();
            e.Property(s => s.LightThemePaletteId).HasMaxLength(100);
            e.Property(s => s.DarkThemePaletteId).HasMaxLength(100);
            e.Property(s => s.CurrentMode).HasMaxLength(10).HasDefaultValue("light");
            e.Property(s => s.ParentTrackingCode).HasMaxLength(6);
            e.HasIndex(s => s.ParentTrackingCode).IsUnique();
            e.Property(s => s.HasSeenTrackingCodePopup).HasDefaultValue(false);
        });

        // Device
        modelBuilder.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.UserId, d.DeviceFingerprint }).IsUnique();
            e.HasOne(d => d.User).WithMany(u => u.Devices).HasForeignKey(d => d.UserId);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Token).IsUnique();
            e.HasOne(r => r.User).WithMany(u => u.RefreshTokens).HasForeignKey(r => r.UserId);
        });

        // CodeGroup
        modelBuilder.Entity<CodeGroup>(e =>
        {
            e.ToTable("code_groups");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.CodeType).HasConversion<int>();
            e.Property(c => c.DiscountPercentage).HasColumnType("decimal(18,2)");
            e.Property(c => c.BalanceAmount).HasColumnType("decimal(18,2)");
            e.HasOne(c => c.CreatedByUser).WithMany().HasForeignKey(c => c.CreatedByUserId);
            e.HasOne(c => c.Teacher).WithMany(t => t.CodeGroups).HasForeignKey(c => c.TeacherId);
        });

        // AccessCode
        modelBuilder.Entity<AccessCode>(e =>
        {
            e.ToTable("access_codes");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.CodeHash).IsUnique();
            e.HasOne(a => a.CodeGroup).WithMany(g => g.AccessCodes).HasForeignKey(a => a.CodeGroupId);
            e.HasOne(a => a.ConsumedByUser).WithMany().HasForeignKey(a => a.ConsumedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // StudentAccessGrant
        modelBuilder.Entity<StudentAccessGrant>(e =>
        {
            e.ToTable("student_access_grants");
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.UserId, s.PackageId });
            e.Property(s => s.GrantType).HasConversion<int>();
            e.Property(s => s.CancellationReason).HasMaxLength(1000);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
            e.HasOne(s => s.AccessCode).WithMany().HasForeignKey(s => s.AccessCodeId);
            e.HasOne(s => s.CancelledByUser).WithMany().HasForeignKey(s => s.CancelledByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // Package
        modelBuilder.Entity<Package>(e =>
        {
            e.ToTable("packages");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.HasOne(p => p.Subject).WithMany(s => s.Packages).HasForeignKey(p => p.SubjectId);
            e.HasOne(p => p.Teacher).WithMany(t => t.Packages).HasForeignKey(p => p.TeacherId);
            e.Property(p => p.TargetGrade).HasMaxLength(100).IsRequired().HasDefaultValue("All");
        });

        modelBuilder.Entity<PackageCodePageProfile>(e =>
        {
            e.ToTable("package_code_page_profiles");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.PackageId).IsUnique();
            e.Property(p => p.Status).HasConversion<int>();
            e.Property(p => p.HeroEyebrow).HasMaxLength(80);
            e.Property(p => p.HeroTitle).HasMaxLength(140);
            e.Property(p => p.HeroDescription).HasMaxLength(600);
            e.Property(p => p.OfferTitle).HasMaxLength(120);
            e.Property(p => p.OfferDescription).HasMaxLength(600);
            e.Property(p => p.ActivationTitle).HasMaxLength(120);
            e.Property(p => p.ActivationDescription).HasMaxLength(500);
            e.Property(p => p.SupportTitle).HasMaxLength(120);
            e.Property(p => p.SupportDescription).HasMaxLength(400);
            e.Property(p => p.ThemeAccentKey).HasMaxLength(60);
            e.HasOne(p => p.Package)
                .WithOne()
                .HasForeignKey<PackageCodePageProfile>(p => p.PackageId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.UpdatedByUser)
                .WithMany()
                .HasForeignKey(p => p.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ContentSection
        modelBuilder.Entity<ContentSection>(e =>
        {
            e.ToTable("content_sections");
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.ImageUrl).HasMaxLength(500);
            e.HasOne(c => c.Term).WithMany(t => t.Sections).HasForeignKey(c => c.TermId);
        });

        // Lesson
        modelBuilder.Entity<Lesson>(e =>
        {
            e.ToTable("lessons");
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.ContentSection).WithMany(cs => cs.Lessons).HasForeignKey(l => l.ContentSectionId);
        });

        // LessonVideo
        modelBuilder.Entity<LessonVideo>(e =>
        {
            e.ToTable("lesson_videos");
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.Lesson).WithMany(le => le.Videos).HasForeignKey(l => l.LessonId);
            e.HasOne(l => l.Exam)
             .WithMany()
             .HasForeignKey(l => l.ExamId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BunnyVideoAsset>(e =>
        {
            e.ToTable("bunny_video_assets");
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.LessonVideoId).IsUnique();
            e.HasIndex(b => b.BunnyVideoGuid).IsUnique();
            e.HasIndex(b => new { b.TeacherId, b.PackageId, b.LessonId });
            e.HasIndex(b => new { b.Status, b.LastStatusSyncedAtUtc });
            e.Property(b => b.BunnyVideoGuid).HasMaxLength(100).IsRequired();
            e.Property(b => b.BunnyCollectionId).HasMaxLength(100);
            e.Property(b => b.Title).HasMaxLength(200).IsRequired();
            e.Property(b => b.UploadMethod).HasMaxLength(40).IsRequired();
            e.Property(b => b.Status).HasMaxLength(40).IsRequired();
            e.Property(b => b.OriginalFileName).HasMaxLength(500);
            e.Property(b => b.SourceUrlHash).HasMaxLength(128);
            e.Property(b => b.ErrorMessage).HasMaxLength(2000);
            e.HasOne(b => b.LessonVideo).WithOne(v => v.BunnyVideoAsset).HasForeignKey<BunnyVideoAsset>(b => b.LessonVideoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Teacher).WithMany().HasForeignKey(b => b.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Package).WithMany().HasForeignKey(b => b.PackageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.Lesson).WithMany().HasForeignKey(b => b.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(b => b.UploadedByUser).WithMany().HasForeignKey(b => b.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BunnyUsageSnapshot>(e =>
        {
            e.ToTable("bunny_usage_snapshots");
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.BunnyVideoAssetId, s.PeriodStartUtc, s.PeriodEndUtc }).IsUnique();
            e.HasIndex(s => new { s.TeacherId, s.PeriodStartUtc, s.PeriodEndUtc });
            e.HasIndex(s => new { s.PackageId, s.PeriodStartUtc, s.PeriodEndUtc });
            e.Property(s => s.BandwidthSource).HasMaxLength(80).IsRequired();
            e.Property(s => s.StorageRateUsdPerGb).HasPrecision(18, 6);
            e.Property(s => s.BandwidthRateUsdPerGb).HasPrecision(18, 6);
            e.Property(s => s.StorageCostUsd).HasPrecision(18, 6);
            e.Property(s => s.BandwidthCostUsd).HasPrecision(18, 6);
            e.Property(s => s.TotalCostUsd).HasPrecision(18, 6);
            e.Property(s => s.Notes).HasMaxLength(1000);
            e.HasOne(s => s.BunnyVideoAsset).WithMany(b => b.UsageSnapshots).HasForeignKey(s => s.BunnyVideoAssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.SyncedByUser).WithMany().HasForeignKey(s => s.SyncedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // VideoChapter
        modelBuilder.Entity<VideoChapter>(e =>
        {
            e.ToTable("video_chapters");
            e.HasKey(v => v.Id);
            e.Property(v => v.Title).HasMaxLength(200).IsRequired();
            e.Property(v => v.SummaryText).HasMaxLength(2000);
            e.Property(v => v.MindmapImageUrl).HasMaxLength(2000);
            e.HasOne(v => v.LessonVideo).WithMany(le => le.VideoChapters).HasForeignKey(v => v.LessonVideoId).OnDelete(DeleteBehavior.Cascade);
        });

        // TeacherPhoto
        modelBuilder.Entity<TeacherPhoto>(e =>
        {
            e.ToTable("teacher_photos");
            e.HasKey(t => t.Id);
            e.Property(t => t.FileUrl).HasMaxLength(2000).IsRequired();
            e.HasOne(t => t.Teacher).WithMany().HasForeignKey(t => t.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        // LessonResource
        modelBuilder.Entity<LessonResource>(e =>
        {
            e.ToTable("lesson_resources");
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.Lesson).WithMany(le => le.Resources).HasForeignKey(l => l.LessonId);
        });

        // LessonComment
        modelBuilder.Entity<LessonComment>(e =>
        {
            e.ToTable("lesson_comments");
            e.HasKey(lc => lc.Id);
            e.Property(lc => lc.Body).HasMaxLength(2000).IsRequired();
            e.Property(lc => lc.Status).HasConversion<int>();
            e.HasIndex(lc => lc.LessonId);
            e.HasIndex(lc => lc.Status);
            e.HasIndex(lc => lc.CreatedAt);
            e.HasIndex(lc => new { lc.LessonId, lc.CreatedAt });
            e.HasIndex(lc => new { lc.Status, lc.CreatedAt });
            e.HasOne(lc => lc.Lesson)
                .WithMany(l => l.Comments)
                .HasForeignKey(lc => lc.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(lc => lc.AuthorUser)
                .WithMany()
                .HasForeignKey(lc => lc.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(lc => lc.ReviewedByUser)
                .WithMany()
                .HasForeignKey(lc => lc.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CommunityPost
        modelBuilder.Entity<CommunityPost>(e =>
        {
            e.ToTable("community_posts");
            e.HasKey(cp => cp.Id);
            e.Property(cp => cp.Body).HasMaxLength(4000).IsRequired();
            e.Property(cp => cp.Status).HasConversion<int>();
            e.HasIndex(cp => cp.AuthorUserId);
            e.HasIndex(cp => cp.Status);
            e.HasIndex(cp => cp.CreatedAt);
            e.HasOne(cp => cp.AuthorUser)
                .WithMany()
                .HasForeignKey(cp => cp.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cp => cp.ReviewedByUser)
                .WithMany()
                .HasForeignKey(cp => cp.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CommunityPostComment
        modelBuilder.Entity<CommunityPostComment>(e =>
        {
            e.ToTable("community_post_comments");
            e.HasKey(c => c.Id);
            e.Property(c => c.Body).HasMaxLength(2000).IsRequired();
            e.Property(c => c.Status).HasConversion<int>();
            e.Property(c => c.RejectionReason).HasMaxLength(1000);
            e.HasIndex(c => c.PostId);
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.CreatedAt);
            e.HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.AuthorUser)
                .WithMany()
                .HasForeignKey(c => c.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.ReviewedByUser)
                .WithMany()
                .HasForeignKey(c => c.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // CommunityPostLike
        modelBuilder.Entity<CommunityPostLike>(e =>
        {
            e.ToTable("community_post_likes");
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.PostId);
            e.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
            e.HasOne(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // CommunityPostPollOption
        modelBuilder.Entity<CommunityPostPollOption>(e =>
        {
            e.ToTable("community_post_poll_options");
            e.HasKey(o => o.Id);
            e.Property(o => o.Text).HasMaxLength(200).IsRequired();
            e.HasOne(o => o.Post)
                .WithMany(p => p.PollOptions)
                .HasForeignKey(o => o.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CommunityPostPollVote
        modelBuilder.Entity<CommunityPostPollVote>(e =>
        {
            e.ToTable("community_post_poll_votes");
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.PostId, v.UserId }).IsUnique(); // One vote per post per user
            e.HasOne(v => v.Post)
                .WithMany(p => p.PollVotes)
                .HasForeignKey(v => v.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(v => v.PollOption)
                .WithMany()
                .HasForeignKey(v => v.PollOptionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // VideoWatchEvent
        modelBuilder.Entity<VideoWatchEvent>(e =>
        {
            e.ToTable("video_watch_events");
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.UserId, v.LessonVideoId }).IsUnique();
            e.HasOne(v => v.User).WithMany().HasForeignKey(v => v.UserId);
            e.HasOne(v => v.LessonVideo).WithMany().HasForeignKey(v => v.LessonVideoId);
        });

        modelBuilder.Entity<VideoPlaybackSession>(e =>
        {
            e.Property(s => s.HasRegisteredView).HasDefaultValue(false);
            e.Property(s => s.LastProgressSequence).HasDefaultValue(0L);
            e.Property(s => s.IsSuperseded).HasDefaultValue(false);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => new { s.UserId, s.LessonVideoId, s.CreatedAt });
        });

        modelBuilder.Entity<ExtraWatchRequest>(e =>
        {
            e.ToTable("ExtraWatchRequests");
            e.HasKey(x => x.Id);
            e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.LessonVideoId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.HasOne(x => x.LessonVideo).WithMany().HasForeignKey(x => x.LessonVideoId);
        });

        // LessonProgress
        modelBuilder.Entity<LessonProgress>(e =>
        {
            e.ToTable("lesson_progress");
            e.HasKey(l => l.Id);
            e.HasIndex(l => new { l.UserId, l.LessonId }).IsUnique();
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId);
            e.HasOne(l => l.Lesson).WithMany().HasForeignKey(l => l.LessonId);
        });

        modelBuilder.Entity<VideoOverride>(e =>
        {
            e.ToTable("video_overrides");
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.UserId);
            e.HasIndex(o => o.LessonVideoId);
            e.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId);
            e.HasOne(o => o.LessonVideo).WithMany().HasForeignKey(o => o.LessonVideoId);
            e.HasOne(o => o.PerformedByUser).WithMany().HasForeignKey(o => o.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // Exam
        modelBuilder.Entity<Exam>(e =>
        {
            e.ToTable("exams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.PassingScore).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalScore).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.CreatedByTeacher).WithMany(t => t.Exams).HasForeignKey(x => x.CreatedByTeacherId);
            e.HasOne(x => x.LessonVideo)
             .WithMany()
             .HasForeignKey(x => x.LessonVideoId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // QuestionBankItem
        modelBuilder.Entity<QuestionBankItem>(e =>
        {
            e.ToTable("question_bank_items");
            e.HasKey(q => q.Id);
            e.Property(q => q.Text).IsRequired();
            e.Property(q => q.ImageUrl).HasMaxLength(500);
            e.Property(q => q.DefaultPoints).HasColumnType("decimal(18,2)");
            e.Property(q => q.Tags).HasMaxLength(500);
            e.HasOne(q => q.CreatedByTeacher).WithMany(t => t.QuestionBankItems).HasForeignKey(q => q.CreatedByTeacherId);
            e.HasOne(q => q.Subject).WithMany(s => s.QuestionBankItems).HasForeignKey(q => q.SubjectId);

            e.HasDiscriminator(q => q.Type)
             .HasValue<QuestionBankItem>(NaderGorge.Domain.Entities.QuestionType.MCQ)
             .HasValue<EssayQuestion>(NaderGorge.Domain.Entities.QuestionType.Essay)
             .HasValue<FindTheMistakeQuestion>(NaderGorge.Domain.Entities.QuestionType.FindTheMistake)
             .IsComplete(false);
        });

        // QuestionOption
        modelBuilder.Entity<QuestionOption>(e =>
        {
            e.ToTable("question_options");
            e.HasKey(o => o.Id);
            e.Property(o => o.Text).IsRequired();
            e.HasOne(o => o.Question).WithMany(q => q.Options).HasForeignKey(o => o.QuestionBankItemId);
        });

        // ExamQuestion (Junction)
        modelBuilder.Entity<ExamQuestion>(e =>
        {
            e.ToTable("exam_questions");
            e.HasKey(eq => eq.Id);
            e.HasIndex(eq => new { eq.ExamId, eq.QuestionBankItemId }).IsUnique();
            e.Property(eq => eq.Points).HasColumnType("decimal(18,2)");
            e.HasOne(eq => eq.Exam).WithMany(x => x.ExamQuestions).HasForeignKey(eq => eq.ExamId);
            e.HasOne(eq => eq.Question).WithMany().HasForeignKey(eq => eq.QuestionBankItemId);
        });

        // StudentExamAttempt
        modelBuilder.Entity<StudentExamAttempt>(e =>
        {
            e.ToTable("student_exam_attempts");
            e.HasKey(a => a.Id);
            e.Property(a => a.ScoreAchieved).HasColumnType("decimal(18,2)");
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            e.HasOne(a => a.Exam).WithMany(x => x.Attempts).HasForeignKey(a => a.ExamId);
        });

        // StudentAnswer
        modelBuilder.Entity<StudentAnswer>(e =>
        {
            e.ToTable("student_answers");
            e.HasKey(sa => sa.Id);
            e.HasIndex(sa => new { sa.StudentExamAttemptId, sa.ExamQuestionId }).IsUnique();
            e.Property(sa => sa.PointsAwarded).HasColumnType("decimal(18,2)");
            e.Property(sa => sa.SubmittedText).HasMaxLength(2000);
            e.HasOne(sa => sa.Attempt).WithMany(a => a.Answers).HasForeignKey(sa => sa.StudentExamAttemptId);
            e.HasOne(sa => sa.ExamQuestion).WithMany().HasForeignKey(sa => sa.ExamQuestionId);
            e.HasOne(sa => sa.SelectedOption)
                .WithMany()
                .HasForeignKey(sa => sa.SelectedOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // EssaySubmission
        modelBuilder.Entity<EssaySubmission>(e =>
        {
            e.ToTable("essay_submissions");
            e.HasKey(es => es.Id);
            e.Property(es => es.AiInitialScore).HasColumnType("decimal(18,2)");
            e.Property(es => es.TeacherFinalScore).HasColumnType("decimal(18,2)");
            e.Property(es => es.AudioUrl).HasMaxLength(2000);
            e.Property(es => es.Status).HasConversion<int>();
            e.HasOne(es => es.Student).WithMany().HasForeignKey(es => es.StudentId);
            e.HasOne(es => es.Question).WithMany().HasForeignKey(es => es.QuestionId);
            e.HasOne(es => es.Attempt).WithMany().HasForeignKey(es => es.StudentExamAttemptId);
            e.HasOne(es => es.GradedByTeacher).WithMany(t => t.EssaySubmissions).HasForeignKey(es => es.GradedByTeacherId);
        });

        // Phase 2

        modelBuilder.Entity<Homework>(e =>
        {
            e.ToTable("homeworks");
            e.HasKey(h => h.Id);
            e.Property(h => h.Title).HasMaxLength(255).IsRequired();
            e.Property(h => h.PassingScoreThreshold).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<HomeworkQuestion>(e =>
        {
            e.ToTable("homework_questions");
            e.HasKey(q => q.Id);
            e.Property(q => q.ImageUrl).HasMaxLength(500);
            e.HasOne(q => q.Homework).WithMany(h => h.Questions).HasForeignKey(q => q.HomeworkId);
        });

        modelBuilder.Entity<HomeworkSubmission>(e =>
        {
            e.ToTable("homework_submissions");
            e.HasKey(s => s.Id);
            e.Property(s => s.OverallScore).HasColumnType("decimal(18,2)");
            e.HasOne(s => s.Homework).WithMany(h => h.Submissions).HasForeignKey(s => s.HomeworkId);
            e.HasOne(s => s.Student).WithMany().HasForeignKey(s => s.StudentId);
            e.HasOne(s => s.AssistantReviewer).WithMany().HasForeignKey(s => s.AssistantReviewerId);
            e.HasIndex(s => new { s.HomeworkId, s.StudentId }).IsUnique();
        });

        modelBuilder.Entity<HomeworkAnswer>(e =>
        {
            e.ToTable("homework_answers");
            e.HasKey(a => a.Id);
            e.HasOne(a => a.Submission).WithMany(s => s.Answers).HasForeignKey(a => a.HomeworkSubmissionId);
            e.HasOne(a => a.Question).WithMany().HasForeignKey(a => a.QuestionId);
        });

        modelBuilder.Entity<StudentGamification>(e =>
        {
            e.ToTable("student_gamifications");
            e.HasKey(s => s.StudentId); // PK is StudentId
            e.HasOne(s => s.Student).WithOne().HasForeignKey<StudentGamification>(s => s.StudentId);
        });

        modelBuilder.Entity<GamificationActionLog>(e =>
        {
            e.ToTable("gamification_action_logs");
            e.HasKey(l => l.Id);
            e.HasOne(l => l.Student).WithMany().HasForeignKey(l => l.StudentId);
        });

        modelBuilder.Entity<StudentBadge>(e =>
        {
            e.ToTable("student_badges");
            e.HasKey(b => b.Id);
            e.HasOne(b => b.Student).WithMany().HasForeignKey(b => b.StudentId);
        });

        modelBuilder.Entity<StudentStatusTracker>(e =>
        {
            e.ToTable("student_status_trackers");
            e.HasKey(t => t.StudentId); // PK is StudentId
            e.HasOne(t => t.Student).WithOne().HasForeignKey<StudentStatusTracker>(t => t.StudentId);
        });

        modelBuilder.Entity<WarningEvent>(e =>
        {
            e.ToTable("warning_events");
            e.HasKey(w => w.Id);
            e.HasOne(w => w.Student).WithMany().HasForeignKey(w => w.StudentId);
            e.HasOne(w => w.ResolvedByAssistant).WithMany().HasForeignKey(w => w.ResolvedByAssistantId);
            e.Property(w => w.OccurrenceKey).HasMaxLength(200);
            e.HasIndex(w => w.OccurrenceKey).IsUnique();
        });

        modelBuilder.Entity<AssistantTaskQueue>(e =>
        {
            e.ToTable("assistant_tasks");
            e.HasKey(t => t.Id);
            e.HasOne(t => t.Student).WithMany().HasForeignKey(t => t.StudentId);
            e.HasOne(t => t.AssignedAssistant).WithMany().HasForeignKey(t => t.AssignedAssistantId);
        });

        modelBuilder.Entity<NotificationEvent>(e =>
        {
            e.ToTable("notification_events");
            e.HasKey(n => n.Id);
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId);
        });

        // ParentDeviceToken
        modelBuilder.Entity<ParentDeviceToken>(e =>
        {
            e.ToTable("ParentDeviceTokens");
            e.HasKey(t => t.Id);
            e.HasOne(t => t.Student)
             .WithMany()
             .HasForeignKey(t => t.StudentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(t => t.DeviceToken).IsRequired().HasMaxLength(500);
            e.Property(t => t.Platform).IsRequired().HasMaxLength(50);
            e.HasIndex(t => new { t.StudentId, t.DeviceToken }).IsUnique();
        });

        // Phase 3: Term
        modelBuilder.Entity<Term>(e =>
        {
            e.ToTable("terms");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(200).IsRequired();
            e.Property(t => t.ImageUrl).HasMaxLength(500);
            e.HasOne(t => t.Package).WithMany(p => p.Terms).HasForeignKey(t => t.PackageId);
        });

        // Phase 3: StudentBalance
        modelBuilder.Entity<StudentBalance>(e =>
        {
            e.ToTable("student_balances");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.CurrentBalance).HasColumnType("decimal(18,2)");
            e.HasOne(s => s.User).WithOne(u => u.StudentBalance).HasForeignKey<StudentBalance>(s => s.UserId);
        });

        // Phase 3: BalanceTransaction
        modelBuilder.Entity<BalanceTransaction>(e =>
        {
            e.ToTable("balance_transactions");
            e.HasKey(b => b.Id);
            e.Property(b => b.Amount).HasColumnType("decimal(18,2)");
            e.Property(b => b.BalanceAfter).HasColumnType("decimal(18,2)");
            e.Property(b => b.TransactionType).HasMaxLength(50).IsRequired();
            e.Property(b => b.Description).HasMaxLength(500).IsRequired();
            e.HasOne(b => b.StudentBalance).WithMany(s => s.Transactions).HasForeignKey(b => b.StudentBalanceId);
            e.HasOne(b => b.PerformedByUser).WithMany().HasForeignKey(b => b.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        // Phase 3: CodeVideoTarget
        modelBuilder.Entity<CodeVideoTarget>(e =>
        {
            e.ToTable("code_video_targets");
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.CodeGroupId, c.LessonVideoId }).IsUnique();
            e.HasOne(c => c.CodeGroup).WithMany(g => g.CodeVideoTargets).HasForeignKey(c => c.CodeGroupId);
            e.HasOne(c => c.LessonVideo).WithMany().HasForeignKey(c => c.LessonVideoId);
        });

        // Custom Forms
        modelBuilder.Entity<CustomForm>(e =>
        {
            e.ToTable("custom_forms");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.VisitCount).HasDefaultValue(0);
            e.Property(x => x.FieldsJson).IsRequired();
        });

        modelBuilder.Entity<FormSubmission>(e =>
        {
            e.ToTable("form_submissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.AdminNotes).HasMaxLength(2000);
            e.Property(x => x.SubmittedDataJson).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.CustomForm)
             .WithMany(f => f.Submissions)
             .HasForeignKey(x => x.CustomFormId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // EmployeeProfile
        modelBuilder.Entity<EmployeeProfile>(e =>
        {
            e.ToTable("employee_profiles");
            e.HasKey(ep => ep.Id);
            e.HasIndex(ep => ep.UserId).IsUnique();
            e.HasOne(ep => ep.User)
             .WithOne(u => u.EmployeeProfile)
             .HasForeignKey<EmployeeProfile>(ep => ep.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(ep => ep.BasicSalary).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ep => ep.StandardStartTime).IsRequired();
            e.Property(ep => ep.TargetDailyHours).IsRequired();
        });

        // AttendanceLog
        modelBuilder.Entity<AttendanceLog>(e =>
        {
            e.ToTable("attendance_logs");
            e.HasKey(al => al.Id);
            e.HasIndex(al => al.EmployeeId);
            e.HasIndex(al => al.Date);
            e.HasOne(al => al.Employee)
             .WithMany()
             .HasForeignKey(al => al.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.Property(al => al.Status).HasConversion<int>();
            e.Property(al => al.IpAddress).HasMaxLength(45);
            e.Property(al => al.UserAgent).HasMaxLength(500);
        });

        // EmployeeVacation
        modelBuilder.Entity<EmployeeVacation>(e =>
        {
            e.ToTable("employee_vacations");
            e.HasKey(ev => ev.Id);
            e.HasIndex(ev => ev.EmployeeId);
            e.HasOne(ev => ev.Employee)
             .WithMany()
             .HasForeignKey(ev => ev.EmployeeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ev => ev.HandledByUser)
             .WithMany()
             .HasForeignKey(ev => ev.HandledBy)
             .OnDelete(DeleteBehavior.SetNull);
            e.Property(ev => ev.Status).HasConversion<int>();
            e.Property(ev => ev.Reason).HasMaxLength(2000).IsRequired();
        });

        // TaskItem
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("task_items");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(255).IsRequired();
            e.Property(t => t.Description).HasMaxLength(4000);
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.Priority).HasConversion<int>();
            e.HasOne(t => t.Assignee)
             .WithMany()
             .HasForeignKey(t => t.AssigneeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.CreatedBy)
             .WithMany()
             .HasForeignKey(t => t.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.ApprovedBy)
             .WithMany()
             .HasForeignKey(t => t.ApprovedById)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.MediaPipeline)
             .WithMany(mp => mp.Tasks)
             .HasForeignKey(t => t.MediaPipelineId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // TaskComment
        modelBuilder.Entity<TaskComment>(e =>
        {
            e.ToTable("task_comments");
            e.HasKey(c => c.Id);
            e.Property(c => c.Content).HasMaxLength(4000).IsRequired();
            e.Property(c => c.AttachmentUrl).HasMaxLength(2048);
            e.HasOne(c => c.Task)
             .WithMany(t => t.Comments)
             .HasForeignKey(c => c.TaskId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User)
             .WithMany()
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ChatRoom
        modelBuilder.Entity<ChatRoom>(e =>
        {
            e.ToTable("chat_rooms");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Type).HasConversion<int>();
            e.HasOne(r => r.TaskItem)
             .WithMany()
             .HasForeignKey(r => r.TaskItemId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.CreatedByUser)
             .WithMany()
             .HasForeignKey(r => r.CreatedByUserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ChatParticipant
        modelBuilder.Entity<ChatParticipant>(e =>
        {
            e.ToTable("chat_participants");
            e.HasKey(p => new { p.ChatRoomId, p.UserId });
            e.HasOne(p => p.ChatRoom)
             .WithMany(r => r.ChatParticipants)
             .HasForeignKey(p => p.ChatRoomId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User)
             .WithMany()
             .HasForeignKey(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.LastReadMessage)
             .WithMany()
             .HasForeignKey(p => p.LastReadMessageId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ChatMessage
        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("chat_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.Content).HasMaxLength(4000).IsRequired();
            e.Property(m => m.Type).HasConversion<int>();
            e.Property(m => m.MediaUrl).HasMaxLength(2048);
            e.Property(m => m.MediaMetadata).HasMaxLength(4000);
            e.HasOne(m => m.ChatRoom)
             .WithMany(r => r.ChatMessages)
             .HasForeignKey(m => m.ChatRoomId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.SenderUser)
             .WithMany()
             .HasForeignKey(m => m.SenderUserId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(m => m.ChatRoomId);
            e.HasIndex(m => m.CreatedAt);
        });

        // ChatMessageReadState
        modelBuilder.Entity<ChatMessageReadState>(e =>
        {
            e.ToTable("chat_message_read_states");
            e.HasKey(rs => new { rs.MessageId, rs.UserId });
            e.HasOne(rs => rs.Message)
             .WithMany()
             .HasForeignKey(rs => rs.MessageId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rs => rs.User)
             .WithMany()
             .HasForeignKey(rs => rs.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureLiveSupport(modelBuilder);

        // CrmStudentStatus
        modelBuilder.Entity<CrmStudentStatus>(e =>
        {
            e.ToTable("crm_student_statuses");
            e.HasKey(s => s.StudentId);
            e.Property(s => s.Status).HasConversion<int>();
            e.Property(s => s.Priority).HasConversion<int>();
            e.Property(s => s.Notes).HasMaxLength(4000);
            
            e.HasOne(s => s.Student)
             .WithOne()
             .HasForeignKey<CrmStudentStatus>(s => s.StudentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.AssignedAgent)
             .WithMany()
             .HasForeignKey(s => s.AssignedAgentId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(s => s.AssignedAgentId);
            e.HasIndex(s => s.NextFollowUpDate);
        });

        // CrmCallLog
        modelBuilder.Entity<CrmCallLog>(e =>
        {
            e.ToTable("crm_call_logs");
            e.HasKey(l => l.Id);
            e.Property(l => l.Notes).HasMaxLength(4000);
            e.Property(l => l.Outcome).HasConversion<int>();
            
            e.HasOne(l => l.Student)
             .WithMany()
             .HasForeignKey(l => l.StudentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(l => l.Agent)
             .WithMany()
             .HasForeignKey(l => l.AgentId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(l => l.StudentId);
            e.HasIndex(l => l.CallDate);
        });

        // MediaProductionPipeline
        modelBuilder.Entity<MediaProductionPipeline>(e =>
        {
            e.ToTable("media_production_pipelines");
            e.HasKey(mp => mp.Id);
            e.Property(mp => mp.Title).HasMaxLength(250).IsRequired();
            e.Property(mp => mp.Description).HasMaxLength(2000);
            e.Property(mp => mp.AssetFolderUrl).HasMaxLength(2000);
            e.Property(mp => mp.Stage).HasConversion<int>();
            e.HasOne(mp => mp.AssignedAgent)
             .WithMany()
             .HasForeignKey(mp => mp.AssignedAgentId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(mp => mp.AssignedAgentId);
            e.HasIndex(mp => mp.Stage);
        });

        // SocialMediaPlan
        modelBuilder.Entity<SocialMediaPlan>(e =>
        {
            e.ToTable("social_media_plans");
            e.HasKey(sm => sm.Id);
            e.Property(sm => sm.Title).HasMaxLength(250).IsRequired();
            e.Property(sm => sm.Description).HasMaxLength(2000);
            e.Property(sm => sm.Script).HasMaxLength(4000);
            e.Property(sm => sm.Platform).HasConversion<int>();
            e.Property(sm => sm.Status).HasConversion<int>();
            e.HasOne(sm => sm.MediaProductionPipeline)
             .WithMany(mp => mp.SocialMediaPlans)
             .HasForeignKey(sm => sm.MediaProductionPipelineId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(sm => sm.ScheduledDate);
            e.HasIndex(sm => sm.MediaProductionPipelineId);
        });

        // PayrollRecord
        modelBuilder.Entity<PayrollRecord>(e =>
        {
            e.ToTable("payroll_records");
            e.HasKey(pr => pr.Id);
            e.Property(pr => pr.BasicSalary).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(pr => pr.Status).HasConversion<int>();
            e.HasOne(pr => pr.EmployeeProfile)
             .WithMany()
             .HasForeignKey(pr => pr.EmployeeProfileId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pr => pr.ApprovedByUser)
             .WithMany()
             .HasForeignKey(pr => pr.ApprovedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(pr => new { pr.EmployeeProfileId, pr.Month, pr.Year }).IsUnique();
        });

        // PayrollAdjustment
        modelBuilder.Entity<PayrollAdjustment>(e =>
        {
            e.ToTable("payroll_adjustments");
            e.HasKey(pa => pa.Id);
            e.Property(pa => pa.Amount).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(pa => pa.Type).HasConversion<int>();
            e.Property(pa => pa.Reason).HasMaxLength(2000).IsRequired();
            e.HasOne(pa => pa.PayrollRecord)
             .WithMany(pr => pr.Adjustments)
             .HasForeignKey(pa => pa.PayrollRecordId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // TeacherAccount
        modelBuilder.Entity<TeacherAccount>(e =>
        {
            e.ToTable("teacher_accounts");
            e.HasKey(ta => ta.Id);
            e.Property(ta => ta.TotalEarnings).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ta => ta.CurrentBalance).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ta => ta.CommissionRate).HasColumnType("decimal(18,2)").IsRequired();
            e.HasOne(ta => ta.Teacher)
             .WithOne()
             .HasForeignKey<TeacherAccount>(ta => ta.TeacherId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(ta => ta.TeacherId).IsUnique();
        });

        // TeacherPayout
        modelBuilder.Entity<TeacherPayout>(e =>
        {
            e.ToTable("teacher_payouts");
            e.HasKey(tp => tp.Id);
            e.Property(tp => tp.Amount).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(tp => tp.Status).HasConversion<int>();
            e.Property(tp => tp.RejectionReason).HasMaxLength(2000);
            e.HasOne(tp => tp.Teacher)
             .WithMany()
             .HasForeignKey(tp => tp.TeacherId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(tp => tp.HandledByUser)
             .WithMany()
             .HasForeignKey(tp => tp.HandledByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(tp => tp.TeacherId);
            e.HasIndex(tp => tp.Status);
        });

        // AccessCodeActivationLog
        modelBuilder.Entity<AccessCodeActivationLog>(e =>
        {
            e.ToTable("access_code_activation_logs");
            e.HasKey(al => al.Id);
            e.Property(al => al.Price).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(al => al.CommissionRate).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(al => al.CommissionEarned).HasColumnType("decimal(18,2)").IsRequired();
            e.HasOne(al => al.AccessCode)
             .WithMany()
             .HasForeignKey(al => al.AccessCodeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(al => al.Student)
             .WithMany()
             .HasForeignKey(al => al.StudentId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(al => al.Package)
             .WithMany()
             .HasForeignKey(al => al.PackageId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(al => al.Teacher)
             .WithMany()
             .HasForeignKey(al => al.TeacherId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(al => al.AccessCodeId).IsUnique();
            e.HasIndex(al => al.TeacherId);
            e.HasIndex(al => al.StudentId);
        });

        // OutboxEvent
        modelBuilder.Entity<OutboxEvent>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(o => o.Id);
            e.Property(o => o.Type).HasMaxLength(100).IsRequired();
            e.Property(o => o.PayloadJson).IsRequired();
            e.Property(o => o.TargetGroup).HasMaxLength(150);
            e.Property(o => o.TargetUserId).HasMaxLength(150);
            e.Property(o => o.LastError).HasMaxLength(4000);
            e.Property(o => o.IsDeadLetter).HasDefaultValue(false);
            
            e.HasIndex(o => new { o.ProcessedAt, o.CreatedAt });
        });

        // WebVitalsMetric
        modelBuilder.Entity<WebVitalsMetric>(e =>
        {
            e.ToTable("web_vitals_metrics");
            e.HasKey(m => m.Id);
            e.Property(m => m.MetricName).HasMaxLength(32).IsRequired();
            e.Property(m => m.Rating).HasMaxLength(32).IsRequired();
            e.Property(m => m.PageUrl).HasMaxLength(512).IsRequired();
            e.Property(m => m.UserAgent).HasMaxLength(512).IsRequired();

            e.HasIndex(m => m.MetricName);
            e.HasIndex(m => m.CreatedAt);
        });
    }

    private static void ConfigureLiveSupport(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveSupportConversation>(e =>
        {
            e.ToTable("live_support_conversations", table =>
                table.HasCheckConstraint("CK_live_support_conversation_identity", "(\"ParticipantType\" = 0 AND \"StudentUserId\" IS NOT NULL AND \"GuestSessionId\" IS NULL) OR (\"ParticipantType\" = 1 AND \"GuestSessionId\" IS NOT NULL AND \"StudentUserId\" IS NULL)"));
            e.Property(x => x.ParticipantType).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.CloseReason).HasMaxLength(500);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.StudentUserId).IsUnique().HasFilter("\"StudentUserId\" IS NOT NULL AND \"Status\" IN (0, 1, 2)");
            e.HasIndex(x => x.GuestSessionId).IsUnique().HasFilter("\"GuestSessionId\" IS NOT NULL AND \"Status\" IN (0, 1, 2)");
            e.HasIndex(x => new { x.Status, x.QueuedAt, x.Id });
            e.HasIndex(x => new { x.CurrentOwnerUserId, x.Status });
            e.HasIndex(x => new { x.LinkedStudentUserId, x.CreatedAt });
            e.HasIndex(x => x.LastMessageAt);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportGuestSession>().WithMany().HasForeignKey(x => x.GuestSessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.LinkedStudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.PreviousConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CurrentOwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportGuestSession>(e =>
        {
            e.ToTable("live_support_guest_sessions");
            e.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(x => x.SecurityStampHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.CreatedIpHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.UserAgentSummary).HasMaxLength(300);
            e.HasIndex(x => new { x.PhoneNumber, x.CreatedAt });
            e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => x.RevokedAt);
        });

        modelBuilder.Entity<LiveSupportStaffConfig>(e =>
        {
            e.ToTable("live_support_staff_configs", table =>
                table.HasCheckConstraint("CK_live_support_staff_capacity", "\"MaxActiveConversations\" BETWEEN 1 AND 50"));
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ConfiguredByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportScheduleWindow>(e =>
        {
            e.ToTable("live_support_schedule_windows", table =>
            {
                table.HasCheckConstraint("CK_live_support_schedule_day", "\"DayOfWeek\" BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_live_support_schedule_time", "\"StartLocalTime\" < \"EndLocalTime\"");
            });
            e.HasIndex(x => new { x.StaffConfigId, x.DayOfWeek, x.StartLocalTime, x.EndLocalTime }).IsUnique();
            e.HasOne<LiveSupportStaffConfig>().WithMany().HasForeignKey(x => x.StaffConfigId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiveSupportQueueEntry>(e =>
        {
            e.ToTable("live_support_queue_entries");
            e.Property(x => x.DequeueReason).HasMaxLength(100);
            e.HasIndex(x => x.ConversationId).IsUnique().HasFilter("\"DequeuedAt\" IS NULL");
            e.HasIndex(x => new { x.DequeuedAt, x.EnteredAt, x.Sequence });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAssignment>(e =>
        {
            e.ToTable("live_support_assignments");
            e.Property(x => x.EndReason).HasConversion<int>();
            e.Property(x => x.TransferReason).HasMaxLength(500);
            e.HasIndex(x => x.ConversationId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
            e.HasIndex(x => new { x.StaffUserId, x.EndedAt, x.StartedAt });
            e.HasIndex(x => new { x.ConversationId, x.AssignmentSequence }).IsUnique();
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportMessage>(e =>
        {
            e.ToTable("live_support_messages");
            e.Property(x => x.SenderType).HasConversion<int>();
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.ClientMessageId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Content).HasMaxLength(4000);
            e.HasIndex(x => new { x.ConversationId, x.ClientMessageId }).IsUnique();
            e.HasIndex(x => new { x.ConversationId, x.SentAt, x.Id });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportGuestSession>().WithMany().HasForeignKey(x => x.SenderGuestSessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAttachment>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAttachment>(e =>
        {
            e.ToTable("live_support_attachments");
            e.Property(x => x.StoragePath).HasMaxLength(2048).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.UploadedByIdentity).HasMaxLength(150).IsRequired();
        });

        modelBuilder.Entity<LiveSupportStudentLinkHistory>(e =>
        {
            e.ToTable("live_support_student_link_history");
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.ConversationId, x.ChangedAt });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PreviousStudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.NewStudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportEvent>(e =>
        {
            e.ToTable("live_support_events");
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.RelatedEntityType).HasMaxLength(100);
            e.Property(x => x.SafeMetadataJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ConversationId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.Type, x.OccurredAt });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportGuestSession>().WithMany().HasForeignKey(x => x.ActorGuestSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportActionExecution>(e =>
        {
            e.ToTable("live_support_action_executions");
            e.Property(x => x.ActionKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.SafeRequestJson).HasColumnType("jsonb");
            e.Property(x => x.SafeResultJson).HasColumnType("jsonb");
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.HasIndex(x => new { x.StaffUserId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.ConversationId, x.StartedAt });
            e.HasIndex(x => new { x.StudentUserId, x.StartedAt });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StaffUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AuditLog>().WithMany().HasForeignKey(x => x.AuditLogId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportRating>(e =>
        {
            e.ToTable("live_support_ratings", table =>
                table.HasCheckConstraint("CK_live_support_rating_stars", "\"Stars\" BETWEEN 1 AND 5"));
            e.Property(x => x.Comment).HasMaxLength(1000);
            e.HasIndex(x => x.ConversationId).IsUnique();
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportGuestSession>().WithMany().HasForeignKey(x => x.SubmittedByGuestSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureLiveSupportAI(modelBuilder);
    }

    private static void ConfigureLiveSupportAI(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LiveSupportAIPolicyVersion>(e =>
        {
            e.ToTable("live_support_ai_policy_versions", table =>
            {
                table.HasCheckConstraint("CK_live_support_ai_policy_verification", "\"VerificationRequiredCorrect\" >= 1 AND \"VerificationMaxAttempts\" BETWEEN 1 AND 10");
                table.HasCheckConstraint("CK_live_support_ai_policy_action_expiry", "\"PendingActionExpirySeconds\" BETWEEN 30 AND 900");
                table.HasCheckConstraint("CK_live_support_ai_policy_inactivity", "\"InactivityMinutes\" BETWEEN 5 AND 1440 AND \"InactivityWarningGraceSeconds\" BETWEEN 30 AND 600");
            });
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.SystemInstructions).HasMaxLength(20000).IsRequired();
            e.Property(x => x.ReadableDataKeysJson).HasColumnType("jsonb");
            e.Property(x => x.ActionKeysJson).HasColumnType("jsonb");
            e.Property(x => x.LookupKeysJson).HasColumnType("jsonb");
            e.Property(x => x.VerificationQuestionKeysJson).HasColumnType("jsonb");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.VersionNumber).IsUnique();
            e.HasIndex(x => x.IsEnabled).IsUnique().HasFilter("\"Status\" = 1 AND \"IsEnabled\" = TRUE");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIKnowledgeEntry>(e =>
        {
            e.ToTable("live_support_ai_knowledge_entries");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIKnowledgeRevision>(e =>
        {
            e.ToTable("live_support_ai_knowledge_revisions");
            e.Property(x => x.Content).HasMaxLength(50000).IsRequired();
            e.Property(x => x.SourceLabel).HasMaxLength(300);
            e.Property(x => x.SearchText).HasMaxLength(50000).IsRequired();
            e.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.EntryId, x.RevisionNumber }).IsUnique();
            e.HasOne<LiveSupportAIKnowledgeEntry>().WithMany().HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIPolicyKnowledgeRevision>(e =>
        {
            e.ToTable("live_support_ai_policy_knowledge_revisions");
            e.HasKey(x => new { x.PolicyVersionId, x.KnowledgeRevisionId });
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAIKnowledgeRevision>().WithMany().HasForeignKey(x => x.KnowledgeRevisionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIConversationState>(e =>
        {
            e.ToTable("live_support_ai_conversation_states");
            e.HasKey(x => x.ConversationId);
            e.Property(x => x.Mode).HasConversion<int>();
            e.Property(x => x.HandoffReasonCode).HasMaxLength(100);
            e.Property(x => x.HandoffSafeSummary).HasMaxLength(2000);
            e.Property(x => x.ResolutionCode).HasMaxLength(100);
            e.Property(x => x.SafeSummaryJson).HasColumnType("jsonb");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.Mode, x.AutoCloseAt });
            e.HasIndex(x => new { x.Mode, x.LastParticipantActivityAt });
            e.HasOne<LiveSupportConversation>().WithOne().HasForeignKey<LiveSupportAIConversationState>(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.VerifiedStudentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAITurn>(e =>
        {
            e.ToTable("live_support_ai_turns");
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DecisionType).HasConversion<int>();
            e.Property(x => x.CallbackStatus).HasConversion<int>();
            e.Property(x => x.ContextCategoryKeysJson).HasColumnType("jsonb");
            e.Property(x => x.KnowledgeRevisionIdsJson).HasColumnType("jsonb");
            e.Property(x => x.Provider).HasMaxLength(100);
            e.Property(x => x.Model).HasMaxLength(150);
            e.Property(x => x.ProviderResponseId).HasMaxLength(200);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.SafeFailureDetail).HasMaxLength(1000);
            e.Property(x => x.DecisionHash).HasMaxLength(64);
            e.Property(x => x.LastSafeCallbackErrorCode).HasMaxLength(100);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.SourceMessageId).IsUnique();
            e.HasIndex(x => x.OutputMessageId).IsUnique().HasFilter("\"OutputMessageId\" IS NOT NULL");
            e.HasIndex(x => new { x.Status, x.QueuedAt });
            e.HasIndex(x => new { x.CallbackStatus, x.NextCallbackAttemptAt });
            e.HasIndex(x => new { x.ConversationId, x.QueuedAt });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportMessage>().WithMany().HasForeignKey(x => x.SourceMessageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportMessage>().WithMany().HasForeignKey(x => x.OutputMessageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIPendingAction>(e =>
        {
            e.ToTable("live_support_ai_pending_actions", table =>
            {
                table.HasCheckConstraint(
                    "CK_live_support_ai_pending_action_target",
                    "\"DecisionKind\" <> 0 OR (\"StudentUserId\" IS NOT NULL AND length(\"ActionKey\") > 0 AND length(\"PayloadHash\") > 0 AND length(\"StateFingerprint\") > 0 AND \"EncryptedPayload\" IS NOT NULL)");
            });
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DecisionKind).HasConversion<int>();
            e.Property(x => x.ActionKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.SafeProposalJson).HasColumnType("jsonb");
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.StateFingerprint).HasMaxLength(64).IsRequired();
            e.Property(x => x.ConfirmationNonceHash).HasMaxLength(64).IsRequired();
            e.Property(x => x.CallbackDecisionHash).HasMaxLength(64);
            e.Property(x => x.FailureCode).HasMaxLength(100);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => x.ActionExecutionId).IsUnique().HasFilter("\"ActionExecutionId\" IS NOT NULL");
            e.HasIndex(x => new { x.ConversationId, x.Status });
            e.HasIndex(x => new { x.Status, x.ExpiresAt });
            e.HasIndex(x => new { x.ConversationId, x.DecisionKind })
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAITurn>().WithMany().HasForeignKey(x => x.TurnId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportGuestSession>().WithMany().HasForeignKey(x => x.ConfirmedByGuestSessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportActionExecution>().WithMany().HasForeignKey(x => x.ActionExecutionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIVerificationPolicyQuestion>(e =>
        {
            e.ToTable("live_support_ai_verification_policy_questions");
            e.Property(x => x.QuestionKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.PromptText).HasMaxLength(300).IsRequired();
            e.Property(x => x.SourceFieldKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.ComparisonMode).HasConversion<int>();
            e.HasIndex(x => new { x.PolicyVersionId, x.Order }).IsUnique();
            e.HasIndex(x => new { x.PolicyVersionId, x.QuestionKey }).IsUnique();
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIVerificationSession>(e =>
        {
            e.ToTable("live_support_ai_verification_sessions", table =>
            {
                table.HasCheckConstraint(
                    "CK_live_support_ai_verification_counts",
                    "\"CorrectCount\" >= 0 AND \"CorrectCount\" <= \"AttemptCount\" AND \"AttemptCount\" <= \"MaxAttempts\" AND \"CurrentQuestionIndex\" >= 0");
            });
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.LookupKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.LookupValueHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.SelectedQuestionKeysJson).HasColumnType("jsonb");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.ConversationId).IsUnique().HasFilter("\"Status\" IN (0, 1)");
            e.HasIndex(x => new { x.Status, x.ExpiresAt });
            e.HasOne<LiveSupportConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LiveSupportAIPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CandidateStudentUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiveSupportAIVerificationAttempt>(e =>
        {
            e.ToTable("live_support_ai_verification_attempts");
            e.Property(x => x.QuestionKeysJson).HasColumnType("jsonb");
            e.Property(x => x.OutcomeCodesJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.SessionId, x.AttemptNumber }).IsUnique();
            e.HasOne<LiveSupportAIVerificationSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        // DigitalWallet mapping
        modelBuilder.Entity<DigitalWallet>(e =>
        {
            e.ToTable("digital_wallets");
            e.HasKey(dw => dw.Id);
            e.HasIndex(dw => dw.PhoneNumber).IsUnique();
            e.HasIndex(dw => dw.PairingToken).IsUnique();
            e.Property(dw => dw.PhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(dw => dw.Label).HasMaxLength(100).IsRequired();
            e.Property(dw => dw.PairingToken).HasMaxLength(20).IsRequired();
            e.Property(dw => dw.DailyLimit).HasPrecision(18, 2);
            e.Property(dw => dw.MonthlyLimit).HasPrecision(18, 2);
            e.Property(dw => dw.CurrentBalance).HasPrecision(18, 2);
        });

        // RechargeRequest mapping
        modelBuilder.Entity<RechargeRequest>(e =>
        {
            e.ToTable("recharge_requests");
            e.HasKey(rr => rr.Id);
            
            e.HasOne(rr => rr.User)
                .WithMany()
                .HasForeignKey(rr => rr.UserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.HasOne(rr => rr.Wallet)
                .WithMany(w => w.RechargeRequests)
                .HasForeignKey(rr => rr.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.HasOne(rr => rr.ResolvedByUser)
                .WithMany()
                .HasForeignKey(rr => rr.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.HasOne(rr => rr.MatchedSmsLog)
                .WithOne(sms => sms.MatchedRechargeRequest)
                .HasForeignKey<RechargeRequest>(rr => rr.MatchedSmsLogId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.Property(rr => rr.Amount).HasPrecision(18, 2);
            e.Property(rr => rr.SenderPhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(rr => rr.ScreenshotUrl).HasMaxLength(1000);
            e.Property(rr => rr.RejectionReason).HasMaxLength(500);
        });

        // IncomingSmsLog mapping
        modelBuilder.Entity<IncomingSmsLog>(e =>
        {
            e.ToTable("incoming_sms_logs");
            e.HasKey(sms => sms.Id);
            
            e.HasOne(sms => sms.Wallet)
                .WithMany(w => w.IncomingSmsLogs)
                .HasForeignKey(sms => sms.WalletId)
                .OnDelete(DeleteBehavior.Restrict);
                
            e.HasIndex(sms => sms.DeduplicationHash).IsUnique();
            e.Property(sms => sms.Sender).HasMaxLength(100).IsRequired();
            e.Property(sms => sms.Body).HasMaxLength(1000).IsRequired();
            e.Property(sms => sms.DeduplicationHash).HasMaxLength(64).IsRequired();
            e.Property(sms => sms.ParsedAmount).HasPrecision(18, 2);
            e.Property(sms => sms.ParsedSenderPhone).HasMaxLength(20);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        // Intercept Added NotificationEvents to generate OutboxEvents
        var newNotifications = ChangeTracker.Entries<Domain.Entities.Notifications.NotificationEvent>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        foreach (var notification in newNotifications)
        {
            var outboxEvent = new OutboxEvent
            {
                Type = "NotificationCreated",
                TargetUserId = notification.UserId.ToString(),
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Body,
                    createdAt = notification.CreatedAt
                })
            };
            OutboxEvents.Add(outboxEvent);
        }

        if (Database.IsRelational() && StaffRealtimeChangeDetector.CreateEvent(ChangeTracker) is { } staffEvent)
        {
            OutboxEvents.Add(staffEvent);
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;
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
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageCodePageProfile> PackageCodePageProfiles => Set<PackageCodePageProfile>();
    public DbSet<ContentSection> ContentSections => Set<ContentSection>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
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

    public Task<StudentAnswer?> FindStudentAnswerAsync(
        Guid studentExamAttemptId,
        Guid examQuestionId,
        CancellationToken cancellationToken = default)
    {
        return StudentAnswers.FirstOrDefaultAsync(
            answer => answer.StudentExamAttemptId == studentExamAttemptId && answer.ExamQuestionId == examQuestionId,
            cancellationToken);
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

        // Role
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Name).IsUnique();
            e.Property(r => r.Name).HasMaxLength(50).IsRequired();
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
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
            e.HasOne(s => s.AccessCode).WithMany().HasForeignKey(s => s.AccessCodeId);
        });

        // Program
        modelBuilder.Entity<Program>(e =>
        {
            e.ToTable("programs");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
        });

        // Package
        modelBuilder.Entity<Package>(e =>
        {
            e.ToTable("packages");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).HasMaxLength(200).IsRequired();
            e.HasOne(p => p.Program).WithMany(pr => pr.Packages).HasForeignKey(p => p.ProgramId);
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

        // Exam
        modelBuilder.Entity<Exam>(e =>
        {
            e.ToTable("exams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.PassingScore).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalScore).HasColumnType("decimal(18,2)");
        });

        // QuestionBankItem
        modelBuilder.Entity<QuestionBankItem>(e =>
        {
            e.ToTable("question_bank_items");
            e.HasKey(q => q.Id);
            e.Property(q => q.Text).IsRequired();
            e.Property(q => q.DefaultPoints).HasColumnType("decimal(18,2)");
            e.Property(q => q.Tags).HasMaxLength(500);

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

        // Phase 3: Term
        modelBuilder.Entity<Term>(e =>
        {
            e.ToTable("terms");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(200).IsRequired();
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

    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

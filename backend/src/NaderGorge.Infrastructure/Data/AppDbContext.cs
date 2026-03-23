using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
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
    public DbSet<ContentSection> ContentSections => Set<ContentSection>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<LessonResource> LessonResources => Set<LessonResource>();
    
    // Tracking
    public DbSet<VideoWatchEvent> VideoWatchEvents => Set<VideoWatchEvent>();
    public DbSet<LessonProgress> LessonProgresses => Set<LessonProgress>();
    
    // Exams
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<QuestionBankItem> QuestionBankItems => Set<QuestionBankItem>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();
    public DbSet<StudentExamAttempt> StudentExamAttempts => Set<StudentExamAttempt>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();

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

        // ContentSection
        modelBuilder.Entity<ContentSection>(e =>
        {
            e.ToTable("content_sections");
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.HasOne(c => c.Package).WithMany(p => p.Sections).HasForeignKey(c => c.PackageId);
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

        // LessonResource
        modelBuilder.Entity<LessonResource>(e =>
        {
            e.ToTable("lesson_resources");
            e.HasKey(l => l.Id);
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.Lesson).WithMany(le => le.Resources).HasForeignKey(l => l.LessonId);
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
            e.HasOne(sa => sa.Attempt).WithMany(a => a.Answers).HasForeignKey(sa => sa.StudentExamAttemptId);
            e.HasOne(sa => sa.ExamQuestion).WithMany().HasForeignKey(sa => sa.ExamQuestionId);
            e.HasOne(sa => sa.SelectedOption).WithMany().HasForeignKey(sa => sa.SelectedOptionId);
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

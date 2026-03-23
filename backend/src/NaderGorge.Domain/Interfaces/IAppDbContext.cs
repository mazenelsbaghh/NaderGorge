using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;

namespace NaderGorge.Domain.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<Device> Devices { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<CodeGroup> CodeGroups { get; }
    DbSet<AccessCode> AccessCodes { get; }
    DbSet<StudentAccessGrant> StudentAccessGrants { get; }
    
    // Content
    DbSet<Program> Programs { get; }
    DbSet<Package> Packages { get; }
    DbSet<ContentSection> ContentSections { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonVideo> LessonVideos { get; }
    DbSet<LessonResource> LessonResources { get; }
    
    // Tracking
    DbSet<VideoWatchEvent> VideoWatchEvents { get; }
    DbSet<LessonProgress> LessonProgresses { get; }
    
    // Exams
    DbSet<Exam> Exams { get; }
    DbSet<QuestionBankItem> QuestionBankItems { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<ExamQuestion> ExamQuestions { get; }
    DbSet<StudentExamAttempt> StudentExamAttempts { get; }
    DbSet<StudentAnswer> StudentAnswers { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

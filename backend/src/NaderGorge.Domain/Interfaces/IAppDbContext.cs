using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;

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
    DbSet<Subject> Subjects { get; }
    DbSet<TeacherProfile> TeacherProfiles { get; }
    DbSet<TeacherSubject> TeacherSubjects { get; }
    DbSet<Package> Packages { get; }
    DbSet<PackageCodePageProfile> PackageCodePageProfiles { get; }
    DbSet<ContentSection> ContentSections { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonVideo> LessonVideos { get; }
    DbSet<BunnyVideoAsset> BunnyVideoAssets { get; }
    DbSet<BunnyUsageSnapshot> BunnyUsageSnapshots { get; }
    DbSet<VideoChapter> VideoChapters { get; }
    DbSet<LessonResource> LessonResources { get; }
    DbSet<LessonComment> LessonComments { get; }
    DbSet<CommunityPost> CommunityPosts { get; }
    DbSet<CommunityPostComment> CommunityPostComments { get; }
    DbSet<CommunityPostLike> CommunityPostLikes { get; }
    DbSet<CommunityPostPollOption> CommunityPostPollOptions { get; }
    DbSet<CommunityPostPollVote> CommunityPostPollVotes { get; }
    DbSet<TeacherPhoto> TeacherPhotos { get; }
    DbSet<CustomForm> CustomForms { get; }
    DbSet<FormSubmission> FormSubmissions { get; }

    // Phase 3
    DbSet<Term> Terms { get; }
    DbSet<StudentBalance> StudentBalances { get; }
    DbSet<BalanceTransaction> BalanceTransactions { get; }
    DbSet<CodeVideoTarget> CodeVideoTargets { get; }

    // Tracking
    DbSet<VideoWatchEvent> VideoWatchEvents { get; }
    DbSet<ExtraWatchRequest> ExtraWatchRequests { get; }
    DbSet<LessonProgress> LessonProgresses { get; }
    DbSet<VideoPlaybackSession> VideoPlaybackSessions { get; }
    DbSet<VideoOverride> VideoOverrides { get; }

    // Exams
    DbSet<Exam> Exams { get; }
    DbSet<QuestionBankItem> QuestionBankItems { get; }
    DbSet<QuestionOption> QuestionOptions { get; }
    DbSet<ExamQuestion> ExamQuestions { get; }
    DbSet<StudentExamAttempt> StudentExamAttempts { get; }
    DbSet<StudentAnswer> StudentAnswers { get; }
    DbSet<EssaySubmission> EssaySubmissions { get; }
    DbSet<PlatformSetting> PlatformSettings { get; }

    // Phase 2: Homework & Academic Ops
    DbSet<Homework> Homeworks { get; }
    DbSet<HomeworkQuestion> HomeworkQuestions { get; }
    DbSet<HomeworkSubmission> HomeworkSubmissions { get; }
    DbSet<HomeworkAnswer> HomeworkAnswers { get; }

    // Phase 2: Gamification
    DbSet<StudentGamification> StudentGamifications { get; }
    DbSet<GamificationActionLog> GamificationActionLogs { get; }
    DbSet<StudentBadge> StudentBadges { get; }

    // Phase 2: Student Tracking
    DbSet<StudentStatusTracker> StudentStatusTrackers { get; }
    DbSet<WarningEvent> WarningEvents { get; }

    // Phase 2: Assistant Ops
    DbSet<AssistantTaskQueue> AssistantTasks { get; }

    // Phase 2: Notifications
    DbSet<NotificationEvent> NotificationEvents { get; }

    // Student Notes
    DbSet<StudentNote> StudentNotes { get; }

    // Phase 2: HR Core
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    DbSet<AttendanceLog> AttendanceLogs { get; }
    DbSet<EmployeeVacation> EmployeeVacations { get; }

    DbSet<TaskItem> TaskItems { get; }
    DbSet<TaskComment> TaskComments { get; }

    // Phase 5: Internal Chat
    DbSet<ChatRoom> ChatRooms { get; }
    DbSet<ChatParticipant> ChatParticipants { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<ChatMessageReadState> ChatMessageReadStates { get; }

    // Phase 6: Call Center CRM
    DbSet<CrmStudentStatus> CrmStudentStatuses { get; }
    DbSet<CrmCallLog> CrmCallLogs { get; }

    // Phase 8: Media Production & Social Planner
    DbSet<MediaProductionPipeline> MediaProductionPipelines { get; }
    DbSet<SocialMediaPlan> SocialMediaPlans { get; }

    // Phase 9: Payroll & Teacher Finance
    DbSet<PayrollRecord> PayrollRecords { get; }
    DbSet<PayrollAdjustment> PayrollAdjustments { get; }
    DbSet<TeacherAccount> TeacherAccounts { get; }
    DbSet<TeacherPayout> TeacherPayouts { get; }
    DbSet<AccessCodeActivationLog> AccessCodeActivationLogs { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<WebVitalsMetric> WebVitalsMetrics { get; }

    Task<StudentAnswer?> FindStudentAnswerAsync(Guid studentExamAttemptId, Guid examQuestionId, CancellationToken cancellationToken = default);
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class;
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

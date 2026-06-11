using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;

namespace NaderGorge.Infrastructure.Data;

internal static class StaffRealtimeChangeDetector
{
    private static readonly IReadOnlyDictionary<Type, string[]> EntityScopes =
        new Dictionary<Type, string[]>
        {
            [typeof(User)] = ["users"],
            [typeof(Role)] = ["users", "settings"],
            [typeof(UserRole)] = ["users", "settings"],
            [typeof(StudentProfile)] = ["users"],
            [typeof(Device)] = ["users"],
            [typeof(TeacherProfile)] = ["users", "subjects"],
            [typeof(TeacherSubject)] = ["users", "subjects"],
            [typeof(TeacherPhoto)] = ["users"],
            [typeof(StudentNote)] = ["users"],
            [typeof(Subject)] = ["subjects", "content"],
            [typeof(Package)] = ["content"],
            [typeof(PackageCodePageProfile)] = ["content", "codes"],
            [typeof(Term)] = ["content"],
            [typeof(ContentSection)] = ["content"],
            [typeof(Lesson)] = ["content"],
            [typeof(LessonVideo)] = ["content", "ai"],
            [typeof(VideoChapter)] = ["content", "ai"],
            [typeof(LessonResource)] = ["content"],
            [typeof(LessonComment)] = ["comments"],
            [typeof(CommunityPost)] = ["community"],
            [typeof(CommunityPostComment)] = ["community"],
            [typeof(CustomForm)] = ["forms"],
            [typeof(FormSubmission)] = ["forms"],
            [typeof(CodeGroup)] = ["codes"],
            [typeof(AccessCode)] = ["codes"],
            [typeof(StudentAccessGrant)] = ["codes", "users"],
            [typeof(CodeVideoTarget)] = ["codes"],
            [typeof(AccessCodeActivationLog)] = ["codes", "finance"],
            [typeof(ExtraWatchRequest)] = ["watch-requests"],
            [typeof(VideoOverride)] = ["watch-requests", "users"],
            [typeof(Exam)] = ["assessments"],
            [typeof(QuestionBankItem)] = ["assessments"],
            [typeof(QuestionOption)] = ["assessments"],
            [typeof(ExamQuestion)] = ["assessments"],
            [typeof(StudentExamAttempt)] = ["assessments", "activity"],
            [typeof(StudentAnswer)] = ["assessments"],
            [typeof(EssaySubmission)] = ["assessments"],
            [typeof(Homework)] = ["assessments", "content"],
            [typeof(HomeworkQuestion)] = ["assessments"],
            [typeof(HomeworkSubmission)] = ["assessments", "activity"],
            [typeof(HomeworkAnswer)] = ["assessments"],
            [typeof(PlatformSetting)] = ["settings"],
            [typeof(AssistantTaskQueue)] = ["operations"],
            [typeof(TaskItem)] = ["operations"],
            [typeof(TaskComment)] = ["operations"],
            [typeof(EmployeeProfile)] = ["hr", "users"],
            [typeof(AttendanceLog)] = ["hr"],
            [typeof(EmployeeVacation)] = ["hr"],
            [typeof(CrmStudentStatus)] = ["crm"],
            [typeof(CrmCallLog)] = ["crm"],
            [typeof(MediaProductionPipeline)] = ["media"],
            [typeof(SocialMediaPlan)] = ["media"],
            [typeof(PayrollRecord)] = ["finance"],
            [typeof(PayrollAdjustment)] = ["finance"],
            [typeof(TeacherAccount)] = ["finance"],
            [typeof(TeacherPayout)] = ["finance"],
            [typeof(StudentBalance)] = ["balance", "finance"],
            [typeof(BalanceTransaction)] = ["balance", "finance"],
            [typeof(StudentGamification)] = ["gamification", "activity"],
            [typeof(GamificationActionLog)] = ["gamification", "activity"],
            [typeof(WarningEvent)] = ["activity", "reports"],
            [typeof(StudentStatusTracker)] = ["activity"],
            [typeof(NotificationEvent)] = ["notifications"],
            [typeof(AuditLog)] = ["reports"]
        };

    public static OutboxEvent? CreateEvent(ChangeTracker changeTracker)
    {
        var scopes = changeTracker.Entries()
            .Where(IsChangedEntity)
            .SelectMany(entry => EntityScopes.GetValueOrDefault(entry.Metadata.ClrType) ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scope => scope, StringComparer.Ordinal)
            .ToArray();

        return scopes.Length == 0 ? null : new OutboxEvent
        {
            Type = "StaffDataChanged",
            TargetGroup = "Role_Staff",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new { scopes })
        };
    }

    private static bool IsChangedEntity(EntityEntry entry)
    {
        return entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && EntityScopes.ContainsKey(entry.Metadata.ClrType);
    }
}

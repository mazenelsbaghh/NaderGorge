namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed record AdminAIStudentSnapshotOutput(
    bool Found,
    Guid? StudentId,
    string? DisplayName,
    string? PhoneEnding,
    IReadOnlyList<string> IncludedSections,
    AdminAIStudentProfileSection? Profile,
    AdminAIStudentContactSection? Contact,
    AdminAIStudentBalancesSection? Balances,
    AdminAIStudentSubscriptionsSection? Subscriptions,
    AdminAIStudentActivitySection? Activity,
    AdminAIStudentAssessmentsSection? Assessments,
    DateTime DataAsOf);

public sealed record AdminAIStudentProfileAccount(
    string StudentCode,
    string AccountStatus,
    DateTime CreatedAt,
    bool IsProfileComplete);

public sealed record AdminAIStudentProfilePersonal(
    DateTime DateOfBirth,
    string Gender,
    string Nationality);

public sealed record AdminAIStudentProfileAcademic(
    string EducationStage,
    string GradeLevel,
    string StudyTrack);

public sealed record AdminAIStudentProfileSchool(
    string SchoolName,
    string SchoolType);

public sealed record AdminAIStudentProfileSection(
    AdminAIStudentProfileAccount? Account,
    AdminAIStudentProfilePersonal? Personal,
    AdminAIStudentProfileAcademic? Academic,
    AdminAIStudentProfileSchool? School);

public sealed record AdminAIStudentOwnPhones(
    string PhoneNumber,
    string SecondaryPhoneNumber);

public sealed record AdminAIStudentGuardianPhones(
    string ParentPhoneNumber,
    string SecondaryParentPhoneNumber,
    string MotherPhoneNumber);

public sealed record AdminAIStudentLocation(
    string Governorate,
    string District,
    string Address);

public sealed record AdminAIStudentContactSection(
    AdminAIStudentOwnPhones? StudentPhones,
    AdminAIStudentGuardianPhones? GuardianPhones,
    AdminAIStudentLocation? Location);

public sealed record AdminAIStudentPromotionalBalance(
    Guid TeacherId,
    string TeacherName,
    decimal AvailableEgp,
    DateTime? NearestExpiryAt);

public sealed record AdminAIStudentBalanceTransaction(
    decimal AmountEgp,
    decimal BalanceAfterEgp,
    string TransactionType,
    DateTime CreatedAt);

public sealed record AdminAIStudentBalancesSection(
    decimal GeneralCashEgp,
    decimal GeneralPromotionalAvailableEgp,
    IReadOnlyList<AdminAIStudentPromotionalBalance> TeacherScopedBalances,
    int TeacherScopeCount,
    Guid? ContextTeacherId,
    string? ContextTeacherName,
    decimal? EligiblePromotionalForContextTeacherEgp,
    decimal? ContextualPurchasingPowerEgp,
    IReadOnlyList<AdminAIStudentBalanceTransaction> RecentGeneralTransactions,
    string BalanceRuleAr);

public sealed record AdminAIStudentSubscriptionTypeCount(string GrantType, int Count);

public sealed record AdminAIStudentSubscriptionItem(
    Guid GrantId,
    string GrantType,
    Guid? ContentId,
    string ContentName,
    Guid? TeacherId,
    string TeacherName,
    string Source,
    string EntitlementState,
    bool IsEffective,
    DateTime GrantedAt,
    DateTime? ExpiresAt,
    DateTime? CancelledAt);

public sealed record AdminAIStudentSubscriptionsSection(
    int TotalGrantCount,
    int ActiveGrantCount,
    int CancelledGrantCount,
    int ExpiredGrantCount,
    int ExhaustedGrantCount,
    int InactiveGrantCount,
    IReadOnlyList<AdminAIStudentSubscriptionTypeCount> GrantCountsByType,
    IReadOnlyList<AdminAIStudentSubscriptionItem> RecentEntitlements,
    AdminAIStudentTeacherEntitlement? ContextTeacherEntitlement,
    bool TeacherScopedBalanceDoesNotGrantAccess,
    string AccessRuleAr);

public sealed record AdminAIStudentTeacherEntitlement(
    Guid TeacherId,
    string TeacherName,
    bool HasEffectiveEntitlement,
    int EffectiveGrantCount,
    IReadOnlyList<AdminAIStudentSubscriptionTypeCount> EffectiveGrantCountsByType);

public sealed record AdminAIStudentWatchItem(
    Guid VideoId,
    string VideoTitle,
    string LessonTitle,
    string PackageName,
    Guid TeacherId,
    string TeacherName,
    int WatchCount,
    bool IsLocked,
    DateTime LastWatchedAt);

public sealed record AdminAIStudentWatchingActivity(
    int WatchedVideoCount,
    int TotalWatchedSeconds,
    decimal TotalActualWatchedSeconds,
    int LockedVideoCount,
    DateTime? LastWatchedAt,
    IReadOnlyList<AdminAIStudentWatchItem> RecentWatches);

public sealed record AdminAIStudentDeviceActivity(
    int DeviceCount,
    int ActiveDeviceCount,
    DateTime? LastDeviceActiveAt);

public sealed record AdminAIStudentCommitmentActivity(
    string CommitmentStatus,
    int ConsecutiveMissedHomeworks,
    int ConsecutiveFailedExams);

public sealed record AdminAIStudentWarningActivity(
    int UnresolvedWarningCount,
    int CriticalUnresolvedWarningCount);

public sealed record AdminAIStudentNoteActivity(
    int AdminNoteCount,
    int PinnedAdminNoteCount);

public sealed record AdminAIStudentActivitySection(
    AdminAIStudentWatchingActivity? Watching,
    int? CompletedLessonCount,
    AdminAIStudentDeviceActivity? Devices,
    AdminAIStudentCommitmentActivity? Commitment,
    AdminAIStudentWarningActivity? Warnings,
    AdminAIStudentNoteActivity? AdminNotes);

public sealed record AdminAIStudentExamAttemptItem(
    Guid ExamId,
    string ExamTitle,
    Guid TeacherId,
    string TeacherName,
    decimal ScoreAchieved,
    decimal CurrentTotalScore,
    string AttemptState,
    DateTime AttemptedAt);

public sealed record AdminAIStudentHomeworkItem(
    Guid HomeworkId,
    string HomeworkTitle,
    Guid TeacherId,
    string TeacherName,
    decimal ScoreAchieved,
    decimal CurrentTotalScore,
    string Status,
    DateTime ActivityAt);

public sealed record AdminAIStudentExamAssessments(
    int ExamAttemptCount,
    int DistinctExamCount,
    int PassedAttemptCount,
    int FailedAttemptCount,
    int InProgressAttemptCount,
    int PendingGradingAttemptCount,
    int TimedOutAttemptCount,
    int DistinctEverPassedExamCount,
    int DistinctEverFailedExamCount,
    IReadOnlyList<AdminAIStudentExamAttemptItem> RecentExamAttempts,
    bool PassedAndFailedDistinctCountsCanOverlap,
    bool ScoresUseCurrentAssessmentTotals);

public sealed record AdminAIStudentHomeworkAssessments(
    int HomeworkSubmissionCount,
    int HomeworkGradedCount,
    int HomeworkMissedCount,
    IReadOnlyList<AdminAIStudentHomeworkItem> RecentHomework);

public sealed record AdminAIStudentEssayAssessments(
    int EssaySubmissionCount,
    int EssayAwaitingTeacherCount,
    int EssayTeacherGradedCount);

public sealed record AdminAIStudentAssessmentsSection(
    AdminAIStudentExamAssessments? Exams,
    AdminAIStudentHomeworkAssessments? Homework,
    AdminAIStudentEssayAssessments? Essays);

internal sealed record AdminAIStudentSnapshotRequest(
    Guid StudentId,
    int RecentLimit,
    Guid? BalanceContextTeacherId,
    Guid? SubscriptionContextTeacherId,
    DateTime DataAsOf,
    IReadOnlySet<string> ProfileFields,
    IReadOnlySet<string> ContactFields,
    IReadOnlySet<string> ActivityFields,
    IReadOnlySet<string> AssessmentFields);

internal sealed record AdminAIStudentSnapshotSelection(
    AdminAIStudentSnapshotRequest Request,
    IReadOnlySet<string> Sections,
    IReadOnlyList<string> IncludedSections);

internal sealed record AdminAIStudentSnapshotSection<T>(T Payload, bool IsTruncated);

internal sealed record AdminAIStudentSnapshotSubject(Guid Id, string DisplayName, string PhoneNumber);

internal sealed record AdminAIStudentSnapshotSections(
    AdminAIStudentProfileSection? Profile,
    AdminAIStudentContactSection? Contact,
    AdminAIStudentBalancesSection? Balances,
    AdminAIStudentSubscriptionsSection? Subscriptions,
    AdminAIStudentActivitySection? Activity,
    AdminAIStudentAssessmentsSection? Assessments,
    bool IsTruncated);

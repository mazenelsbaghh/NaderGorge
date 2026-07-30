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
using NaderGorge.Application.Interfaces;

namespace NaderGorge.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly IUserSecurityStateCache? _userSecurityStateCache;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IUserSecurityStateCache userSecurityStateCache) : base(options)
    {
        _userSecurityStateCache = userSecurityStateCache;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<AcademicSubjectEligibility> AcademicSubjectEligibilities => Set<AcademicSubjectEligibility>();
    public DbSet<StudentFacingAcademicScope> StudentFacingAcademicScopes => Set<StudentFacingAcademicScope>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CodeGroup> CodeGroups => Set<CodeGroup>();
    public DbSet<AccessCode> AccessCodes => Set<AccessCode>();
    public DbSet<StudentAccessGrant> StudentAccessGrants => Set<StudentAccessGrant>();
    public DbSet<GiftIssuance> GiftIssuances => Set<GiftIssuance>();
    public DbSet<GiftRecipient> GiftRecipients => Set<GiftRecipient>();
    public DbSet<PromotionalBalanceAllocation> PromotionalBalanceAllocations => Set<PromotionalBalanceAllocation>();
    public DbSet<PromotionalBalanceUsage> PromotionalBalanceUsages => Set<PromotionalBalanceUsage>();
    public DbSet<SalesRule> SalesRules => Set<SalesRule>();
    public DbSet<DiscountStackingPolicy> DiscountStackingPolicies => Set<DiscountStackingPolicy>();
    public DbSet<SalesCoupon> SalesCoupons => Set<SalesCoupon>();
    public DbSet<SalesCouponUsage> SalesCouponUsages => Set<SalesCouponUsage>();
    public DbSet<PrintableCodeBatch> PrintableCodeBatches => Set<PrintableCodeBatch>();
    public DbSet<PrintableSalesCode> PrintableSalesCodes => Set<PrintableSalesCode>();
    public DbSet<PrintableCodeRedemption> PrintableCodeRedemptions => Set<PrintableCodeRedemption>();
    public DbSet<PrintableCodeTemplate> PrintableCodeTemplates => Set<PrintableCodeTemplate>();
    public DbSet<PublicExamProduct> PublicExamProducts => Set<PublicExamProduct>();
    public DbSet<SalesFinancialEffect> SalesFinancialEffects => Set<SalesFinancialEffect>();

    // Content
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<TeacherStaffMember> TeacherStaffMembers => Set<TeacherStaffMember>();
    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageCodePageProfile> PackageCodePageProfiles => Set<PackageCodePageProfile>();
    public DbSet<ContentSection> ContentSections => Set<ContentSection>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonVideo> LessonVideos => Set<LessonVideo>();
    public DbSet<VideoType> VideoTypes => Set<VideoType>();
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
    public DbSet<HrIdempotencyRecord> HrIdempotencyRecords => Set<HrIdempotencyRecord>();
    public DbSet<HrModuleRollout> HrModuleRollouts => Set<HrModuleRollout>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<JobPosition> JobPositions => Set<JobPosition>();
    public DbSet<JobGrade> JobGrades => Set<JobGrade>();
    public DbSet<WorkLocation> WorkLocations => Set<WorkLocation>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<EmploymentAssignment> EmploymentAssignments => Set<EmploymentAssignment>();
    public DbSet<EmploymentContract> EmploymentContracts => Set<EmploymentContract>();
    public DbSet<WorkCalendar> WorkCalendars => Set<WorkCalendar>();
    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();
    public DbSet<ShiftSegment> ShiftSegments => Set<ShiftSegment>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<ShiftSwapRequest> ShiftSwapRequests => Set<ShiftSwapRequest>();
    public DbSet<AttendancePolicy> AttendancePolicies => Set<AttendancePolicy>();
    public DbSet<AttendancePolicyAssignment> AttendancePolicyAssignments => Set<AttendancePolicyAssignment>();
    public DbSet<TrustedAttendanceDevice> TrustedAttendanceDevices => Set<TrustedAttendanceDevice>();
    public DbSet<AttendancePolicyException> AttendancePolicyExceptions => Set<AttendancePolicyException>();
    public DbSet<AttendanceAttempt> AttendanceAttempts => Set<AttendanceAttempt>();
    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();
    public DbSet<AttendanceBreak> AttendanceBreaks => Set<AttendanceBreak>();
    public DbSet<WorkdayClassification> WorkdayClassifications => Set<WorkdayClassification>();
    public DbSet<AttendanceCorrection> AttendanceCorrections => Set<AttendanceCorrection>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeavePolicy> LeavePolicies => Set<LeavePolicy>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<LeaveLedgerEntry> LeaveLedgerEntries => Set<LeaveLedgerEntry>();
    public DbSet<HrLeaveRequest> HrLeaveRequests => Set<HrLeaveRequest>();
    public DbSet<ApprovalDefinition> ApprovalDefinitions => Set<ApprovalDefinition>();
    public DbSet<ApprovalDefinitionStep> ApprovalDefinitionSteps => Set<ApprovalDefinitionStep>();
    public DbSet<ApprovalInstance> ApprovalInstances => Set<ApprovalInstance>();
    public DbSet<ApprovalStepInstance> ApprovalStepInstances => Set<ApprovalStepInstance>();
    public DbSet<ApprovalDelegation> ApprovalDelegations => Set<ApprovalDelegation>();
    public DbSet<PayComponent> PayComponents => Set<PayComponent>();
    public DbSet<PayrollRule> PayrollRules => Set<PayrollRule>();
    public DbSet<EmployeeCompensation> EmployeeCompensations => Set<EmployeeCompensation>();
    public DbSet<HrPayrollRun> HrPayrollRuns => Set<HrPayrollRun>();
    public DbSet<EmployeePayroll> EmployeePayrolls => Set<EmployeePayroll>();
    public DbSet<PayrollLineItem> PayrollLineItems => Set<PayrollLineItem>();
    public DbSet<Payslip> Payslips => Set<Payslip>();
    public DbSet<PayrollSettlementAdjustment> PayrollSettlementAdjustments => Set<PayrollSettlementAdjustment>();
    public DbSet<HrFinancialRequest> HrFinancialRequests => Set<HrFinancialRequest>();
    public DbSet<HrFinancialInstallment> HrFinancialInstallments => Set<HrFinancialInstallment>();
    public DbSet<HrPayrollInputSource> HrPayrollInputSources => Set<HrPayrollInputSource>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeDocumentVersion> EmployeeDocumentVersions => Set<EmployeeDocumentVersion>();
    public DbSet<HrAsset> HrAssets => Set<HrAsset>();
    public DbSet<AssetCustody> AssetCustodies => Set<AssetCustody>();
    public DbSet<PerformanceCycle> PerformanceCycles => Set<PerformanceCycle>();
    public DbSet<PerformanceGoal> PerformanceGoals => Set<PerformanceGoal>();
    public DbSet<PerformanceReview> PerformanceReviews => Set<PerformanceReview>();
    public DbSet<EmployeeCase> EmployeeCases => Set<EmployeeCase>();
    public DbSet<CaseEvidence> CaseEvidence => Set<CaseEvidence>();
    public DbSet<CaseResponse> CaseResponses => Set<CaseResponse>();
    public DbSet<DisciplinaryAction> DisciplinaryActions => Set<DisciplinaryAction>();
    public DbSet<Requisition> Requisitions => Set<Requisition>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateInterview> CandidateInterviews => Set<CandidateInterview>();
    public DbSet<CandidateOffer> CandidateOffers => Set<CandidateOffer>();
    public DbSet<EmployeeLifecycleTask> EmployeeLifecycleTasks => Set<EmployeeLifecycleTask>();
    public DbSet<OffboardingProcess> OffboardingProcesses => Set<OffboardingProcess>();
    public DbSet<HrMigrationBatch> HrMigrationBatches => Set<HrMigrationBatch>();
    public DbSet<HrMigrationRecordMap> HrMigrationRecordMaps => Set<HrMigrationRecordMap>();
    public DbSet<HrMigrationConflict> HrMigrationConflicts => Set<HrMigrationConflict>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
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
    public DbSet<TeacherFinancialEvent> TeacherFinancialEvents => Set<TeacherFinancialEvent>();
    public DbSet<TeacherFinancialAllocation> TeacherFinancialAllocations => Set<TeacherFinancialAllocation>();
    public DbSet<TeacherPayoutAdjustment> TeacherPayoutAdjustments => Set<TeacherPayoutAdjustment>();
    public DbSet<TeacherFinancialAgreement> TeacherFinancialAgreements => Set<TeacherFinancialAgreement>();
    public DbSet<CodeGroupFinancialTerms> CodeGroupFinancialTerms => Set<CodeGroupFinancialTerms>();
    public DbSet<CodeGroupDeliveryConfirmation> CodeGroupDeliveryConfirmations => Set<CodeGroupDeliveryConfirmation>();
    public DbSet<TeacherSettlement> TeacherSettlements => Set<TeacherSettlement>();
    public DbSet<TeacherSettlementLine> TeacherSettlementLines => Set<TeacherSettlementLine>();
    public DbSet<TeacherSettlementPayment> TeacherSettlementPayments => Set<TeacherSettlementPayment>();
    public DbSet<FinancialInvoice> FinancialInvoices => Set<FinancialInvoice>();
    public DbSet<SharedTeacherPackage> SharedTeacherPackages => Set<SharedTeacherPackage>();
    public DbSet<SharedTeacherPackageTeacher> SharedTeacherPackageTeachers => Set<SharedTeacherPackageTeacher>();
    public DbSet<SharedTeacherPackageItem> SharedTeacherPackageItems => Set<SharedTeacherPackageItem>();
    public DbSet<AccessCodeActivationLog> AccessCodeActivationLogs => Set<AccessCodeActivationLog>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ClusterLease> ClusterLeases => Set<ClusterLease>();
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
            e.Property(u => u.IsDeleted).HasDefaultValue(false);
            e.Property(u => u.SecurityStampVersion).HasDefaultValue(0);
        });

        modelBuilder.Entity<ClusterLease>(e =>
        {
            e.ToTable("cluster_leases");
            e.HasKey(lease => lease.Name);
            e.Property(lease => lease.Name).HasMaxLength(160);
            e.Property(lease => lease.LastOutcome).HasMaxLength(64);
            e.HasIndex(lease => lease.ExpiresAt);
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
            e.Property(tp => tp.PublicSlug).HasMaxLength(160);
            e.HasIndex(tp => tp.PublicSlug).IsUnique().HasFilter("\"PublicSlug\" IS NOT NULL");
            e.Property(tp => tp.PublicBio).HasMaxLength(2000);
            e.Property(tp => tp.IntroVideoUrl).HasMaxLength(1000);
            e.Property(tp => tp.IsVisibleToStudents).HasDefaultValue(true);
            e.Property(tp => tp.IsContentVisibleToStudents).HasDefaultValue(true);
            e.Property(tp => tp.RatingAverage).HasPrecision(5, 2);
        });

        // TeacherSubject
        modelBuilder.Entity<TeacherSubject>(e =>
        {
            e.ToTable("teacher_subjects");
            e.HasKey(ts => new { ts.TeacherId, ts.SubjectId });
            e.HasOne(ts => ts.Teacher).WithMany(t => t.TeacherSubjects).HasForeignKey(ts => ts.TeacherId);
            e.HasOne(ts => ts.Subject).WithMany(s => s.TeacherSubjects).HasForeignKey(ts => ts.SubjectId);
        });

        modelBuilder.Entity<TeacherStaffMember>(e =>
        {
            e.ToTable("teacher_staff_members");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TeacherId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.Teacher).WithMany(t => t.StaffMembers).HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.TeacherStaffMemberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByTeacherUser).WithMany(u => u.CreatedTeacherStaffMembers).HasForeignKey(x => x.CreatedByTeacherUserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.PermissionKeys).HasMaxLength(500).HasDefaultValue(string.Empty);
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
            e.Property(a => a.RequestId).HasMaxLength(100);
            e.Property(a => a.ActorType).HasMaxLength(20).IsRequired();
            e.Property(a => a.Reason).HasMaxLength(1000);
            e.HasOne(a => a.PerformedByUser).WithMany().HasForeignKey(a => a.PerformedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ReportDefinition>(e =>
        {
            e.ToTable("report_definitions");
            e.HasKey(report => report.Id);
            e.HasIndex(report => new { report.OwnerUserId, report.Domain, report.CreatedAt });
            e.Property(report => report.Name).HasMaxLength(120).IsRequired();
            e.Property(report => report.Domain).HasMaxLength(64).IsRequired();
            e.Property(report => report.ConfigurationJson).HasColumnType("jsonb").IsRequired();
            e.Property(report => report.Version).IsRowVersion();
            e.HasOne(report => report.OwnerUser).WithMany().HasForeignKey(report => report.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
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
            e.HasIndex(s => new { s.EducationStage, s.GradeLevel, s.UserId });
            e.Property(s => s.HasSeenTrackingCodePopup).HasDefaultValue(false);
        });

        modelBuilder.Entity<AcademicSubjectEligibility>(e =>
        {
            e.ToTable("academic_subject_eligibilities");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.EducationStage, x.GradeLevel, x.SubjectId }).IsUnique();
            e.HasIndex(x => new { x.EducationStage, x.GradeLevel, x.IsActive });
            e.HasIndex(x => new { x.SubjectId, x.IsActive, x.EducationStage, x.GradeLevel });
            e.HasIndex(x => x.SubjectId);
            e.Property(x => x.EducationStage).HasConversion<int>();
            e.Property(x => x.GradeLevel).HasConversion<int>();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StudentFacingAcademicScope>(e =>
        {
            e.ToTable("student_facing_academic_scopes", table =>
            {
                table.HasCheckConstraint(
                    "CK_student_facing_scopes_shape",
                    "(\"ScopeLevel\" = 1 AND \"EducationStage\" IS NULL AND \"GradeLevel\" IS NULL AND \"SubjectId\" IS NULL) OR " +
                    "(\"ScopeLevel\" = 2 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NULL AND \"SubjectId\" IS NULL) OR " +
                    "(\"ScopeLevel\" = 3 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NOT NULL AND \"SubjectId\" IS NULL) OR " +
                    "(\"ScopeLevel\" = 0 AND \"EducationStage\" IS NOT NULL AND \"GradeLevel\" IS NOT NULL AND \"SubjectId\" IS NOT NULL)");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerType, x.OwnerId });
            e.HasIndex(x => new { x.OwnerType, x.OwnerId, x.ScopeLevel, x.EducationStage, x.GradeLevel, x.SubjectId });
            e.HasIndex(x => new { x.ScopeLevel, x.EducationStage, x.GradeLevel, x.SubjectId });
            e.HasIndex(x => x.SubjectId);
            e.Property(x => x.OwnerType).HasConversion<int>();
            e.Property(x => x.ScopeLevel).HasConversion<int>();
            e.Property(x => x.EducationStage).HasConversion<int?>();
            e.Property(x => x.GradeLevel).HasConversion<int?>();
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
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
            e.Property(c => c.RevenueOwner).HasConversion<int>();
            e.Property(c => c.RevenueAllocationMode).HasConversion<int>();
            e.Property(c => c.RevenueAllocationValue).HasColumnType("decimal(18,2)");
            e.Property(c => c.AccountingTiming).HasConversion<int>();
            e.Property(c => c.IncludeFutureVideos).HasDefaultValue(true);
            e.Property(c => c.ExpireActivatedAccess).HasDefaultValue(true);
            e.HasIndex(c => c.PublicExamProductId);
            e.HasIndex(c => c.VideoTypeId);
            e.HasOne(c => c.CreatedByUser).WithMany().HasForeignKey(c => c.CreatedByUserId);
            e.HasOne(c => c.Teacher).WithMany(t => t.CodeGroups).HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.SetNull);
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
            e.ToTable("student_access_grants", table =>
            {
                table.HasCheckConstraint(
                    "CK_student_access_grants_gift_uses",
                    "\"UsesConsumed\" >= 0 AND (\"MaxUses\" IS NULL OR (\"MaxUses\" > 0 AND \"UsesConsumed\" <= \"MaxUses\"))");
                table.HasCheckConstraint(
                    "CK_student_access_grants_target_shape",
                    "(\"GrantType\" = 0 AND \"PackageId\" IS NOT NULL AND \"TermId\" IS NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                    "(\"GrantType\" = 1 AND \"TermId\" IS NOT NULL AND \"ContentSectionId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                    "(\"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                    "(\"GrantType\" = 3 AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL) OR " +
                    "(\"GrantType\" = 4 AND (\"LessonVideoId\" IS NOT NULL OR \"VideoTypeId\" IS NOT NULL) AND \"ExamId\" IS NULL) OR " +
                    "(\"GrantType\" = 5 AND \"ExamId\" IS NOT NULL AND \"LessonVideoId\" IS NULL)");
            });
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.UserId, s.PackageId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"PackageId\" IS NOT NULL AND \"GrantType\" = 0");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.TermId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 1 AND \"TermId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.ContentSectionId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 2 AND \"ContentSectionId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.LessonId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 3 AND \"LessonId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.LessonVideoId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 4 AND \"LessonVideoId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.VideoTypeId, s.PackageId, s.TermId, s.ContentSectionId, s.LessonId })
                .HasDatabaseName("IX_student_access_grants_video_type_scope")
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 4 AND \"VideoTypeId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.ExamId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"GrantType\" = 5 AND \"ExamId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.PackageId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"PackageId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.TermId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"TermId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.ContentSectionId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"ContentSectionId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.LessonId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"LessonId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.LessonVideoId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"LessonVideoId\" IS NOT NULL");
            e.HasIndex(s => new { s.UserId, s.GrantType, s.AccessCodeId, s.ExamId })
                .IsUnique()
                .HasFilter("\"IsActive\" = TRUE AND \"AccessCodeId\" IS NOT NULL AND \"ExamId\" IS NOT NULL");
            e.Property(s => s.GrantType).HasConversion<int>();
            e.Property(s => s.CancellationReason).HasMaxLength(1000);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
            e.HasOne(s => s.AccessCode).WithMany().HasForeignKey(s => s.AccessCodeId);
            e.HasIndex(s => s.GiftRecipientId).IsUnique();
            e.HasIndex(s => s.PublicExamProductId);
            e.Property(s => s.UsesConsumed).HasDefaultValue(0);
            e.HasOne(s => s.GiftRecipient).WithOne(r => r.AccessGrant).HasForeignKey<StudentAccessGrant>(s => s.GiftRecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.PublicExamProduct).WithMany().HasForeignKey(s => s.PublicExamProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.CancelledByUser).WithMany().HasForeignKey(s => s.CancelledByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GiftIssuance>(e =>
        {
            e.ToTable("gift_issuances", table =>
            {
                table.HasCheckConstraint(
                    "CK_gift_issuances_target",
                    "(\"TargetType\" = 0 AND \"PackageId\" IS NOT NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR " +
                    "(\"TargetType\" = 1 AND \"PackageId\" IS NULL AND \"LessonId\" IS NOT NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR " +
                    "(\"TargetType\" = 2 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NOT NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR " +
                    "(\"TargetType\" = 3 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NOT NULL AND \"TeacherId\" IS NULL AND \"Amount\" IS NULL) OR " +
                    "(\"TargetType\" = 4 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NULL AND \"Amount\" > 0) OR " +
                    "(\"TargetType\" = 5 AND \"PackageId\" IS NULL AND \"LessonId\" IS NULL AND \"LessonVideoId\" IS NULL AND \"ExamId\" IS NULL AND \"TeacherId\" IS NOT NULL AND \"Amount\" > 0)");
                table.HasCheckConstraint("CK_gift_issuances_max_uses", "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RequestId).IsUnique();
            e.HasIndex(x => new { x.CreatedAt, x.Status });
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.IssuedByUser).WithMany().HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lesson).WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.LessonVideo).WithMany().HasForeignKey(x => x.LessonVideoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Exam).WithMany().HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GiftRecipient>(e =>
        {
            e.ToTable("gift_recipients", table => table.HasCheckConstraint("CK_gift_recipients_uses", "\"UsesConsumed\" >= 0"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.GiftIssuanceId, x.StudentId }).IsUnique();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.OutcomeCode).HasMaxLength(80).IsRequired();
            e.Property(x => x.OutcomeMessage).HasMaxLength(500);
            e.Property(x => x.RevocationReason).HasMaxLength(500);
            e.HasOne(x => x.GiftIssuance).WithMany(x => x.Recipients).HasForeignKey(x => x.GiftIssuanceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RevokedByUser).WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PromotionalBalanceAllocation>(e =>
        {
            e.ToTable("promotional_balance_allocations", table =>
            {
                table.HasCheckConstraint(
                    "CK_promotional_balance_conservation",
                    "\"OriginalAmount\" > 0 AND \"AvailableAmount\" >= 0 AND \"ConsumedAmount\" >= 0 AND \"ExpiredAmount\" >= 0 AND \"RevokedAmount\" >= 0 AND \"OriginalAmount\" = \"AvailableAmount\" + \"ConsumedAmount\" + \"ExpiredAmount\" + \"RevokedAmount\"");
                table.HasCheckConstraint("CK_promotional_balance_purchase_count", "\"PurchaseCount\" >= 0 AND (\"MaxPurchaseCount\" IS NULL OR (\"MaxPurchaseCount\" > 0 AND \"PurchaseCount\" <= \"MaxPurchaseCount\"))");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GiftRecipientId).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.TeacherId, x.Status, x.ExpiresAt });
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.OriginalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AvailableAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.ConsumedAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.ExpiredAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.RevokedAmount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.GiftRecipient).WithOne(x => x.PromotionalBalanceAllocation).HasForeignKey<PromotionalBalanceAllocation>(x => x.GiftRecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PromotionalBalanceUsage>(e =>
        {
            e.ToTable("promotional_balance_usages", table => table.HasCheckConstraint("CK_promotional_balance_usage_amount", "\"Amount\" > 0"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PurchaseOperationId, x.AllocationId }).IsUnique();
            e.Property(x => x.ContentType).HasConversion<int>();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Allocation).WithMany(x => x.Usages).HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.GiftRecipient).WithMany().HasForeignKey(x => x.GiftRecipientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesRule>(e =>
        {
            e.ToTable("sales_rules");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.TeacherId, x.VideoTypeId, x.IsActive });
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.GradeLevel).HasMaxLength(80);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.VideoType).WithMany().HasForeignKey(x => x.VideoTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DiscountStackingPolicy>(e =>
        {
            e.ToTable("discount_stacking_policies", table =>
            {
                table.HasCheckConstraint("CK_discount_policy_percentage", "\"MaxDiscountPercentage\" IS NULL OR (\"MaxDiscountPercentage\" >= 0 AND \"MaxDiscountPercentage\" <= 100)");
                table.HasCheckConstraint("CK_discount_policy_amount", "\"MaxDiscountAmount\" IS NULL OR \"MaxDiscountAmount\" > 0");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedName).IsUnique();
            e.HasIndex(x => x.IsDefault);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(120).IsRequired();
            e.Property(x => x.Mode).HasConversion<int>();
            e.Property(x => x.MaxDiscountPercentage).HasColumnType("decimal(18,2)");
            e.Property(x => x.MaxDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PriorityJson).HasColumnType("jsonb");
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesCoupon>(e =>
        {
            e.ToTable("sales_coupons", table =>
            {
                table.HasCheckConstraint("CK_sales_coupons_discount_value", "\"DiscountValue\" > 0 AND (\"DiscountType\" <> 0 OR \"DiscountValue\" <= 100)");
                table.HasCheckConstraint("CK_sales_coupons_limits", "(\"GlobalUsageLimit\" IS NULL OR \"GlobalUsageLimit\" > 0) AND (\"PerStudentUsageLimit\" IS NULL OR \"PerStudentUsageLimit\" > 0) AND \"UsedCount\" >= 0");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.NormalizedCode).IsUnique();
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.Status });
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.NormalizedCode).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.DiscountType).HasConversion<int>();
            e.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)");
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.OwnerType).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DisableReason).HasMaxLength(500);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.StackingPolicy).WithMany().HasForeignKey(x => x.StackingPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesCouponUsage>(e =>
        {
            e.ToTable("sales_coupon_usages", table => table.HasCheckConstraint("CK_sales_coupon_usage_amounts", "\"GrossAmount\" >= 0 AND \"DiscountAmount\" > 0"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CouponId, x.PurchaseOperationId }).IsUnique();
            e.HasIndex(x => new { x.CouponId, x.StudentId, x.PurchaseOperationId }).IsUnique();
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.Coupon).WithMany(x => x.Usages).HasForeignKey(x => x.CouponId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrintableCodeTemplate>(e =>
        {
            e.ToTable("printable_code_templates", table => table.HasCheckConstraint("CK_printable_templates_size", "\"WidthMm\" > 0 AND \"HeightMm\" > 0"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.WidthMm).HasColumnType("decimal(18,2)");
            e.Property(x => x.HeightMm).HasColumnType("decimal(18,2)");
            e.Property(x => x.BackgroundColor).HasMaxLength(32);
            e.Property(x => x.BackgroundImageUrl).HasMaxLength(1000);
            e.Property(x => x.LayoutJson).HasColumnType("jsonb");
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrintableCodeBatch>(e =>
        {
            e.ToTable("printable_code_batches", table =>
            {
                table.HasCheckConstraint("CK_printable_batches_total", "\"TotalCodes\" > 0 AND \"TotalCodes\" <= 10000 AND \"UsedCount\" >= 0");
                table.HasCheckConstraint("CK_printable_batches_values", "(\"Behavior\" = 0 AND \"DiscountType\" IS NOT NULL AND \"DiscountValue\" > 0) OR (\"Behavior\" = 1) OR (\"Behavior\" = 2 AND \"CreditAmount\" > 0)");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.Status });
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Behavior).HasConversion<int>();
            e.Property(x => x.DiscountType).HasConversion<int>();
            e.Property(x => x.DiscountValue).HasColumnType("decimal(18,2)");
            e.Property(x => x.CreditAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.OwnerType).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DisableReason).HasMaxLength(500);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Template).WithMany(x => x.Batches).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.StackingPolicy).WithMany().HasForeignKey(x => x.StackingPolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PrintableSalesCode>(e =>
        {
            e.ToTable("printable_sales_codes", table => table.HasCheckConstraint("CK_printable_sales_codes_usage", "\"UsageLimit\" > 0 AND \"UsedCount\" >= 0 AND \"UsedCount\" <= \"UsageLimit\""));
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CodeHash).IsUnique();
            e.HasIndex(x => x.SerialNumber).IsUnique();
            e.Property(x => x.CodeHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.CodePlaintext).HasMaxLength(80);
            e.Property(x => x.QrPayload).HasMaxLength(500).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasOne(x => x.Batch).WithMany(x => x.Codes).HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ConsumedByUser).WithMany().HasForeignKey(x => x.ConsumedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PrintableCodeRedemption>(e =>
        {
            e.ToTable("printable_code_redemptions", table => table.HasCheckConstraint("CK_printable_redemption_amount", "\"AppliedAmount\" >= 0"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PrintableCodeId, x.RequestId }).IsUnique();
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.AppliedAmount).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.PrintableCode).WithMany(x => x.Redemptions).HasForeignKey(x => x.PrintableCodeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PublicExamProduct>(e =>
        {
            e.ToTable("public_exam_products", table => table.HasCheckConstraint("CK_public_exam_price", "(\"IsPaid\" = FALSE AND \"Price\" = 0) OR (\"IsPaid\" = TRUE AND \"Price\" > 0)"));
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ExamId).IsUnique();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsPublished, x.DisabledAt, x.AvailableFrom, x.AvailableUntil });
            e.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.GradeLevel).HasMaxLength(80);
            e.Property(x => x.DisableReason).HasMaxLength(500);
            e.HasOne(x => x.Exam).WithOne(x => x.PublicExamProduct).HasForeignKey<PublicExamProduct>(x => x.ExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject).WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DisabledByUser).WithMany().HasForeignKey(x => x.DisabledByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesFinancialEffect>(e =>
        {
            e.ToTable("sales_financial_effects", table =>
            {
                table.HasCheckConstraint("CK_sales_financial_effect_amounts", "\"GrossAmount\" >= 0 AND \"CouponDiscountAmount\" >= 0 AND \"PrintableCodeDiscountAmount\" >= 0 AND \"PromotionalAmount\" >= 0 AND \"PaidAmount\" >= 0 AND \"TeacherShareImpact\" >= 0 AND \"PlatformShareImpact\" >= 0");
                table.HasCheckConstraint("CK_sales_financial_effect_conservation", "\"GrossAmount\" = \"CouponDiscountAmount\" + \"PrintableCodeDiscountAmount\" + \"PromotionalAmount\" + \"PaidAmount\"");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PurchaseOperationId).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.TargetType, x.TargetId });
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.CouponDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PrintableCodeDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PromotionalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TeacherShareImpact).HasColumnType("decimal(18,2)");
            e.Property(x => x.PlatformShareImpact).HasColumnType("decimal(18,2)");
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
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
            e.Property(l => l.InternalCode).HasMaxLength(40).IsRequired();
            e.HasIndex(l => l.InternalCode).IsUnique();
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.ContentSection).WithMany(cs => cs.Lessons).HasForeignKey(l => l.ContentSectionId);
        });

        // LessonVideo
        modelBuilder.Entity<LessonVideo>(e =>
        {
            e.ToTable("lesson_videos");
            e.HasKey(l => l.Id);
            e.Property(l => l.InternalCode).HasMaxLength(40).IsRequired();
            e.HasIndex(l => l.InternalCode).IsUnique();
            e.Property(l => l.Title).HasMaxLength(200).IsRequired();
            e.HasOne(l => l.Lesson).WithMany(le => le.Videos).HasForeignKey(l => l.LessonId);
            e.HasOne(l => l.VideoType)
                .WithMany(type => type.Videos)
                .HasForeignKey(l => l.VideoTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Exam)
             .WithMany()
             .HasForeignKey(l => l.ExamId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<VideoType>(e =>
        {
            e.ToTable("video_types");
            e.HasKey(type => type.Id);
            e.Property(type => type.Name).HasMaxLength(80).IsRequired();
            e.Property(type => type.NormalizedName).HasMaxLength(80).IsRequired();
            e.HasIndex(type => type.NormalizedName).IsUnique();
            e.HasIndex(type => new { type.SortOrder, type.Name });
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
            e.HasIndex(cp => cp.TeacherId);
            e.HasIndex(cp => cp.Status);
            e.HasIndex(cp => cp.CreatedAt);
            e.HasOne(cp => cp.AuthorUser)
                .WithMany()
                .HasForeignKey(cp => cp.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(cp => cp.Teacher)
                .WithMany(t => t.CommunityPosts)
                .HasForeignKey(cp => cp.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);
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
            e.HasIndex(c => c.ParentCommentId);
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.CreatedAt);
            e.HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
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
            e.Property(x => x.RequestReason).HasMaxLength(1000).IsRequired();
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
            e.Property(x => x.InternalCode).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.InternalCode).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.PassingScore).HasColumnType("decimal(18,2)");
            e.Property(x => x.TotalScore).HasColumnType("decimal(18,2)");
            e.Property(x => x.IsActive).HasDefaultValue(true);
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
            e.Property(h => h.IsActive).HasDefaultValue(true);
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
            e.HasIndex(n => new { n.AcademicScopeOwnerType, n.AcademicScopeOwnerId });
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
            e.ToTable("student_balances", table =>
                table.HasCheckConstraint("CK_student_balances_non_negative", "\"CurrentBalance\" >= 0"));
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId).IsUnique();
            e.Property(s => s.CurrentBalance).HasColumnType("decimal(18,2)");
            e.Property(s => s.Version).IsConcurrencyToken().HasDefaultValue(0L);
            e.HasOne(s => s.User).WithOne(u => u.StudentBalance).HasForeignKey<StudentBalance>(s => s.UserId).OnDelete(DeleteBehavior.NoAction);
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
            e.HasIndex(b => new { b.TransactionType, b.ReferenceId })
                .IsUnique()
                .HasFilter("\"ReferenceId\" IS NOT NULL AND \"TransactionType\" IN ('DigitalRecharge', 'CodeRedemption')");
            e.HasOne(b => b.StudentBalance).WithMany(s => s.Transactions).HasForeignKey(b => b.StudentBalanceId).OnDelete(DeleteBehavior.Restrict);
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
            e.HasIndex(ep => ep.EmployeeNumber).IsUnique();
            e.HasIndex(ep => new { ep.EmploymentStatus, ep.HireDate, ep.TerminationDate });
            e.HasOne(ep => ep.User)
             .WithOne(u => u.EmployeeProfile)
             .HasForeignKey<EmployeeProfile>(ep => ep.UserId)
             .OnDelete(DeleteBehavior.Restrict);
            e.Property(ep => ep.EmployeeNumber).HasMaxLength(40).IsRequired();
            e.Property(ep => ep.EmploymentStatus).HasConversion<int>().IsRequired();
            e.Property(ep => ep.HireDate).HasColumnType("date").IsRequired();
            e.Property(ep => ep.TerminationDate).HasColumnType("date");
            e.Property(ep => ep.WorkMode).HasConversion<int>().IsRequired();
            e.Property(ep => ep.BasicSalary).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ep => ep.StandardStartTime).IsRequired();
            e.Property(ep => ep.TargetDailyHours).IsRequired();
        });

        modelBuilder.Entity<HrIdempotencyRecord>(e =>
        {
            e.ToTable("hr_idempotency_records");
            e.HasKey(item => item.Id);
            e.Property(item => item.Scope).HasMaxLength(100).IsRequired();
            e.Property(item => item.Key).HasMaxLength(200).IsRequired();
            e.Property(item => item.RequestHash).HasMaxLength(128).IsRequired();
            e.Property(item => item.ResponseJson).HasMaxLength(8000);
            e.HasIndex(item => new { item.Scope, item.ActorUserId, item.Key }).IsUnique();
            e.HasIndex(item => item.ExpiresAt);
        });

        modelBuilder.Entity<HrModuleRollout>(e =>
        {
            e.ToTable("hr_module_rollouts");
            e.HasKey(item => item.Id);
            e.Property(item => item.Module).HasMaxLength(100).IsRequired();
            e.Property(item => item.ReadTarget).HasMaxLength(20).IsRequired();
            e.Property(item => item.WriteTarget).HasMaxLength(20).IsRequired();
            e.Property(item => item.Reason).HasMaxLength(2000);
            e.Property(item => item.State).HasConversion<int>().IsRequired();
            e.HasIndex(item => item.Module).IsUnique();
        });

        modelBuilder.Entity<OrganizationUnit>(e =>
        {
            e.ToTable("hr_organization_units");
            e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.Type).HasConversion<int>().IsRequired();
            e.Property(item => item.EffectiveFrom).HasColumnType("date");
            e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.HasIndex(item => item.Code).IsUnique();
            e.HasIndex(item => item.ParentId);
            e.HasOne(item => item.Parent).WithMany().HasForeignKey(item => item.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ManagerEmployee).WithMany().HasForeignKey(item => item.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureHrLookup<JobPosition>(modelBuilder, "hr_job_positions");
        ConfigureHrLookup<JobGrade>(modelBuilder, "hr_job_grades");
        ConfigureHrLookup<CostCenter>(modelBuilder, "hr_cost_centers");
        modelBuilder.Entity<WorkLocation>(e =>
        {
            e.ToTable("hr_work_locations");
            e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.Address).HasMaxLength(500);
            e.Property(item => item.Latitude).HasPrecision(9, 6);
            e.Property(item => item.Longitude).HasPrecision(9, 6);
            e.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<EmploymentAssignment>(e =>
        {
            e.ToTable("hr_employment_assignments");
            e.HasKey(item => item.Id);
            e.Property(item => item.EffectiveFrom).HasColumnType("date");
            e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.Property(item => item.ChangeReason).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.EffectiveFrom });
            e.HasIndex(item => new { item.OrganizationUnitId, item.EffectiveFrom, item.EffectiveTo, item.EmployeeId });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.OrganizationUnit).WithMany().HasForeignKey(item => item.OrganizationUnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.JobPosition).WithMany().HasForeignKey(item => item.JobPositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.JobGrade).WithMany().HasForeignKey(item => item.JobGradeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ManagerEmployee).WithMany().HasForeignKey(item => item.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.WorkLocation).WithMany().HasForeignKey(item => item.WorkLocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.CostCenter).WithMany().HasForeignKey(item => item.CostCenterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmploymentContract>(e =>
        {
            e.ToTable("hr_employment_contracts");
            e.HasKey(item => item.Id);
            e.Property(item => item.ContractNumber).HasMaxLength(80).IsRequired();
            e.Property(item => item.Type).HasConversion<int>().IsRequired();
            e.Property(item => item.Status).HasConversion<int>().IsRequired();
            e.Property(item => item.StartDate).HasColumnType("date");
            e.Property(item => item.EndDate).HasColumnType("date");
            e.Property(item => item.ProbationEndDate).HasColumnType("date");
            e.Property(item => item.BaseSalary).HasColumnType("decimal(18,2)");
            e.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            e.Property(item => item.TermsJson).HasColumnType("jsonb");
            e.HasIndex(item => item.ContractNumber).IsUnique();
            e.HasIndex(item => new { item.EmployeeId, item.StartDate });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkCalendar>(e =>
        {
            e.ToTable("hr_work_calendars");
            e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
            e.Property(item => item.HolidaysJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<ShiftTemplate>(e =>
        {
            e.ToTable("hr_shift_templates");
            e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.Mode).HasConversion<int>().IsRequired();
            e.HasIndex(item => item.Code).IsUnique();
            e.HasOne(item => item.WorkCalendar).WithMany(item => item.ShiftTemplates).HasForeignKey(item => item.WorkCalendarId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShiftSegment>(e =>
        {
            e.ToTable("hr_shift_segments", table => table.HasCheckConstraint("CK_hr_shift_segments_nonzero", "\"StartsAt\" <> \"EndsAt\""));
            e.HasKey(item => item.Id);
            e.Property(item => item.WorkDateRule).HasConversion<int>().IsRequired();
            e.HasIndex(item => new { item.ShiftTemplateId, item.Sequence }).IsUnique();
            e.HasOne(item => item.ShiftTemplate).WithMany(item => item.Segments).HasForeignKey(item => item.ShiftTemplateId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShiftAssignment>(e =>
        {
            e.ToTable("hr_shift_assignments", table => table.HasCheckConstraint("CK_hr_shift_assignments_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\""));
            e.HasKey(item => item.Id);
            e.Property(item => item.EffectiveFrom).HasColumnType("date");
            e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.Property(item => item.Status).HasConversion<int>().IsRequired();
            e.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.EffectiveFrom, item.EffectiveTo });
            e.HasIndex(item => item.ReplacesAssignmentId).IsUnique().HasFilter("\"ReplacesAssignmentId\" IS NOT NULL");
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ShiftTemplate).WithMany().HasForeignKey(item => item.ShiftTemplateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ReplacesAssignment).WithMany().HasForeignKey(item => item.ReplacesAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(item => item.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShiftSwapRequest>(e =>
        {
            e.ToTable("hr_shift_swap_requests");
            e.HasKey(item => item.Id);
            e.Property(item => item.Status).HasConversion<int>().IsRequired();
            e.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            e.Property(item => item.DecisionReason).HasMaxLength(1000);
            e.HasIndex(item => new { item.RequesterEmployeeId, item.Status });
            e.HasOne(item => item.RequesterEmployee).WithMany().HasForeignKey(item => item.RequesterEmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.TargetEmployee).WithMany().HasForeignKey(item => item.TargetEmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.RequesterAssignment).WithMany().HasForeignKey(item => item.RequesterAssignmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.TargetAssignment).WithMany().HasForeignKey(item => item.TargetAssignmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendancePolicy>(e =>
        {
            e.ToTable("hr_attendance_policies"); e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired(); e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.Kind).HasConversion<int>().IsRequired(); e.Property(item => item.Latitude).HasPrecision(9, 6); e.Property(item => item.Longitude).HasPrecision(9, 6);
            e.HasIndex(item => item.Code).IsUnique();
        });
        modelBuilder.Entity<AttendancePolicyAssignment>(e =>
        {
            e.ToTable("hr_attendance_policy_assignments", table => table.HasCheckConstraint("CK_hr_attendance_policy_assignment_target", "(CASE WHEN \"EmployeeId\" IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN \"ShiftTemplateId\" IS NOT NULL THEN 1 ELSE 0 END) = 1"));
            e.HasKey(item => item.Id); e.Property(item => item.EffectiveFrom).HasColumnType("date"); e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.HasIndex(item => new { item.EmployeeId, item.EffectiveFrom }); e.HasIndex(item => new { item.ShiftTemplateId, item.EffectiveFrom });
            e.HasOne(item => item.AttendancePolicy).WithMany().HasForeignKey(item => item.AttendancePolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ShiftTemplate).WithMany().HasForeignKey(item => item.ShiftTemplateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TrustedAttendanceDevice>(e =>
        {
            e.ToTable("hr_trusted_attendance_devices"); e.HasKey(item => item.Id);
            e.Property(item => item.TokenHash).HasMaxLength(128).IsRequired(); e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.TokenHash }).IsUnique();
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(item => item.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendancePolicyException>(e =>
        {
            e.ToTable("hr_attendance_policy_exceptions", table => table.HasCheckConstraint("CK_hr_attendance_policy_exception_dates", "\"EndsAt\" > \"StartsAt\"")); e.HasKey(item => item.Id);
            e.Property(item => item.Reason).HasMaxLength(1000).IsRequired(); e.HasIndex(item => new { item.EmployeeId, item.StartsAt, item.EndsAt });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.OverridePolicy).WithMany().HasForeignKey(item => item.OverridePolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(item => item.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceSession>(e =>
        {
            e.ToTable("hr_attendance_sessions", table => table.HasCheckConstraint("CK_hr_attendance_session_times", "\"ClockedOutAt\" IS NULL OR \"ClockedOutAt\" > \"ClockedInAt\"")); e.HasKey(item => item.Id);
            e.Property(item => item.WorkDate).HasColumnType("date"); e.Property(item => item.State).HasConversion<int>().IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.WorkDate });
            e.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("\"State\" = 0");
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ShiftAssignment).WithMany().HasForeignKey(item => item.ShiftAssignmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceBreak>(e =>
        {
            e.ToTable("hr_attendance_breaks", table => table.HasCheckConstraint("CK_hr_attendance_break_times", "\"EndedAt\" IS NULL OR \"EndedAt\" > \"StartedAt\"")); e.HasKey(item => item.Id);
            e.Property(item => item.Kind).HasConversion<int>().IsRequired();
            e.HasIndex(item => item.AttendanceSessionId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
            e.HasOne(item => item.AttendanceSession).WithMany(item => item.Breaks).HasForeignKey(item => item.AttendanceSessionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceAttempt>(e =>
        {
            e.ToTable("hr_attendance_attempts"); e.HasKey(item => item.Id);
            e.Property(item => item.EventType).HasConversion<int>().IsRequired(); e.Property(item => item.DecisionCode).HasMaxLength(100).IsRequired();
            e.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired(); e.Property(item => item.EvidenceJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.EventType, item.IdempotencyKey }).IsUnique(); e.HasIndex(item => new { item.EmployeeId, item.OccurredAt });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.AttendancePolicy).WithMany().HasForeignKey(item => item.AttendancePolicyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.AttendanceSession).WithMany().HasForeignKey(item => item.AttendanceSessionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<WorkdayClassification>(e =>
        {
            e.ToTable("hr_workday_classifications"); e.HasKey(item => item.Id);
            e.Property(item => item.WorkDate).HasColumnType("date"); e.Property(item => item.Kind).HasConversion<int>().IsRequired();
            e.Property(item => item.SourceType).HasMaxLength(100).IsRequired(); e.HasIndex(item => new { item.EmployeeId, item.WorkDate }).IsUnique();
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttendanceCorrection>(e =>
        {
            e.ToTable("hr_attendance_corrections"); e.HasKey(item => item.Id);
            e.Property(item => item.Reason).HasMaxLength(1000).IsRequired(); e.Property(item => item.EvidenceReference).HasMaxLength(1000);
            e.Property(item => item.State).HasConversion<int>().IsRequired(); e.Property(item => item.BeforeJson).HasColumnType("jsonb").IsRequired(); e.Property(item => item.AppliedJson).HasColumnType("jsonb");
            e.Property(item => item.DecisionReason).HasMaxLength(1000); e.HasIndex(item => new { item.EmployeeId, item.State }); e.HasIndex(item => item.AttendanceSessionId);
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.AttendanceSession).WithMany().HasForeignKey(item => item.AttendanceSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LeaveType>(e =>
        {
            e.ToTable("hr_leave_types"); e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(40).IsRequired(); e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(item => item.Code).IsUnique();
        });
        modelBuilder.Entity<LeavePolicy>(e =>
        {
            e.ToTable("hr_leave_policies", table => table.HasCheckConstraint("CK_hr_leave_policy_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\""));
            e.HasKey(item => item.Id); e.Property(item => item.Name).HasMaxLength(200).IsRequired();
            e.Property(item => item.AnnualEntitlement).HasColumnType("decimal(10,2)"); e.Property(item => item.MaximumCarryover).HasColumnType("decimal(10,2)");
            e.Property(item => item.EffectiveFrom).HasColumnType("date"); e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.HasIndex(item => new { item.LeaveTypeId, item.EffectiveFrom });
            e.HasOne(item => item.LeaveType).WithMany().HasForeignKey(item => item.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.WorkCalendar).WithMany().HasForeignKey(item => item.WorkCalendarId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LeaveBalance>(e =>
        {
            e.ToTable("hr_leave_balances", table => table.HasCheckConstraint("CK_hr_leave_balance_nonnegative", "\"Reserved\" >= 0 AND \"Used\" >= 0")); e.HasKey(item => item.Id);
            e.Property(item => item.Granted).HasColumnType("decimal(10,2)"); e.Property(item => item.Carried).HasColumnType("decimal(10,2)");
            e.Property(item => item.Reserved).HasColumnType("decimal(10,2)"); e.Property(item => item.Used).HasColumnType("decimal(10,2)");
            e.HasIndex(item => new { item.EmployeeId, item.LeaveTypeId, item.Year }).IsUnique();
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.LeaveType).WithMany().HasForeignKey(item => item.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LeaveLedgerEntry>(e =>
        {
            e.ToTable("hr_leave_ledger_entries"); e.HasKey(item => item.Id); e.Property(item => item.EntryType).HasConversion<int>();
            e.Property(item => item.Amount).HasColumnType("decimal(10,2)"); e.Property(item => item.SourceType).HasMaxLength(100).IsRequired();
            e.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.SourceType, item.SourceId, item.EntryType }).IsUnique();
            e.HasOne(item => item.LeaveBalance).WithMany().HasForeignKey(item => item.LeaveBalanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrLeaveRequest>(e =>
        {
            e.ToTable("hr_leave_requests", table =>
            {
                table.HasCheckConstraint("CK_hr_leave_request_dates", "\"EndDate\" >= \"StartDate\"");
                table.HasCheckConstraint("CK_hr_leave_request_fraction", "\"DayFraction\" > 0 AND \"DayFraction\" <= 1");
            });
            e.HasKey(item => item.Id); e.Property(item => item.StartDate).HasColumnType("date"); e.Property(item => item.EndDate).HasColumnType("date");
            e.Property(item => item.DayFraction).HasColumnType("decimal(4,2)"); e.Property(item => item.Workdays).HasColumnType("decimal(10,2)");
            e.Property(item => item.ReservedAmount).HasColumnType("decimal(10,2)"); e.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            e.Property(item => item.AttachmentReference).HasMaxLength(1000); e.Property(item => item.State).HasConversion<int>();
            e.Property(item => item.Version).IsConcurrencyToken();
            e.HasIndex(item => new { item.EmployeeId, item.StartDate, item.EndDate });
            e.HasIndex(item => new { item.State, item.StartDate, item.EndDate, item.EmployeeId });
            e.HasIndex(item => item.ApprovalInstanceId).IsUnique().HasFilter("\"ApprovalInstanceId\" IS NOT NULL");
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.LeaveType).WithMany().HasForeignKey(item => item.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.ApprovalInstance).WithMany().HasForeignKey(item => item.ApprovalInstanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ApprovalDefinition>(e =>
        {
            e.ToTable("hr_approval_definitions"); e.HasKey(item => item.Id); e.Property(item => item.RequestType).HasMaxLength(100).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired(); e.HasIndex(item => new { item.RequestType, item.Version }).IsUnique();
        });
        modelBuilder.Entity<ApprovalDefinitionStep>(e =>
        {
            e.ToTable("hr_approval_definition_steps", table => table.HasCheckConstraint("CK_hr_approval_step_sla", "\"SlaMinutes\" > 0")); e.HasKey(item => item.Id);
            e.Property(item => item.Name).HasMaxLength(200).IsRequired(); e.Property(item => item.ApproverKind).HasConversion<int>();
            e.Property(item => item.Permission).HasMaxLength(200); e.Property(item => item.EscalationPermission).HasMaxLength(200);
            e.HasIndex(item => new { item.ApprovalDefinitionId, item.Order }).IsUnique();
            e.HasOne(item => item.ApprovalDefinition).WithMany(item => item.Steps).HasForeignKey(item => item.ApprovalDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ApprovalInstance>(e =>
        {
            e.ToTable("hr_approval_instances"); e.HasKey(item => item.Id); e.Property(item => item.RequestType).HasMaxLength(100).IsRequired(); e.Property(item => item.State).HasConversion<int>();
            e.Property(item => item.Version).IsConcurrencyToken();
            e.HasIndex(item => new { item.RequestType, item.RequestId }).IsUnique(); e.HasIndex(item => new { item.State, item.CurrentStepOrder });
            e.HasOne(item => item.ApprovalDefinition).WithMany().HasForeignKey(item => item.ApprovalDefinitionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.RequesterEmployee).WithMany().HasForeignKey(item => item.RequesterEmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ApprovalStepInstance>(e =>
        {
            e.ToTable("hr_approval_step_instances"); e.HasKey(item => item.Id); e.Property(item => item.State).HasConversion<int>();
            e.Property(item => item.DecisionReason).HasMaxLength(2000); e.HasIndex(item => new { item.ApprovalInstanceId, item.Order }).IsUnique();
            e.HasIndex(item => new { item.State, item.DueAt });
            e.HasOne(item => item.ApprovalInstance).WithMany(item => item.Steps).HasForeignKey(item => item.ApprovalInstanceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.DefinitionStep).WithMany().HasForeignKey(item => item.ApprovalDefinitionStepId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ApprovalDelegation>(e =>
        {
            e.ToTable("hr_approval_delegations", table => table.HasCheckConstraint("CK_hr_approval_delegation_dates", "\"EndsAt\" > \"StartsAt\"")); e.HasKey(item => item.Id);
            e.Property(item => item.Scope).HasMaxLength(100).IsRequired(); e.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.PrincipalUserId, item.DelegateUserId, item.Scope, item.StartsAt, item.EndsAt });
        });
        modelBuilder.Entity<PayComponent>(e =>
        {
            e.ToTable("hr_pay_components"); e.HasKey(item => item.Id); e.Property(item => item.Code).HasMaxLength(50).IsRequired();
            e.Property(item => item.Name).HasMaxLength(200).IsRequired(); e.Property(item => item.Classification).HasConversion<int>(); e.HasIndex(item => item.Code).IsUnique();
        });
        modelBuilder.Entity<PayrollRule>(e =>
        {
            e.ToTable("hr_payroll_rules", table =>
            {
                table.HasCheckConstraint("CK_hr_payroll_rule_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
                table.HasCheckConstraint("CK_hr_payroll_rule_version", "\"Version\" > 0");
            });
            e.HasKey(item => item.Id); e.Property(item => item.Name).HasMaxLength(200).IsRequired(); e.Property(item => item.Expression).HasMaxLength(500).IsRequired();
            e.Property(item => item.Rate).HasColumnType("decimal(18,4)"); e.Property(item => item.EffectiveFrom).HasColumnType("date"); e.Property(item => item.EffectiveTo).HasColumnType("date");
            e.HasIndex(item => new { item.PayComponentId, item.EffectiveFrom, item.Version }).IsUnique();
            e.HasOne(item => item.PayComponent).WithMany().HasForeignKey(item => item.PayComponentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmployeeCompensation>(e =>
        {
            e.ToTable("hr_employee_compensations", table => table.HasCheckConstraint("CK_hr_compensation_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\""));
            e.HasKey(item => item.Id); e.Property(item => item.BaseSalary).HasColumnType("decimal(18,2)"); e.Property(item => item.Currency).HasMaxLength(3).IsRequired();
            e.Property(item => item.EffectiveFrom).HasColumnType("date"); e.Property(item => item.EffectiveTo).HasColumnType("date"); e.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.EffectiveFrom }).IsUnique(); e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrPayrollRun>(e =>
        {
            e.ToTable("hr_payroll_runs", table => table.HasCheckConstraint("CK_hr_payroll_run_period", "\"PeriodEnd\" >= \"PeriodStart\"")); e.HasKey(item => item.Id);
            e.Property(item => item.RunNumber).HasMaxLength(40).IsRequired(); e.Property(item => item.PeriodStart).HasColumnType("date"); e.Property(item => item.PeriodEnd).HasColumnType("date");
            e.Property(item => item.Status).HasConversion<int>(); e.Property(item => item.TotalGross).HasColumnType("decimal(18,2)"); e.Property(item => item.TotalDeductions).HasColumnType("decimal(18,2)"); e.Property(item => item.TotalNet).HasColumnType("decimal(18,2)");
            e.Property(item => item.SourceDataVersion).HasMaxLength(100).IsRequired(); e.Property(item => item.ReconciliationHash).HasMaxLength(128).IsRequired();
            e.HasIndex(item => item.RunNumber).IsUnique(); e.HasIndex(item => new { item.PeriodStart, item.PeriodEnd }).IsUnique();
            e.HasIndex(item => new { item.Status, item.PeriodEnd });
        });
        modelBuilder.Entity<EmployeePayroll>(e =>
        {
            e.ToTable("hr_employee_payrolls"); e.HasKey(item => item.Id); e.Property(item => item.EmployeeNumberSnapshot).HasMaxLength(80).IsRequired();
            e.Property(item => item.EmployeeNameSnapshot).HasMaxLength(300).IsRequired(); e.Property(item => item.BaseSalarySnapshot).HasColumnType("decimal(18,2)");
            e.Property(item => item.Currency).HasMaxLength(3).IsRequired(); e.Property(item => item.Gross).HasColumnType("decimal(18,2)"); e.Property(item => item.Deductions).HasColumnType("decimal(18,2)"); e.Property(item => item.Net).HasColumnType("decimal(18,2)"); e.Property(item => item.Status).HasConversion<int>();
            e.HasIndex(item => new { item.PayrollRunId, item.EmployeeId }).IsUnique(); e.HasOne(item => item.PayrollRun).WithMany(item => item.Employees).HasForeignKey(item => item.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(item => new { item.EmployeeId, item.Status, item.PayrollRunId });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PayrollLineItem>(e =>
        {
            e.ToTable("hr_payroll_line_items"); e.HasKey(item => item.Id); e.Property(item => item.Amount).HasColumnType("decimal(18,2)");
            e.Property(item => item.InputsJson).HasColumnType("jsonb").IsRequired(); e.Property(item => item.Explanation).HasMaxLength(2000).IsRequired();
            e.Property(item => item.SourceType).HasMaxLength(100).IsRequired(); e.HasIndex(item => new { item.EmployeePayrollId, item.SourceType, item.SourceId, item.PayComponentId }).IsUnique();
            e.HasOne(item => item.EmployeePayroll).WithMany(item => item.Lines).HasForeignKey(item => item.EmployeePayrollId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.PayComponent).WithMany().HasForeignKey(item => item.PayComponentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.RuleVersion).WithMany().HasForeignKey(item => item.RuleVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Payslip>(e =>
        {
            e.ToTable("hr_payslips"); e.HasKey(item => item.Id); e.Property(item => item.AssetReference).HasMaxLength(1000).IsRequired(); e.Property(item => item.ContentHash).HasMaxLength(128).IsRequired();
            e.HasIndex(item => new { item.EmployeePayrollId, item.Version }).IsUnique(); e.HasOne(item => item.EmployeePayroll).WithMany().HasForeignKey(item => item.EmployeePayrollId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PayrollSettlementAdjustment>(e =>
        {
            e.ToTable("hr_payroll_settlement_adjustments"); e.HasKey(item => item.Id); e.Property(item => item.Amount).HasColumnType("decimal(18,2)"); e.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            e.HasIndex(item => new { item.OriginalPayrollLineItemId, item.SettlementPayrollRunId }).IsUnique();
            e.HasOne(item => item.OriginalPayrollLineItem).WithMany().HasForeignKey(item => item.OriginalPayrollLineItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.SettlementPayrollRun).WithMany().HasForeignKey(item => item.SettlementPayrollRunId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrFinancialRequest>(e =>
        {
            e.ToTable("hr_financial_requests", table =>
            {
                table.HasCheckConstraint("CK_hr_financial_request_amount", "\"Amount\" > 0 AND \"OutstandingBalance\" >= 0");
                table.HasCheckConstraint("CK_hr_financial_request_installments", "\"RequestedInstallments\" BETWEEN 1 AND 60");
            });
            e.HasKey(item => item.Id); e.Property(item => item.Type).HasConversion<int>(); e.Property(item => item.State).HasConversion<int>();
            e.Property(item => item.Amount).HasColumnType("decimal(18,2)"); e.Property(item => item.OutstandingBalance).HasColumnType("decimal(18,2)");
            e.Property(item => item.Reason).HasMaxLength(2000).IsRequired(); e.Property(item => item.AttachmentReference).HasMaxLength(1000).IsRequired();
            e.HasIndex(item => new { item.EmployeeId, item.State, item.CreatedAt });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrFinancialInstallment>(e =>
        {
            e.ToTable("hr_financial_installments", table => table.HasCheckConstraint("CK_hr_financial_installment_amount", "\"Amount\" > 0"));
            e.HasKey(item => item.Id); e.Property(item => item.DueDate).HasColumnType("date"); e.Property(item => item.Amount).HasColumnType("decimal(18,2)"); e.Property(item => item.State).HasConversion<int>();
            e.HasIndex(item => new { item.FinancialRequestId, item.Sequence }).IsUnique(); e.HasIndex(item => item.PayrollLineItemId).IsUnique().HasFilter("\"PayrollLineItemId\" IS NOT NULL");
            e.HasIndex(item => new { item.State, item.DueDate });
            e.HasOne(item => item.FinancialRequest).WithMany(item => item.Installments).HasForeignKey(item => item.FinancialRequestId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.PayrollLineItem).WithMany().HasForeignKey(item => item.PayrollLineItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrPayrollInputSource>(e =>
        {
            e.ToTable("hr_payroll_input_sources"); e.HasKey(item => item.Id); e.Property(item => item.SourceType).HasMaxLength(100).IsRequired();
            e.HasIndex(item => new { item.SourceType, item.SourceId }).IsUnique(); e.HasIndex(item => item.PayrollLineItemId).IsUnique();
            e.HasOne(item => item.EmployeePayroll).WithMany().HasForeignKey(item => item.EmployeePayrollId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.PayrollLineItem).WithMany().HasForeignKey(item => item.PayrollLineItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmployeeDocument>(e =>
        {
            e.ToTable("hr_employee_documents"); e.HasKey(item => item.Id); e.Property(item => item.Category).HasConversion<int>(); e.Property(item => item.Name).HasMaxLength(300).IsRequired();
            e.Property(item => item.IssuedOn).HasColumnType("date"); e.Property(item => item.ExpiresOn).HasColumnType("date"); e.Property(item => item.RetainUntil).HasColumnType("date");
            e.HasIndex(item => new { item.EmployeeId, item.Category, item.Name }); e.HasIndex(item => new { item.ExpiresOn, item.IsArchived }); e.HasIndex(item => new { item.RetainUntil, item.LegalHold, item.IsArchived });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmployeeDocumentVersion>(e =>
        {
            e.ToTable("hr_employee_document_versions", table => table.HasCheckConstraint("CK_hr_document_version_size", "\"SizeBytes\" >= 0 AND \"Version\" > 0")); e.HasKey(item => item.Id);
            e.Property(item => item.AssetReference).HasMaxLength(1000).IsRequired(); e.Property(item => item.ContentHash).HasMaxLength(128).IsRequired(); e.Property(item => item.MimeType).HasMaxLength(200).IsRequired();
            e.HasIndex(item => new { item.EmployeeDocumentId, item.Version }).IsUnique(); e.HasOne(item => item.EmployeeDocument).WithMany(item => item.Versions).HasForeignKey(item => item.EmployeeDocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrAsset>(e =>
        {
            e.ToTable("hr_assets", table => table.HasCheckConstraint("CK_hr_asset_value", "\"Value\" >= 0")); e.HasKey(item => item.Id);
            e.Property(item => item.Code).HasMaxLength(80).IsRequired(); e.Property(item => item.Name).HasMaxLength(300).IsRequired(); e.Property(item => item.SerialNumber).HasMaxLength(200);
            e.Property(item => item.Value).HasColumnType("decimal(18,2)"); e.Property(item => item.Status).HasConversion<int>(); e.HasIndex(item => item.Code).IsUnique(); e.HasIndex(item => item.SerialNumber).IsUnique().HasFilter("\"SerialNumber\" IS NOT NULL");
        });
        modelBuilder.Entity<AssetCustody>(e =>
        {
            e.ToTable("hr_asset_custodies"); e.HasKey(item => item.Id); e.Property(item => item.State).HasConversion<int>(); e.Property(item => item.AssignedCondition).HasMaxLength(1000).IsRequired();
            e.Property(item => item.ReturnCondition).HasMaxLength(1000); e.Property(item => item.ExceptionReason).HasMaxLength(2000);
            e.HasIndex(item => item.AssetId).IsUnique().HasFilter("\"State\" = 0"); e.HasIndex(item => new { item.EmployeeId, item.State });
            e.HasOne(item => item.Asset).WithMany(item => item.Custodies).HasForeignKey(item => item.AssetId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PerformanceCycle>(e =>
        {
            e.ToTable("hr_performance_cycles", table => table.HasCheckConstraint("CK_hr_performance_cycle_dates", "\"EndsOn\" >= \"StartsOn\"")); e.HasKey(item => item.Id);
            e.Property(item => item.Name).HasMaxLength(200).IsRequired(); e.Property(item => item.StartsOn).HasColumnType("date"); e.Property(item => item.EndsOn).HasColumnType("date"); e.Property(item => item.State).HasConversion<int>();
            e.HasIndex(item => new { item.StartsOn, item.EndsOn });
        });
        modelBuilder.Entity<PerformanceGoal>(e =>
        {
            e.ToTable("hr_performance_goals", table => table.HasCheckConstraint("CK_hr_performance_goal_weight", "\"Weight\" > 0 AND \"Weight\" <= 100")); e.HasKey(item => item.Id);
            e.Property(item => item.Name).HasMaxLength(300).IsRequired(); e.Property(item => item.Weight).HasColumnType("decimal(5,2)"); e.HasIndex(item => new { item.PerformanceCycleId, item.Name }).IsUnique();
            e.HasOne(item => item.PerformanceCycle).WithMany(item => item.Goals).HasForeignKey(item => item.PerformanceCycleId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PerformanceReview>(e =>
        {
            e.ToTable("hr_performance_reviews", table => table.HasCheckConstraint("CK_hr_performance_review_score", "\"WeightedScore\" >= 0 AND \"WeightedScore\" <= 100")); e.HasKey(item => item.Id);
            e.Property(item => item.ScoresJson).HasColumnType("jsonb").IsRequired(); e.Property(item => item.WeightedScore).HasColumnType("decimal(5,2)"); e.Property(item => item.State).HasConversion<int>();
            e.Property(item => item.AppealReason).HasMaxLength(2000); e.Property(item => item.AppealResolution).HasMaxLength(2000); e.HasIndex(item => new { item.PerformanceCycleId, item.EmployeeId }).IsUnique();
            e.HasOne(item => item.PerformanceCycle).WithMany().HasForeignKey(item => item.PerformanceCycleId).OnDelete(DeleteBehavior.Restrict); e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmployeeCase>(e =>
        {
            e.ToTable("hr_employee_cases"); e.HasKey(item => item.Id); e.Property(item => item.CaseNumber).HasMaxLength(80).IsRequired(); e.Property(item => item.Title).HasMaxLength(300).IsRequired();
            e.Property(item => item.Description).HasMaxLength(10000).IsRequired(); e.Property(item => item.State).HasConversion<int>(); e.HasIndex(item => item.CaseNumber).IsUnique(); e.HasIndex(item => new { item.EmployeeId, item.State, item.IsConfidential });
            e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CaseEvidence>(e =>
        {
            e.ToTable("hr_case_evidence"); e.HasKey(item => item.Id); e.Property(item => item.AssetReference).HasMaxLength(1000).IsRequired(); e.Property(item => item.ContentHash).HasMaxLength(128).IsRequired();
            e.HasIndex(item => new { item.EmployeeCaseId, item.ContentHash }).IsUnique(); e.HasOne(item => item.EmployeeCase).WithMany(item => item.Evidence).HasForeignKey(item => item.EmployeeCaseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CaseResponse>(e =>
        {
            e.ToTable("hr_case_responses"); e.HasKey(item => item.Id); e.Property(item => item.Response).HasMaxLength(10000).IsRequired(); e.Property(item => item.AttachmentReference).HasMaxLength(1000);
            e.HasOne(item => item.EmployeeCase).WithMany(item => item.Responses).HasForeignKey(item => item.EmployeeCaseId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DisciplinaryAction>(e =>
        {
            e.ToTable("hr_disciplinary_actions", table => table.HasCheckConstraint("CK_hr_disciplinary_financial", "\"Type\" <> 2 OR \"FinancialAmount\" > 0")); e.HasKey(item => item.Id);
            e.Property(item => item.Type).HasConversion<int>(); e.Property(item => item.FinancialAmount).HasColumnType("decimal(18,2)"); e.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
            e.HasIndex(item => item.PayrollLineItemId).IsUnique().HasFilter("\"PayrollLineItemId\" IS NOT NULL"); e.HasOne(item => item.EmployeeCase).WithMany(item => item.Actions).HasForeignKey(item => item.EmployeeCaseId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(item => item.PayrollLineItem).WithMany().HasForeignKey(item => item.PayrollLineItemId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Requisition>(e =>
        {
            e.ToTable("hr_requisitions", table => table.HasCheckConstraint("CK_hr_requisition_openings", "\"Openings\" > 0")); e.HasKey(item => item.Id);
            e.Property(item => item.RequisitionNumber).HasMaxLength(80).IsRequired(); e.Property(item => item.Title).HasMaxLength(300).IsRequired(); e.Property(item => item.Requirements).HasMaxLength(10000).IsRequired(); e.Property(item => item.State).HasConversion<int>();
            e.HasIndex(item => item.RequisitionNumber).IsUnique(); e.HasIndex(item => new { item.State, item.CreatedAt }); e.HasOne(item => item.OrganizationUnit).WithMany().HasForeignKey(item => item.OrganizationUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Candidate>(e =>
        {
            e.ToTable("hr_candidates"); e.HasKey(item => item.Id); e.Property(item => item.FullName).HasMaxLength(300).IsRequired(); e.Property(item => item.PhoneNumber).HasMaxLength(30).IsRequired();
            e.Property(item => item.Email).HasMaxLength(320); e.Property(item => item.CvAssetReference).HasMaxLength(1000); e.Property(item => item.Stage).HasConversion<int>();
            e.HasIndex(item => new { item.RequisitionId, item.PhoneNumber }).IsUnique(); e.HasIndex(item => item.EmployeeProfileId).IsUnique().HasFilter("\"EmployeeProfileId\" IS NOT NULL");
            e.HasOne(item => item.Requisition).WithMany(item => item.Candidates).HasForeignKey(item => item.RequisitionId).OnDelete(DeleteBehavior.Restrict); e.HasOne(item => item.EmployeeProfile).WithMany().HasForeignKey(item => item.EmployeeProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CandidateInterview>(e =>
        {
            e.ToTable("hr_candidate_interviews", table => table.HasCheckConstraint("CK_hr_interview_score", "\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= 100)")); e.HasKey(item => item.Id); e.Property(item => item.Score).HasColumnType("decimal(5,2)"); e.Property(item => item.Feedback).HasMaxLength(5000);
            e.HasIndex(item => new { item.InterviewerUserId, item.ScheduledAt }); e.HasOne(item => item.Candidate).WithMany(item => item.Interviews).HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CandidateOffer>(e =>
        {
            e.ToTable("hr_candidate_offers", table => table.HasCheckConstraint("CK_hr_offer_salary", "\"BaseSalary\" >= 0")); e.HasKey(item => item.Id); e.Property(item => item.OfferNumber).HasMaxLength(80).IsRequired();
            e.Property(item => item.BaseSalary).HasColumnType("decimal(18,2)"); e.Property(item => item.Currency).HasMaxLength(3).IsRequired(); e.Property(item => item.ProposedStartDate).HasColumnType("date"); e.Property(item => item.State).HasConversion<int>();
            e.HasIndex(item => item.OfferNumber).IsUnique(); e.HasOne(item => item.Candidate).WithMany(item => item.Offers).HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmployeeLifecycleTask>(e =>
        {
            e.ToTable("hr_employee_lifecycle_tasks"); e.HasKey(item => item.Id); e.Property(item => item.Phase).HasMaxLength(80).IsRequired(); e.Property(item => item.Title).HasMaxLength(500).IsRequired(); e.Property(item => item.State).HasConversion<int>(); e.Property(item => item.CompletionNote).HasMaxLength(2000);
            e.HasIndex(item => new { item.State, item.DueAt }); e.HasIndex(item => new { item.EmployeeId, item.Phase }); e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OffboardingProcess>(e =>
        {
            e.ToTable("hr_offboarding_processes"); e.HasKey(item => item.Id); e.Property(item => item.LastWorkingDate).HasColumnType("date"); e.Property(item => item.Reason).HasMaxLength(2000).IsRequired(); e.Property(item => item.State).HasConversion<int>(); e.Property(item => item.BlockersJson).HasColumnType("jsonb").IsRequired();
            e.HasIndex(item => item.EmployeeId).IsUnique().HasFilter("\"State\" <> 3 AND \"State\" <> 4"); e.HasOne(item => item.Employee).WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrMigrationBatch>(e =>
        {
            e.ToTable("hr_migration_batches"); e.HasKey(item => item.Id); e.Property(item => item.Module).HasMaxLength(80).IsRequired(); e.Property(item => item.SourceSystem).HasMaxLength(200).IsRequired();
            e.Property(item => item.RequestHash).HasMaxLength(128).IsRequired(); e.Property(item => item.State).HasConversion<int>(); e.Property(item => item.SourceTotal).HasColumnType("decimal(24,4)"); e.Property(item => item.TargetTotal).HasColumnType("decimal(24,4)");
            e.Property(item => item.SourceHash).HasMaxLength(128).IsRequired(); e.Property(item => item.TargetHash).HasMaxLength(128); e.Property(item => item.ReportJson).HasColumnType("jsonb").IsRequired(); e.HasIndex(item => new { item.Module, item.RequestHash }).IsUnique(); e.HasIndex(item => new { item.Module, item.State, item.CreatedAt });
        });
        modelBuilder.Entity<HrMigrationRecordMap>(e =>
        {
            e.ToTable("hr_migration_record_maps"); e.HasKey(item => item.Id); e.Property(item => item.SourceType).HasMaxLength(100).IsRequired(); e.Property(item => item.SourceId).HasMaxLength(300).IsRequired();
            e.Property(item => item.SourceHash).HasMaxLength(128).IsRequired(); e.Property(item => item.TargetType).HasMaxLength(100).IsRequired(); e.Property(item => item.Amount).HasColumnType("decimal(24,4)");
            e.HasIndex(item => new { item.SourceType, item.SourceId }).IsUnique(); e.HasIndex(item => new { item.MigrationBatchId, item.TargetType, item.TargetId }).IsUnique(); e.HasOne(item => item.MigrationBatch).WithMany(item => item.RecordMaps).HasForeignKey(item => item.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HrMigrationConflict>(e =>
        {
            e.ToTable("hr_migration_conflicts"); e.HasKey(item => item.Id); e.Property(item => item.SourceType).HasMaxLength(100).IsRequired(); e.Property(item => item.SourceId).HasMaxLength(300).IsRequired(); e.Property(item => item.Code).HasMaxLength(100).IsRequired();
            e.Property(item => item.DetailsJson).HasColumnType("jsonb").IsRequired(); e.Property(item => item.State).HasConversion<int>(); e.Property(item => item.ResolutionReason).HasMaxLength(2000);
            e.HasIndex(item => new { item.MigrationBatchId, item.SourceType, item.SourceId, item.Code }).IsUnique(); e.HasIndex(item => new { item.MigrationBatchId, item.State }); e.HasOne(item => item.MigrationBatch).WithMany(item => item.Conflicts).HasForeignKey(item => item.MigrationBatchId).OnDelete(DeleteBehavior.Restrict);
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
             .OnDelete(DeleteBehavior.Restrict);
            e.Property(al => al.Status).HasConversion<int>();
            e.Property(al => al.IpAddress).HasMaxLength(45);
            e.Property(al => al.UserAgent).HasMaxLength(500);
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
             .OnDelete(DeleteBehavior.Restrict);
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
             .OnDelete(DeleteBehavior.Restrict);
        });

        // TeacherAccount
        modelBuilder.Entity<TeacherAccount>(e =>
        {
            e.ToTable("teacher_accounts", table =>
            {
                table.HasCheckConstraint("CK_teacher_accounts_balances_non_negative", "\"TotalEarnings\" >= 0 AND \"CurrentBalance\" >= 0 AND \"ReservedBalance\" >= 0");
                table.HasCheckConstraint("CK_teacher_accounts_reserved_available", "\"ReservedBalance\" <= \"CurrentBalance\"");
            });
            e.HasKey(ta => ta.Id);
            e.Property(ta => ta.TotalEarnings).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ta => ta.CurrentBalance).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ta => ta.ReservedBalance).HasColumnType("decimal(18,2)").HasDefaultValue(0m).IsRequired();
            e.Property(ta => ta.CommissionRate).HasColumnType("decimal(18,2)").IsRequired();
            e.Property(ta => ta.Version).IsConcurrencyToken().HasDefaultValue(0L);
            e.HasOne(ta => ta.Teacher)
             .WithOne()
             .HasForeignKey<TeacherAccount>(ta => ta.TeacherId)
             .OnDelete(DeleteBehavior.Restrict);
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
            e.Property(tp => tp.TransferReference).HasMaxLength(200);
            e.Property(tp => tp.AdminNote).HasMaxLength(2000);
            e.HasOne(tp => tp.Teacher)
             .WithMany()
             .HasForeignKey(tp => tp.TeacherId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(tp => tp.HandledByUser)
             .WithMany()
             .HasForeignKey(tp => tp.HandledByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(tp => tp.ApprovedByUser)
             .WithMany()
             .HasForeignKey(tp => tp.ApprovedByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(tp => tp.PaidByUser)
             .WithMany()
             .HasForeignKey(tp => tp.PaidByUserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(tp => tp.TeacherId);
            e.HasIndex(tp => tp.Status);
        });

        modelBuilder.Entity<TeacherFinancialEvent>(e =>
        {
            e.ToTable("teacher_financial_events", table =>
            {
                table.HasCheckConstraint(
                    "CK_teacher_financial_events_amounts",
                    "\"DiscountAmount\" >= 0 AND \"PlatformDiscountAmount\" >= 0 AND \"TeacherDiscountAmount\" >= 0");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.SourceType).HasConversion<int>();
            e.Property(x => x.TargetType).HasConversion<int>();
            e.Property(x => x.ReviewStatus).HasConversion<int>();
            e.Property(x => x.PayoutStatus).HasConversion<int>();
            e.Property(x => x.GrossAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PlatformDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TeacherDiscountAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaidAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PromotionalAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PlatformShareAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("EGP");
            e.Property(x => x.IdempotencyKey).HasMaxLength(240).IsRequired();
            e.Property(x => x.DetailsJson).HasColumnType("jsonb");
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.HasIndex(x => new { x.ReviewStatus, x.PayoutStatus, x.OccurredAt });
            e.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TeacherFinancialAllocation>(e =>
        {
            e.ToTable("teacher_financial_allocations");
            e.HasKey(x => x.Id);
            e.Property(x => x.AllocationMode).HasConversion<int>();
            e.Property(x => x.ReviewStatus).HasConversion<int>();
            e.Property(x => x.PayoutStatus).HasConversion<int>();
            e.Property(x => x.AllocationValue).HasColumnType("decimal(18,4)");
            e.Property(x => x.GrossBasisAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TeacherShareAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PlatformShareAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AgreementScopeType).HasConversion<int>();
            e.Property(x => x.AgreementAllocationMode).HasConversion<int>();
            e.Property(x => x.PriceBasis).HasConversion<int>();
            e.Property(x => x.DiscountBearer).HasConversion<int>();
            e.Property(x => x.ReversedAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.StudentNameSnapshot).HasMaxLength(200);
            e.Property(x => x.StudentPhoneSnapshot).HasMaxLength(20);
            e.Property(x => x.ContentNameSnapshot).HasMaxLength(300).IsRequired();
            e.HasIndex(x => new { x.TeacherId, x.ReviewStatus, x.PayoutStatus });
            e.HasIndex(x => new { x.TeacherId, x.CreatedAt });
            e.HasIndex(x => x.PayoutId);
            e.HasOne(x => x.TeacherFinancialEvent)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.TeacherFinancialEventId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher)
                .WithMany(x => x.FinancialAllocations)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Payout)
                .WithMany(x => x.Allocations)
                .HasForeignKey(x => x.PayoutId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TeacherPayoutAdjustment>(e =>
        {
            e.ToTable("teacher_payout_adjustments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.TeacherId, x.Status });
            e.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RelatedFinancialEvent)
                .WithMany()
                .HasForeignKey(x => x.RelatedFinancialEventId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.RelatedPayout)
                .WithMany(x => x.Adjustments)
                .HasForeignKey(x => x.RelatedPayoutId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TeacherFinancialAgreement>(e =>
        {
            e.ToTable("teacher_financial_agreements", table =>
            {
                table.HasCheckConstraint("CK_teacher_financial_agreements_value", "\"AllocationValue\" >= 0 AND (\"AllocationMode\" <> 0 OR \"AllocationValue\" <= 100)");
                table.HasCheckConstraint("CK_teacher_financial_agreements_dates", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.ScopeType).HasConversion<int>();
            e.Property(x => x.Trigger).HasConversion<int>();
            e.Property(x => x.AllocationMode).HasConversion<int>();
            e.Property(x => x.PriceBasis).HasConversion<int>();
            e.Property(x => x.AllocationValue).HasColumnType("decimal(18,4)");
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.TeacherId, x.ScopeType, x.ScopeId, x.Trigger, x.EffectiveFrom });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CodeGroupFinancialTerms>(e =>
        {
            e.ToTable("code_group_financial_terms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Trigger).HasConversion<int>();
            e.Property(x => x.Recipient).HasMaxLength(300);
            e.HasIndex(x => x.CodeGroupId).IsUnique();
            e.HasOne(x => x.CodeGroup).WithMany().HasForeignKey(x => x.CodeGroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Agreement).WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CodeGroupDeliveryConfirmation>(e =>
        {
            e.ToTable("code_group_delivery_confirmations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Recipient).HasMaxLength(300).IsRequired();
            e.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            e.Property(x => x.IdempotencyKey).HasMaxLength(240).IsRequired();
            e.HasIndex(x => x.CodeGroupId).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasOne(x => x.CodeGroup).WithMany().HasForeignKey(x => x.CodeGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TeacherSettlement>(e =>
        {
            e.ToTable("teacher_settlements");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("EGP");
            e.Property(x => x.GrossDueAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DebtDeductionAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.NetPayableAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.Note).HasMaxLength(2000);
            e.HasIndex(x => new { x.TeacherId, x.Status, x.PeriodFrom, x.PeriodTo });
            e.HasOne(x => x.Teacher).WithMany().HasForeignKey(x => x.TeacherId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TeacherSettlementLine>(e =>
        {
            e.ToTable("teacher_settlement_lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.DescriptionSnapshot).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.AllocationId).IsUnique().HasFilter("\"AllocationId\" IS NOT NULL");
            e.HasIndex(x => x.AdjustmentId).IsUnique().HasFilter("\"AdjustmentId\" IS NOT NULL");
            e.HasOne(x => x.TeacherSettlement).WithMany(x => x.Lines).HasForeignKey(x => x.TeacherSettlementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Allocation).WithMany().HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Adjustment).WithMany().HasForeignKey(x => x.AdjustmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TeacherSettlementPayment>(e =>
        {
            e.ToTable("teacher_settlement_payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.PaymentMethod).HasMaxLength(100).IsRequired();
            e.Property(x => x.TransferReference).HasMaxLength(200).IsRequired();
            e.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            e.HasOne(x => x.TeacherSettlement).WithMany(x => x.Payments).HasForeignKey(x => x.TeacherSettlementId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinancialInvoice>(e =>
        {
            e.ToTable("financial_invoices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            e.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            e.Property(x => x.PaymentReference).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.DocumentNumber).IsUnique();
            e.HasIndex(x => new { x.TeacherId, x.Status });
        });

        modelBuilder.Entity<SharedTeacherPackage>(e =>
        {
            e.ToTable("shared_teacher_packages", table =>
            {
                table.HasCheckConstraint("CK_shared_teacher_packages_price", "\"Price\" > 0");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.ImageUrl).HasMaxLength(1000);
            e.Property(x => x.Price).HasColumnType("decimal(18,2)");
            e.Property(x => x.DistributionMode).HasConversion<int>();
            e.Property(x => x.EducationStage).HasConversion<int>();
            e.Property(x => x.GradeLevel).HasConversion<int>();
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.IsPublished, x.AvailableFrom, x.AvailableUntil });
            e.HasIndex(x => new { x.EducationStage, x.GradeLevel, x.IsPublished });
            e.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.UpdatedByUser)
                .WithMany()
                .HasForeignKey(x => x.UpdatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SharedTeacherPackageTeacher>(e =>
        {
            e.ToTable("shared_teacher_package_teachers");
            e.HasKey(x => x.Id);
            e.Property(x => x.AllocationMode).HasConversion<int>();
            e.Property(x => x.AllocationValue).HasColumnType("decimal(18,4)");
            e.HasIndex(x => new { x.SharedTeacherPackageId, x.TeacherId, x.SubjectId }).IsUnique();
            e.HasOne(x => x.SharedTeacherPackage)
                .WithMany(x => x.Teachers)
                .HasForeignKey(x => x.SharedTeacherPackageId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher)
                .WithMany(x => x.SharedPackageTeachers)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SharedTeacherPackageItem>(e =>
        {
            e.ToTable("shared_teacher_package_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.ContentType).HasConversion<int>();
            e.Property(x => x.Price).HasColumnType("decimal(18,4)");
            e.HasIndex(x => new { x.SharedTeacherPackageId, x.ContentType, x.ContentId });
            e.HasOne(x => x.SharedTeacherPackage)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.SharedTeacherPackageId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Teacher)
                .WithMany(x => x.SharedPackageItems)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.SetNull);
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
            e.Property(o => o.ClaimedBy).HasMaxLength(120);
            e.Property(o => o.LastError).HasMaxLength(4000);
            e.Property(o => o.IsDeadLetter).HasDefaultValue(false);

            e.HasIndex(o => new { o.ProcessedAt, o.CreatedAt });
            e.HasIndex(o => new
            {
                o.ProcessedAt,
                o.IsDeadLetter,
                o.NextAttemptAt,
                o.LeaseExpiresAt,
                o.CreatedAt
            });
        });

        // WebVitalsMetric
        modelBuilder.Entity<WebVitalsMetric>(e =>
        {
            e.ToTable("web_vitals_metrics");
            e.HasKey(m => m.Id);
            e.Property(m => m.MetricId).HasMaxLength(64).IsRequired();
            e.Property(m => m.MetricName).HasMaxLength(32).IsRequired();
            e.Property(m => m.Rating).HasMaxLength(32).IsRequired();
            e.Property(m => m.RouteTemplate).HasMaxLength(180).IsRequired();
            e.Property(m => m.Surface).HasMaxLength(24).IsRequired();
            e.Property(m => m.DeviceClass).HasMaxLength(16).IsRequired();
            e.Property(m => m.ConnectionClass).HasMaxLength(24).IsRequired();
            e.Property(m => m.NavigationType).HasMaxLength(24).IsRequired();
            e.Property(m => m.ReleaseId).HasMaxLength(96).IsRequired();
            e.Property(m => m.CorrelationId).HasMaxLength(64);
            e.Property(m => m.PageUrl).HasMaxLength(512).IsRequired();
            e.Property(m => m.UserAgent).HasMaxLength(512).IsRequired();

            e.HasIndex(m => m.MetricName);
            e.HasIndex(m => m.CreatedAt);
            e.HasIndex(m => new
            {
                m.ReleaseId,
                m.RouteTemplate,
                m.Surface,
                m.DeviceClass,
                m.MetricName,
                m.CreatedAt
            });
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
            e.ToTable("digital_wallets", table =>
                table.HasCheckConstraint("CK_digital_wallets_current_balance_non_negative", "\"CurrentBalance\" >= 0"));
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
            e.HasIndex(rr => new { rr.WalletId, rr.Status, rr.Amount, rr.SenderPhoneNumber, rr.CreatedAt })
                .HasFilter("\"Status\" = 0");

            e.HasOne(rr => rr.User)
                .WithMany()
                .HasForeignKey(rr => rr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(rr => rr.Wallet)
                .WithMany(w => w.RechargeRequests)
                .HasForeignKey(rr => rr.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(rr => rr.Teacher)
                .WithMany()
                .HasForeignKey(rr => rr.TeacherId)
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
            e.HasIndex(rr => rr.TeacherId);
            e.Property(rr => rr.SenderPhoneNumber).HasMaxLength(20).IsRequired();
            e.Property(rr => rr.ScreenshotUrl).HasMaxLength(1000);
            e.Property(rr => rr.RejectionReason).HasMaxLength(500);
        });

        // IncomingSmsLog mapping
        modelBuilder.Entity<IncomingSmsLog>(e =>
        {
            e.ToTable("incoming_sms_logs", table =>
                table.HasCheckConstraint(
                    "CK_incoming_sms_logs_match_consistency",
                    "(\"IsMatched\" = FALSE AND \"MatchedRechargeRequestId\" IS NULL) OR (\"IsMatched\" = TRUE AND \"MatchedRechargeRequestId\" IS NOT NULL)"));
            e.HasKey(sms => sms.Id);

            e.HasOne(sms => sms.Wallet)
                .WithMany(w => w.IncomingSmsLogs)
                .HasForeignKey(sms => sms.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(sms => sms.DeduplicationHash).IsUnique();
            e.HasIndex(sms => sms.MatchedRechargeRequestId)
                .IsUnique()
                .HasFilter("\"MatchedRechargeRequestId\" IS NOT NULL");
            e.Property(sms => sms.Sender).HasMaxLength(100).IsRequired();
            e.Property(sms => sms.Body).HasMaxLength(1000).IsRequired();
            e.Property(sms => sms.DeduplicationHash).HasMaxLength(64).IsRequired();
            e.Property(sms => sms.ParsedAmount).HasPrecision(18, 2);
            e.Property(sms => sms.ParsedSenderPhone).HasMaxLength(20);
        });
    }

    private static void ConfigureHrLookup<TEntity>(ModelBuilder modelBuilder, string tableName)
        where TEntity : Domain.Common.BaseEntity
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey("Id");
            entity.Property<string>("Code").HasMaxLength(40).IsRequired();
            entity.Property<string>("Name").HasMaxLength(200).IsRequired();
            entity.HasIndex("Code").IsUnique();
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyContentIdentityRules();
        ApplyFinancialPrincipalSoftDelete();
        ApplyFinancialConcurrencyVersions();

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

        var securityStateUserIds = SecurityStateUserIds();
        if (_userSecurityStateCache is not null)
        {
            foreach (var userId in securityStateUserIds)
            {
                await _userSecurityStateCache.RemoveAsync(userId, cancellationToken);
            }
        }

        var savedEntityCount = await base.SaveChangesAsync(cancellationToken);

        if (_userSecurityStateCache is not null)
        {
            foreach (var userId in securityStateUserIds)
            {
                await _userSecurityStateCache.RemoveAsync(userId, cancellationToken);
            }
        }

        return savedEntityCount;
    }

    private Guid[] SecurityStateUserIds()
    {
        var userIds = ChangeTracker.Entries<User>()
            .Where(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted &&
                (entry.State == EntityState.Deleted ||
                 entry.Property(nameof(User.IsActive)).IsModified ||
                 entry.Property(nameof(User.PasswordResetVersion)).IsModified ||
                 entry.Property(nameof(User.SecurityStampVersion)).IsModified))
            .Select(entry => entry.Entity.Id);
        var userRoleIds = ChangeTracker.Entries<UserRole>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.UserId);
        return userIds.Concat(userRoleIds).Distinct().ToArray();
    }

    private void ApplyFinancialConcurrencyVersions()
    {
        foreach (var entry in ChangeTracker.Entries<StudentBalance>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.Version += 1;
        }

        foreach (var entry in ChangeTracker.Entries<TeacherAccount>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.Version += 1;
        }
    }

    private void ApplyFinancialPrincipalSoftDelete()
    {
        var deletedUsers = ChangeTracker.Entries<User>()
            .Where(entry => entry.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in deletedUsers)
        {
            var userId = entry.Entity.Id;
            if (!UserHasFinancialHistory(userId))
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsActive = false;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
            entry.Entity.SuspensionReason ??= "Soft-deleted because financial history exists.";
            entry.Entity.SecurityStampVersion += 1;
        }
    }

    private bool UserHasFinancialHistory(Guid userId)
    {
        return StudentBalances.Any(balance => balance.UserId == userId)
            || RechargeRequests.Any(request => request.UserId == userId || request.ResolvedByUserId == userId)
            || StudentAccessGrants.Any(grant => grant.UserId == userId || grant.CancelledByUserId == userId)
            || BalanceTransactions.Any(transaction => transaction.PerformedByUserId == userId)
            || TeacherAccounts.Any(account => account.Teacher.UserId == userId)
            || TeacherPayouts.Any(payout => payout.Teacher.UserId == userId || payout.HandledByUserId == userId)
            || AuditLogs.Any(log => log.PerformedByUserId == userId);
    }

    private void ApplyContentIdentityRules()
    {
        AssignOrValidateInternalCodes(ChangeTracker.Entries<Lesson>(), "LES");
        AssignOrValidateInternalCodes(ChangeTracker.Entries<LessonVideo>(), "VID");
        AssignOrValidateInternalCodes(ChangeTracker.Entries<Exam>(), "EXM");
    }

    private static void AssignOrValidateInternalCodes<TEntity>(
        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity>> entries,
        string prefix)
        where TEntity : Domain.Common.BaseEntity
    {
        foreach (var entry in entries)
        {
            var property = entry.Property("InternalCode");
            if (entry.State == EntityState.Added)
            {
                property.CurrentValue = $"{prefix}-{entry.Entity.Id:N}";
                continue;
            }

            if (entry.State == EntityState.Modified && property.IsModified)
            {
                if (!Equals(property.CurrentValue, property.OriginalValue))
                {
                    throw new InvalidOperationException("Internal content codes cannot be changed after creation.");
                }

                property.IsModified = false;
            }
        }
    }
}

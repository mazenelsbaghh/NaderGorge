using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.Assistant;
using NaderGorge.Domain.Entities.Gamification;
using NaderGorge.Domain.Entities.Homework;
using NaderGorge.Domain.Entities.Notifications;
using NaderGorge.Domain.Entities.Student;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Entities.AdminAI;

namespace NaderGorge.Domain.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<ReportDefinition> ReportDefinitions { get; }
    DbSet<StudentProfile> StudentProfiles { get; }
    DbSet<AcademicSubjectEligibility> AcademicSubjectEligibilities { get; }
    DbSet<StudentFacingAcademicScope> StudentFacingAcademicScopes { get; }
    DbSet<Device> Devices { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<CodeGroup> CodeGroups { get; }
    DbSet<AccessCode> AccessCodes { get; }
    DbSet<StudentAccessGrant> StudentAccessGrants { get; }
    DbSet<GiftIssuance> GiftIssuances { get; }
    DbSet<GiftRecipient> GiftRecipients { get; }
    DbSet<PromotionalBalanceAllocation> PromotionalBalanceAllocations { get; }
    DbSet<PromotionalBalanceUsage> PromotionalBalanceUsages { get; }
    DbSet<SalesRule> SalesRules { get; }
    DbSet<DiscountStackingPolicy> DiscountStackingPolicies { get; }
    DbSet<SalesCoupon> SalesCoupons { get; }
    DbSet<SalesCouponUsage> SalesCouponUsages { get; }
    DbSet<PrintableCodeBatch> PrintableCodeBatches { get; }
    DbSet<PrintableSalesCode> PrintableSalesCodes { get; }
    DbSet<PrintableCodeRedemption> PrintableCodeRedemptions { get; }
    DbSet<PrintableCodeTemplate> PrintableCodeTemplates { get; }
    DbSet<PublicExamProduct> PublicExamProducts { get; }
    DbSet<SalesFinancialEffect> SalesFinancialEffects { get; }

    // Content
    DbSet<Subject> Subjects { get; }
    DbSet<TeacherProfile> TeacherProfiles { get; }
    DbSet<TeacherStaffMember> TeacherStaffMembers { get; }
    DbSet<TeacherSubject> TeacherSubjects { get; }
    DbSet<Package> Packages { get; }
    DbSet<PackageCodePageProfile> PackageCodePageProfiles { get; }
    DbSet<ContentSection> ContentSections { get; }
    DbSet<Lesson> Lessons { get; }
    DbSet<LessonVideo> LessonVideos { get; }
    DbSet<VideoType> VideoTypes { get; }
    DbSet<BunnyStreamLibrary> BunnyStreamLibraries { get; }
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
    DbSet<ParentDeviceToken> ParentDeviceTokens { get; }

    // Student Notes
    DbSet<StudentNote> StudentNotes { get; }

    // Phase 2: HR Core
    DbSet<EmployeeProfile> EmployeeProfiles { get; }
    DbSet<HrIdempotencyRecord> HrIdempotencyRecords { get; }
    DbSet<HrModuleRollout> HrModuleRollouts { get; }
    DbSet<OrganizationUnit> OrganizationUnits { get; }
    DbSet<JobPosition> JobPositions { get; }
    DbSet<JobGrade> JobGrades { get; }
    DbSet<WorkLocation> WorkLocations { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<EmploymentAssignment> EmploymentAssignments { get; }
    DbSet<EmploymentContract> EmploymentContracts { get; }
    DbSet<WorkCalendar> WorkCalendars { get; }
    DbSet<ShiftTemplate> ShiftTemplates { get; }
    DbSet<ShiftSegment> ShiftSegments { get; }
    DbSet<ShiftAssignment> ShiftAssignments { get; }
    DbSet<ShiftSwapRequest> ShiftSwapRequests { get; }
    DbSet<AttendancePolicy> AttendancePolicies { get; }
    DbSet<AttendancePolicyAssignment> AttendancePolicyAssignments { get; }
    DbSet<TrustedAttendanceDevice> TrustedAttendanceDevices { get; }
    DbSet<AttendancePolicyException> AttendancePolicyExceptions { get; }
    DbSet<AttendanceAttempt> AttendanceAttempts { get; }
    DbSet<AttendanceSession> AttendanceSessions { get; }
    DbSet<AttendanceBreak> AttendanceBreaks { get; }
    DbSet<WorkdayClassification> WorkdayClassifications { get; }
    DbSet<AttendanceCorrection> AttendanceCorrections { get; }
    DbSet<LeaveType> LeaveTypes { get; }
    DbSet<LeavePolicy> LeavePolicies { get; }
    DbSet<LeaveBalance> LeaveBalances { get; }
    DbSet<LeaveLedgerEntry> LeaveLedgerEntries { get; }
    DbSet<HrLeaveRequest> HrLeaveRequests { get; }
    DbSet<ApprovalDefinition> ApprovalDefinitions { get; }
    DbSet<ApprovalDefinitionStep> ApprovalDefinitionSteps { get; }
    DbSet<ApprovalInstance> ApprovalInstances { get; }
    DbSet<ApprovalStepInstance> ApprovalStepInstances { get; }
    DbSet<ApprovalDelegation> ApprovalDelegations { get; }
    DbSet<PayComponent> PayComponents { get; }
    DbSet<PayrollRule> PayrollRules { get; }
    DbSet<EmployeeCompensation> EmployeeCompensations { get; }
    DbSet<HrPayrollRun> HrPayrollRuns { get; }
    DbSet<EmployeePayroll> EmployeePayrolls { get; }
    DbSet<PayrollLineItem> PayrollLineItems { get; }
    DbSet<Payslip> Payslips { get; }
    DbSet<PayrollSettlementAdjustment> PayrollSettlementAdjustments { get; }
    DbSet<HrFinancialRequest> HrFinancialRequests { get; }
    DbSet<HrFinancialInstallment> HrFinancialInstallments { get; }
    DbSet<HrPayrollInputSource> HrPayrollInputSources { get; }
    DbSet<EmployeeDocument> EmployeeDocuments { get; }
    DbSet<EmployeeDocumentVersion> EmployeeDocumentVersions { get; }
    DbSet<HrAsset> HrAssets { get; }
    DbSet<AssetCustody> AssetCustodies { get; }
    DbSet<PerformanceCycle> PerformanceCycles { get; }
    DbSet<PerformanceGoal> PerformanceGoals { get; }
    DbSet<PerformanceReview> PerformanceReviews { get; }
    DbSet<EmployeeCase> EmployeeCases { get; }
    DbSet<CaseEvidence> CaseEvidence { get; }
    DbSet<CaseResponse> CaseResponses { get; }
    DbSet<DisciplinaryAction> DisciplinaryActions { get; }
    DbSet<Requisition> Requisitions { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateInterview> CandidateInterviews { get; }
    DbSet<CandidateOffer> CandidateOffers { get; }
    DbSet<EmployeeLifecycleTask> EmployeeLifecycleTasks { get; }
    DbSet<OffboardingProcess> OffboardingProcesses { get; }
    DbSet<HrMigrationBatch> HrMigrationBatches { get; }
    DbSet<HrMigrationRecordMap> HrMigrationRecordMaps { get; }
    DbSet<HrMigrationConflict> HrMigrationConflicts { get; }
    DbSet<AttendanceLog> AttendanceLogs { get; }
    DbSet<TaskItem> TaskItems { get; }
    DbSet<TaskComment> TaskComments { get; }

    // Phase 5: Internal Chat
    DbSet<ChatRoom> ChatRooms { get; }
    DbSet<ChatParticipant> ChatParticipants { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<ChatMessageReadState> ChatMessageReadStates { get; }

    // Live Support Command Center
    DbSet<LiveSupportConversation> LiveSupportConversations { get; }
    DbSet<LiveSupportGuestSession> LiveSupportGuestSessions { get; }
    DbSet<LiveSupportStaffConfig> LiveSupportStaffConfigs { get; }
    DbSet<LiveSupportScheduleWindow> LiveSupportScheduleWindows { get; }
    DbSet<LiveSupportQueueEntry> LiveSupportQueueEntries { get; }
    DbSet<LiveSupportAssignment> LiveSupportAssignments { get; }
    DbSet<LiveSupportMessage> LiveSupportMessages { get; }
    DbSet<LiveSupportWhatsAppBinding> LiveSupportWhatsAppBindings { get; }
    DbSet<LiveSupportWhatsAppMessage> LiveSupportWhatsAppMessages { get; }
    DbSet<LiveSupportWhatsAppPendingReceipt> LiveSupportWhatsAppPendingReceipts { get; }
    DbSet<LiveSupportWhatsAppTemplate> LiveSupportWhatsAppTemplates { get; }
    DbSet<LiveSupportMessengerBinding> LiveSupportMessengerBindings { get; }
    DbSet<LiveSupportMessengerMessage> LiveSupportMessengerMessages { get; }
    DbSet<LiveSupportMessengerWebhookInbox> LiveSupportMessengerWebhookInbox { get; }
    DbSet<LiveSupportMessengerConfiguration> LiveSupportMessengerConfigurations { get; }
    DbSet<LiveSupportMessengerPage> LiveSupportMessengerPages { get; }
    DbSet<WhatsAppCampaign> WhatsAppCampaigns { get; }
    DbSet<WhatsAppCampaignRecipient> WhatsAppCampaignRecipients { get; }
    DbSet<WhatsAppContactPreference> WhatsAppContactPreferences { get; }
    DbSet<WhatsAppCampaignAuditEvent> WhatsAppCampaignAuditEvents { get; }
    DbSet<WhatsAppTemplateSyncRun> WhatsAppTemplateSyncRuns { get; }
    DbSet<LiveSupportAttachment> LiveSupportAttachments { get; }
    DbSet<LiveSupportStudentLinkHistory> LiveSupportStudentLinkHistories { get; }
    DbSet<LiveSupportEvent> LiveSupportEvents { get; }
    DbSet<LiveSupportActionExecution> LiveSupportActionExecutions { get; }
    DbSet<LiveSupportRating> LiveSupportRatings { get; }
    DbSet<LiveSupportAIPolicyVersion> LiveSupportAIPolicyVersions { get; }
    DbSet<LiveSupportAIKnowledgeEntry> LiveSupportAIKnowledgeEntries { get; }
    DbSet<LiveSupportAIKnowledgeRevision> LiveSupportAIKnowledgeRevisions { get; }
    DbSet<LiveSupportAIPolicyKnowledgeRevision> LiveSupportAIPolicyKnowledgeRevisions { get; }
    DbSet<LiveSupportAIConversationState> LiveSupportAIConversationStates { get; }
    DbSet<LiveSupportAITurn> LiveSupportAITurns { get; }
    DbSet<LiveSupportAIPendingAction> LiveSupportAIPendingActions { get; }
    DbSet<LiveSupportAIVerificationPolicyQuestion> LiveSupportAIVerificationPolicyQuestions { get; }
    DbSet<LiveSupportAIVerificationSession> LiveSupportAIVerificationSessions { get; }
    DbSet<LiveSupportAIVerificationAttempt> LiveSupportAIVerificationAttempts { get; }

    // Standalone Admin AI Agent
    DbSet<AdminAICapabilityBaseline> AdminAICapabilityBaselines { get; }
    DbSet<AdminAISensitiveDataPolicyVersion> AdminAISensitiveDataPolicyVersions { get; }
    DbSet<AdminAIConversation> AdminAIConversations { get; }
    DbSet<AdminAIConversationCommandReceipt> AdminAIConversationCommandReceipts { get; }
    DbSet<AdminAIMessage> AdminAIMessages { get; }
    DbSet<AdminAITurn> AdminAITurns { get; }
    DbSet<AdminAITurnStep> AdminAITurnSteps { get; }
    DbSet<AdminAIReadInvocation> AdminAIReadInvocations { get; }
    DbSet<AdminAIActionProposal> AdminAIActionProposals { get; }
    DbSet<AdminAIConfirmationChallenge> AdminAIConfirmationChallenges { get; }
    DbSet<AdminAISecureInputGrant> AdminAISecureInputGrants { get; }
    DbSet<AdminAIActionExecution> AdminAIActionExecutions { get; }
    DbSet<AdminAIActionExecutionItem> AdminAIActionExecutionItems { get; }
    DbSet<AdminAIAuditEvent> AdminAIAuditEvents { get; }

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
    DbSet<TeacherFinancialEvent> TeacherFinancialEvents { get; }
    DbSet<TeacherFinancialAllocation> TeacherFinancialAllocations { get; }
    DbSet<TeacherPayoutAdjustment> TeacherPayoutAdjustments { get; }
    DbSet<TeacherFinancialAgreement> TeacherFinancialAgreements { get; }
    DbSet<CodeGroupFinancialTerms> CodeGroupFinancialTerms { get; }
    DbSet<CodeGroupDeliveryConfirmation> CodeGroupDeliveryConfirmations { get; }
    DbSet<TeacherSettlement> TeacherSettlements { get; }
    DbSet<TeacherSettlementLine> TeacherSettlementLines { get; }
    DbSet<TeacherSettlementPayment> TeacherSettlementPayments { get; }
    DbSet<FinancialInvoice> FinancialInvoices { get; }
    DbSet<SharedTeacherPackage> SharedTeacherPackages { get; }
    DbSet<SharedTeacherPackageTeacher> SharedTeacherPackageTeachers { get; }
    DbSet<SharedTeacherPackageItem> SharedTeacherPackageItems { get; }
    DbSet<AccessCodeActivationLog> AccessCodeActivationLogs { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<WebVitalsMetric> WebVitalsMetrics { get; }

    // SMS Payment Auto-Matcher
    DbSet<DigitalWallet> DigitalWallets { get; }
    DbSet<RechargeRequest> RechargeRequests { get; }
    DbSet<IncomingSmsLog> IncomingSmsLogs { get; }
    DbSet<WalletTransferReview> WalletTransferReviews { get; }

    DbSet<FinancialAccount> FinancialAccounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<TreasuryAccount> TreasuryAccounts { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<ExpenseCategory> ExpenseCategories { get; }
    DbSet<FinanceCostCenter> FinanceCostCenters { get; }
    DbSet<FinanceVendor> FinanceVendors { get; }
    DbSet<PlatformExpense> PlatformExpenses { get; }
    DbSet<ExpensePayment> ExpensePayments { get; }
    DbSet<PlatformRefund> PlatformRefunds { get; }
    DbSet<FinanceBudgetPlan> FinanceBudgetPlans { get; }
    DbSet<FinanceBudgetLine> FinanceBudgetLines { get; }
    DbSet<TreasuryTransfer> TreasuryTransfers { get; }
    DbSet<TreasuryReconciliation> TreasuryReconciliations { get; }
    DbSet<FinancialProjectionCheckpoint> FinancialProjectionCheckpoints { get; }
    DbSet<FinancialMigrationBatch> FinancialMigrationBatches { get; }
    DbSet<FinancialMigrationItem> FinancialMigrationItems { get; }
    DbSet<FinancialMigrationException> FinancialMigrationExceptions { get; }

    Task<StudentAnswer?> FindStudentAnswerAsync(Guid studentExamAttemptId, Guid examQuestionId, CancellationToken cancellationToken = default);
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> Entry<T>(T entity) where T : class;
    void ClearTrackedChanges();
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task AcquireVideoPlaybackLockAsync(Guid userId, Guid lessonVideoId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

using System.Security.Cryptography;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIReviewedActionPostgresTests
{
    [Fact]
    public async Task RealPostgres_ReviewedActionCandidates_PersistOneEffectPerConfirmedProposal()
    {
        await using var fixture = await PostgresAdminAIFixture.CreateAsync();
        await using var db = fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var adminRole = await db.Roles.FirstAsync(item => item.Type == RoleType.Admin);
        var actor = new User { FullName = "Admin", PhoneNumber = "01000000993", PasswordHash = "test" };
        var student = new User { FullName = "Student", PhoneNumber = "01000000994", PasswordHash = "test" };
        var baseline = new AdminAICapabilityBaseline
        {
            Version = "candidate-flow", ManifestHash = new string('a', 64),
            SourceRevision = "test", RuntimeInventoryHash = new string('b', 64),
            FrontendInventoryHash = new string('c', 64), Status = AdminAICapabilityBaselineStatus.Active
        };
        var policyVersion = new AdminAISensitiveDataPolicyVersion
        {
            Version = "candidate-flow", PolicyHash = new string('d', 64),
            Status = AdminAISensitiveDataPolicyStatus.Active
        };
        var conversation = new AdminAIConversation { OwnerAdminUserId = actor.Id, Title = "PostgreSQL flow" };
        var message = new AdminAIMessage
        {
            ConversationId = conversation.Id, Sequence = 1, Role = AdminAIMessageRole.Admin,
            Content = "Create note and subject"
        };
        var turn = new AdminAITurn
        {
            ActorAdminUserId = actor.Id, ConversationId = conversation.Id, SourceMessageId = message.Id,
            CapabilityBaselineId = baseline.Id, SensitiveDataPolicyVersionId = policyVersion.Id,
            CallbackIdempotencyDigest = new string('e', 64)
        };
        var pipeline = new MediaProductionPipeline { Title = "Lesson production", Stage = MediaStage.Review };
        var task = new TaskItem
        {
            Title = "Review lesson", Description = "Check content", AssigneeId = actor.Id,
            CreatedById = actor.Id, MediaPipelineId = pipeline.Id
        };
        var teacher = new TeacherProfile
        {
            User = new User { FullName = "Teacher", PhoneNumber = "01000000995", PasswordHash = "test" },
            IsContentVisibleToStudents = true
        };
        var lesson = new Lesson
        {
            Title = "Discussion",
            ContentSection = new ContentSection
            {
                Title = "Section",
                Term = new Term
                {
                    Title = "Term",
                    Package = new Package
                    {
                        Name = "Moderation", Teacher = teacher,
                        Subject = new Subject { Name = "Moderation", NormalizedName = "MODERATION" }
                    }
                }
            }
        };
        var parentComment = new LessonComment
        {
            Lesson = lesson, AuthorUser = student, Body = "Original question", Status = LessonCommentStatus.Pending
        };
        var replyComment = new LessonComment
        {
            Lesson = lesson, ParentComment = parentComment, AuthorUser = student,
            Body = "Follow-up question", Status = LessonCommentStatus.Pending
        };
        var scopedPost = new CommunityPost
        {
            AuthorUser = student, Body = "A poll for all students", IsPoll = true,
            Status = CommunityPostStatus.Pending
        };
        var pollOption = new CommunityPostPollOption { Post = scopedPost, Text = "Option A" };
        scopedPost.PollOptions.Add(pollOption);
        scopedPost.PollOptions.Add(new CommunityPostPollOption { Post = scopedPost, Text = "Option B" });
        var teacherPost = new CommunityPost
        {
            AuthorUser = student, Teacher = teacher, Body = "A teacher post", Status = CommunityPostStatus.Pending
        };
        var communityParent = new CommunityPostComment
        {
            Post = teacherPost, AuthorUser = student, Body = "Original community comment",
            Status = CommunityCommentStatus.Pending
        };
        var communityReply = new CommunityPostComment
        {
            Post = teacherPost, ParentComment = communityParent, AuthorUser = student,
            Body = "Community reply", Status = CommunityCommentStatus.Pending
        };
        var recordedType = new VideoType { Name = "Recorded", NormalizedName = "RECORDED" };
        var lessonVideo = new LessonVideo
        {
            Title = "Recorded lesson", Lesson = lesson, VideoType = recordedType,
            Provider = "youtube", ProviderVideoId = "recorded-test", MaxWatchCount = 3
        };
        var watchEvent = new VideoWatchEvent
        {
            User = student, LessonVideo = lessonVideo, WatchCount = 3,
            IsLocked = true, CustomMaxWatchCount = 3
        };
        var extraWatchRequest = new ExtraWatchRequest
        {
            User = student, LessonVideo = lessonVideo, Status = RequestStatus.Pending,
            RequestReason = "Need another view"
        };
        db.AddRange(actor, student, baseline, policyVersion, conversation, message, turn,
            new UserRole { User = actor, Role = adminRole }, pipeline, task,
            parentComment, replyComment, scopedPost, teacherPost, communityParent, communityReply,
            lessonVideo, watchEvent, extraWatchRequest);
        await db.SaveChangesAsync();
        var financialEvent = new TeacherFinancialEvent
        {
            SourceType = TeacherFinancialSourceType.ManualCompensation,
            SourceId = Guid.NewGuid(), TargetType = SalesTargetType.Package,
            TargetId = Guid.NewGuid(), PaidAmount = 25m,
            IdempotencyKey = $"admin-ai-review-{Guid.NewGuid():N}",
            ReviewStatus = TeacherFinancialReviewStatus.PendingReview
        };
        var financialAllocation = new TeacherFinancialAllocation
        {
            TeacherFinancialEvent = financialEvent, TeacherId = teacher.Id,
            TeacherShareAmount = 25m, ContentNameSnapshot = "Reviewed payment",
            ReviewStatus = TeacherFinancialReviewStatus.PendingReview
        };
        db.TeacherFinancialAllocations.Add(financialAllocation);
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddSingleton<IAppDbContext>(db)
            .AddSingleton<IAcademicScopeService>(new AcademicScopeService(db))
            .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly))
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IMediator>();
        var preview = new AdminAIOrdinaryPreviewSource(
            new AdminAIIdentityContentPreviewSource(db), new AdminAIOperationsPreviewSource(db),
            new AdminAIAssessmentPreviewSource(db, new AcademicScopeService(db)),
            new AdminAITeacherFinancialReviewPreviewSource(db));
        IAdminAIActionCapability[] adapters =
        [
            new AdminAIAddStudentNoteAction(mediator, preview),
            new AdminAICreateSubjectAction(mediator, preview),
            new AdminAIUpdateSubjectAction(mediator, preview),
            new AdminAICreateVideoTypeAction(mediator, preview),
            new AdminAIUpdateVideoTypeAction(mediator, preview),
            new AdminAIAddTaskCommentAction(mediator, preview),
            new AdminAIUpdateTaskStatusAction(mediator, preview),
            new AdminAIResolveTaskApprovalAction(mediator, preview),
            new AdminAIApproveLessonCommentAction(mediator, preview),
            new AdminAIApproveCommunityPostAction(mediator, preview),
            new AdminAIApproveCommunityCommentAction(mediator, preview),
            new AdminAIApproveWatchRequestAction(mediator, preview),
            new AdminAIReviewTeacherFinancialAllocationAction(mediator, preview)
        ];
        var registry = new AdminAICapabilityRegistry(
            [.. AdminAIIdentityContentActionCatalog.CreateCandidates(),
                .. AdminAIOperationsActionCatalog.CreateCandidates(),
                .. AdminAIAssessmentActionCatalog.CreateCandidates(),
                .. AdminAIIdentityHighRiskActionCatalog.CreateCandidates(),
                .. AdminAITeacherFinancialReviewActionCatalog.CreateCandidates()]);
        Assert.Equal(11, AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(registry, adapters[..11]).Count);
        Assert.Equal(2, AdminAIActionCapabilityRegistration.ValidateHighRiskCoverage(registry, adapters[11..]).Count);
        var access = new AdminAIAccessGate(db);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminAI:HmacKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        var protector = new AdminAIDataProtector(new EphemeralDataProtectionProvider(), config);
        var challenges = new AdminAIConfirmationChallengeService(db, protector);
        var builder = new AdminAIProposalBuilder(db, access, registry, protector,
            new AdminAISensitiveDataPolicy(), challenges, adapters, config);
        var secureInputs = new AdminAISecureInputService(db, access, protector, config);
        var executor = new AdminAIActionExecutor(db, access, protector, secureInputs, adapters);
        var commands = new AdminAIProposalCommands(db, access, challenges, executor);

        async Task ConfirmAsync(string key, object input)
        {
            var proposal = await builder.BuildAsync(actor.Id, turn.Id, key, input, default);
            Assert.Equal(AdminAIProposalStatus.PendingConfirmation, proposal.Status);
            var intent = $"intent-{proposal.Id:N}";
            var result = await commands.ConfirmAsync(actor.Id, proposal.Id,
                proposal.Version, null, intent, default);
            Assert.Equal(AdminAIExecutionStatus.Succeeded, result.Status);
        }

        async Task ConfirmStrongAsync(string key, object input)
        {
            var proposal = await builder.BuildAsync(actor.Id, turn.Id, key, input, default);
            Assert.Equal(AdminAIConfirmationType.TypedStrong, proposal.Confirmation);
            Assert.False(string.IsNullOrWhiteSpace(proposal.StrongPhrase));
            var result = await commands.ConfirmAsync(actor.Id, proposal.Id,
                proposal.Version, proposal.StrongPhrase, $"intent-{proposal.Id:N}", default);
            Assert.Equal(AdminAIExecutionStatus.Succeeded, result.Status);
        }

        var noteProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[0].Key,
            new { studentId = student.Id, content = "Durable note", isPinned = false }, default);
        Assert.Empty(await db.StudentNotes.ToListAsync());
        var noteIntent = $"intent-{noteProposal.Id:N}";
        var noteResult = await commands.ConfirmAsync(actor.Id, noteProposal.Id,
            noteProposal.Version, null, noteIntent, default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, noteResult.Status);

        await ConfirmAsync(adapters[1].Key, new { name = "Physics", description = "New subject" });
        var subjectId = await db.Subjects.Where(item => item.Name == "Physics")
            .Select(item => item.Id).SingleAsync();
        await ConfirmAsync(adapters[2].Key,
            new { subjectId, name = "Advanced Physics", description = "Updated" });
        await ConfirmAsync(adapters[3].Key,
            new { name = "Lecture", sortOrder = 1, isActive = true });
        var videoTypeId = await db.VideoTypes.Where(item => item.Name == "Lecture")
            .Select(item => item.Id).SingleAsync();
        await ConfirmAsync(adapters[4].Key,
            new { videoTypeId, name = "Revision", sortOrder = 2 });
        var commentInput = new { taskId = task.Id, content = "Reviewed the lesson" };
        var staleCommentProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[5].Key,
            commentInput, default);
        Assert.Empty(await db.TaskComments.ToListAsync());
        task.Status = NaderGorge.Domain.Enums.TaskStatus.InProgress;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            staleCommentProposal.Id, staleCommentProposal.Version, null,
            $"intent-{staleCommentProposal.Id:N}", default));
        Assert.Empty(await db.TaskComments.ToListAsync());
        Assert.Equal(AdminAIProposalStatus.Invalidated,
            (await db.AdminAIActionProposals.AsNoTracking().SingleAsync(item => item.Id == staleCommentProposal.Id)).Status);
        await ConfirmAsync(adapters[5].Key, commentInput);
        await ConfirmAsync(adapters[6].Key,
            new { taskId = task.Id, status = (int)NaderGorge.Domain.Enums.TaskStatus.Review });
        await ConfirmAsync(adapters[7].Key,
            new { taskId = task.Id, approve = true });
        var approvedTask = await db.TaskItems.AsNoTracking().SingleAsync(item => item.Id == task.Id);
        Assert.Equal(NaderGorge.Domain.Enums.TaskStatus.Completed, approvedTask.Status);
        Assert.Equal(actor.Id, approvedTask.ApprovedById);
        Assert.NotNull(approvedTask.CompletedAt);
        Assert.Equal(MediaStage.Approved,
            (await db.MediaProductionPipelines.AsNoTracking().SingleAsync(item => item.Id == pipeline.Id)).Stage);
        await ConfirmAsync(adapters[6].Key,
            new { taskId = task.Id, status = (int)NaderGorge.Domain.Enums.TaskStatus.Review });
        await ConfirmAsync(adapters[7].Key,
            new { taskId = task.Id, approve = false, rejectionReason = "Needs another pass" });
        await Assert.ThrowsAsync<AdminAIActionPreviewUnavailableException>(() => builder.BuildAsync(
            actor.Id, turn.Id, adapters[8].Key, new { commentId = replyComment.Id }, default));
        var pendingCommentProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[8].Key,
            new { commentId = parentComment.Id }, default);
        parentComment.Body = "Edited original question";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            pendingCommentProposal.Id, pendingCommentProposal.Version, null,
            $"intent-{pendingCommentProposal.Id:N}", default));
        Assert.Equal(LessonCommentStatus.Pending,
            (await db.LessonComments.AsNoTracking().SingleAsync(item => item.Id == parentComment.Id)).Status);
        await ConfirmAsync(adapters[8].Key, new { commentId = parentComment.Id });
        await ConfirmAsync(adapters[8].Key, new { commentId = replyComment.Id });
        var unpublishedParentApproval = await mediator.Send(
            new ApproveCommunityCommentCommand(communityParent.Id, actor.Id));
        Assert.False(unpublishedParentApproval.Success);
        Assert.Contains("POST_NOT_APPROVED", unpublishedParentApproval.Errors!);
        await Assert.ThrowsAsync<AdminAIActionPreviewUnavailableException>(() => builder.BuildAsync(
            actor.Id, turn.Id, adapters[9].Key, new { postId = scopedPost.Id }, default));
        var academicScope = new StudentFacingAcademicScope
        {
            OwnerType = StudentFacingScopeOwnerType.CommunityPost,
            OwnerId = scopedPost.Id, ScopeLevel = AcademicScopeLevel.PlatformWide
        };
        db.StudentFacingAcademicScopes.Add(academicScope);
        await db.SaveChangesAsync();
        var pendingPostProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[9].Key,
            new { postId = scopedPost.Id }, default);
        academicScope.ScopeLevel = AcademicScopeLevel.StageWide;
        academicScope.EducationStage = EducationStage.Secondary;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            pendingPostProposal.Id, pendingPostProposal.Version, null,
            $"intent-{pendingPostProposal.Id:N}", default));
        Assert.Equal(CommunityPostStatus.Pending,
            (await db.CommunityPosts.AsNoTracking().SingleAsync(item => item.Id == scopedPost.Id)).Status);
        academicScope.ScopeLevel = AcademicScopeLevel.PlatformWide;
        academicScope.EducationStage = null;
        await db.SaveChangesAsync();
        var pendingPollProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[9].Key,
            new { postId = scopedPost.Id }, default);
        pollOption.Text = "Edited option A";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            pendingPollProposal.Id, pendingPollProposal.Version, null,
            $"intent-{pendingPollProposal.Id:N}", default));
        await ConfirmAsync(adapters[9].Key, new { postId = scopedPost.Id });
        await ConfirmAsync(adapters[9].Key, new { postId = teacherPost.Id });
        var prematureReplyApproval = await mediator.Send(
            new ApproveCommunityCommentCommand(communityReply.Id, actor.Id));
        Assert.False(prematureReplyApproval.Success);
        Assert.Contains("PARENT_NOT_APPROVED", prematureReplyApproval.Errors!);
        await Assert.ThrowsAsync<AdminAIActionPreviewUnavailableException>(() => builder.BuildAsync(
            actor.Id, turn.Id, adapters[10].Key, new { commentId = communityReply.Id }, default));
        var pendingCommunityCommentProposal = await builder.BuildAsync(actor.Id, turn.Id,
            adapters[10].Key, new { commentId = communityParent.Id }, default);
        communityParent.Body = "Edited community comment";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            pendingCommunityCommentProposal.Id, pendingCommunityCommentProposal.Version, null,
            $"intent-{pendingCommunityCommentProposal.Id:N}", default));
        await ConfirmAsync(adapters[10].Key, new { commentId = communityParent.Id });
        await ConfirmAsync(adapters[10].Key, new { commentId = communityReply.Id });
        var repeatedParentApproval = await mediator.Send(
            new ApproveCommunityCommentCommand(communityParent.Id, actor.Id));
        Assert.False(repeatedParentApproval.Success);
        Assert.Contains("ALREADY_RESOLVED", repeatedParentApproval.Errors!);
        var overflowApproval = await mediator.Send(new ApproveWatchRequestCommand(
            extraWatchRequest.Id, actor.Id, "Too many", int.MaxValue));
        Assert.False(overflowApproval.Success);
        Assert.Contains("WATCH_LIMIT_OVERFLOW", overflowApproval.Errors!);
        var longReasonApproval = await mediator.Send(new ApproveWatchRequestCommand(
            extraWatchRequest.Id, actor.Id, new string('x', 1001), 1));
        Assert.False(longReasonApproval.Success);
        Assert.Contains("REASON_TOO_LONG", longReasonApproval.Errors!);
        Assert.Equal(RequestStatus.Pending,
            (await db.ExtraWatchRequests.AsNoTracking().SingleAsync(item => item.Id == extraWatchRequest.Id)).Status);
        var staleWatchProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[11].Key,
            new { requestId = extraWatchRequest.Id, addedViews = 2 }, default);
        Assert.Empty(await db.VideoOverrides.ToListAsync());
        watchEvent.CustomMaxWatchCount = 4;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            staleWatchProposal.Id, staleWatchProposal.Version, staleWatchProposal.StrongPhrase,
            $"intent-{staleWatchProposal.Id:N}", default));
        Assert.Empty(await db.VideoOverrides.ToListAsync());
        await ConfirmStrongAsync(adapters[11].Key,
            new { requestId = extraWatchRequest.Id, addedViews = 2 });
        await ConfirmStrongAsync(adapters[11].Key,
            new { requestId = extraWatchRequest.Id, addedViews = 1 });
        var financialInput = new { allocationId = financialAllocation.Id,
            status = (int)TeacherFinancialReviewStatus.Approved, note = "Verified source" };
        var staleFinanceProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[12].Key,
            financialInput, default);
        financialAllocation.ContentNameSnapshot = "Updated source label";
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ConfirmAsync(actor.Id,
            staleFinanceProposal.Id, staleFinanceProposal.Version, staleFinanceProposal.StrongPhrase,
            $"intent-{staleFinanceProposal.Id:N}", default));
        Assert.Empty(await db.TeacherAccounts.Where(item => item.TeacherId == teacher.Id).ToListAsync());
        var financeProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[12].Key,
            financialInput, default);
        Assert.Equal(AdminAIConfirmationType.TypedStrong, financeProposal.Confirmation);
        var financeResult = await commands.ConfirmAsync(actor.Id, financeProposal.Id,
            financeProposal.Version, financeProposal.StrongPhrase,
            $"intent-{financeProposal.Id:N}", default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, financeResult.Status);

        await using var replayDb = fixture.CreateDbContext();
        var replayPreview = new AdminAIIdentityContentPreviewSource(replayDb);
        var replayAdapter = new AdminAIAddStudentNoteAction(mediator, replayPreview);
        var replayAccess = new AdminAIAccessGate(replayDb);
        var replayExecutor = new AdminAIActionExecutor(replayDb, replayAccess, protector,
            new AdminAISecureInputService(replayDb, replayAccess, protector, config), [replayAdapter]);
        var replay = await replayExecutor.ExecuteAsync(actor.Id, noteProposal.Id, noteIntent, default);
        Assert.Equal(noteResult.Id, replay.Id);

        await using var verifyDb = fixture.CreateDbContext();
        var note = Assert.Single(await verifyDb.StudentNotes.AsNoTracking().ToListAsync());
        Assert.Equal(student.Id, note.StudentId);
        Assert.Equal(actor.Id, note.AdminId);
        Assert.Equal("Durable note", note.Content);
        var subject = await verifyDb.Subjects.AsNoTracking().SingleAsync(item => item.Id == subjectId);
        Assert.Equal("Advanced Physics", subject.Name);
        Assert.Equal("Updated", subject.Description);
        var videoType = await verifyDb.VideoTypes.AsNoTracking()
            .SingleAsync(item => item.Id == videoTypeId);
        Assert.Equal("Revision", videoType.Name);
        Assert.Equal(2, videoType.SortOrder);
        var comments = await verifyDb.TaskComments.AsNoTracking().OrderBy(item => item.CreatedAt).ToListAsync();
        Assert.Equal(2, comments.Count);
        var comment = comments.Single(item => item.Content == "Reviewed the lesson");
        Assert.Equal(task.Id, comment.TaskId);
        Assert.Equal(actor.Id, comment.UserId);
        Assert.Contains(comments, item => item.Content.Contains("Needs another pass", StringComparison.Ordinal));
        var finalTask = await verifyDb.TaskItems.AsNoTracking().SingleAsync(item => item.Id == task.Id);
        Assert.Equal(NaderGorge.Domain.Enums.TaskStatus.InProgress, finalTask.Status);
        Assert.Null(finalTask.ApprovedById);
        Assert.Null(finalTask.CompletedAt);
        Assert.Equal(MediaStage.Editing,
            (await verifyDb.MediaProductionPipelines.AsNoTracking().SingleAsync(item => item.Id == pipeline.Id)).Stage);
        Assert.Equal(2, await verifyDb.LessonComments.AsNoTracking()
            .CountAsync(item => item.Status == LessonCommentStatus.Approved));
        Assert.Equal(4, await verifyDb.OutboxEvents.AsNoTracking()
            .CountAsync(item => item.Type == "LessonCommentApproved"));
        Assert.Equal(2, await verifyDb.CommunityPosts.AsNoTracking()
            .CountAsync(item => item.Status == CommunityPostStatus.Approved));
        Assert.Equal("Edited option A", (await verifyDb.CommunityPostPollOptions.AsNoTracking()
            .SingleAsync(item => item.Id == pollOption.Id)).Text);
        Assert.Equal(2, await verifyDb.OutboxEvents.AsNoTracking()
            .CountAsync(item => item.Type == "CommunityPostApproved"));
        Assert.Equal(2, await verifyDb.CommunityPostComments.AsNoTracking()
            .CountAsync(item => item.Status == CommunityCommentStatus.Approved));
        Assert.Equal(2, await verifyDb.OutboxEvents.AsNoTracking()
            .CountAsync(item => item.Type == "CommunityCommentApproved"));
        Assert.Equal(RequestStatus.Approved,
            (await verifyDb.ExtraWatchRequests.AsNoTracking()
                .SingleAsync(item => item.Id == extraWatchRequest.Id)).Status);
        var finalWatch = await verifyDb.VideoWatchEvents.AsNoTracking()
            .SingleAsync(item => item.Id == watchEvent.Id);
        Assert.False(finalWatch.IsLocked);
        Assert.Equal(7, finalWatch.CustomMaxWatchCount);
        Assert.Equal(-1, finalWatch.TimeWatchedInSeconds);
        Assert.Equal(new[] { 6, 7 }, await verifyDb.VideoOverrides.AsNoTracking()
            .OrderBy(item => item.NewLimit).Select(item => item.NewLimit).ToArrayAsync());
        var firstApproval = await verifyDb.VideoOverrides.AsNoTracking()
            .SingleAsync(item => item.NewLimit == 6);
        Assert.Equal(extraWatchRequest.Id, firstApproval.WatchRequestId);
        Assert.False(string.IsNullOrWhiteSpace(firstApproval.OperationId));
        var approvalExecution = await verifyDb.AdminAIActionExecutions.AsNoTracking()
            .SingleAsync(item => item.ExternalOperationId == firstApproval.OperationId);
        Assert.Equal("admin.identity.watch-request.approve", approvalExecution.CapabilityKey);
        var resolver = new AdminAIWatchRequestApprovalResultResolver(verifyDb);
        var recovered = await resolver.ResolveAsync(firstApproval.OperationId!, approvalExecution.Id.ToString("N"), default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, recovered?.Status);
        Assert.Null(await resolver.ResolveAsync(firstApproval.OperationId!, Guid.NewGuid().ToString("N"), default));
        var replayedApproval = await mediator.Send(new ApproveWatchRequestCommand(
            extraWatchRequest.Id, actor.Id, null, 2, firstApproval.OperationId));
        Assert.True(replayedApproval.Success);
        var conflictedApproval = await mediator.Send(new ApproveWatchRequestCommand(
            extraWatchRequest.Id, actor.Id, null, 3, firstApproval.OperationId));
        Assert.False(conflictedApproval.Success);
        Assert.Contains("IDEMPOTENCY_CONFLICT", conflictedApproval.Errors!);
        Assert.Equal(2, await verifyDb.VideoOverrides.AsNoTracking().CountAsync());
        Assert.Equal(7, (await verifyDb.VideoWatchEvents.AsNoTracking()
            .SingleAsync(item => item.Id == watchEvent.Id)).CustomMaxWatchCount);
        var reviewedAllocation = await verifyDb.TeacherFinancialAllocations.AsNoTracking()
            .SingleAsync(item => item.Id == financialAllocation.Id);
        Assert.Equal(TeacherFinancialReviewStatus.Approved, reviewedAllocation.ReviewStatus);
        Assert.Equal(actor.Id, reviewedAllocation.ReviewActorUserId);
        Assert.Equal("Verified source", reviewedAllocation.ReviewNote);
        Assert.Equal(financeResult.Id.ToString("N"), reviewedAllocation.ReviewOperationId);
        Assert.Equal(25m, (await verifyDb.TeacherAccounts.AsNoTracking()
            .SingleAsync(item => item.TeacherId == teacher.Id)).CurrentBalance);
        var financeResolver = new AdminAITeacherFinancialReviewResultResolver(verifyDb);
        Assert.Equal(AdminAIExecutionStatus.Succeeded,
            (await financeResolver.ResolveAsync(reviewedAllocation.ReviewOperationId!,
                financeResult.Id.ToString("N"), default))?.Status);
        Assert.Null(await financeResolver.ResolveAsync(reviewedAllocation.ReviewOperationId!,
            Guid.NewGuid().ToString("N"), default));
        var recoveringFinanceExecution = await verifyDb.AdminAIActionExecutions
            .SingleAsync(item => item.Id == financeResult.Id);
        var recoveringFinanceProposal = await verifyDb.AdminAIActionProposals
            .SingleAsync(item => item.Id == recoveringFinanceExecution.ProposalId);
        recoveringFinanceExecution.Status = AdminAIExecutionStatus.RecoveryRequired;
        recoveringFinanceExecution.CompletedAt = null;
        recoveringFinanceProposal.Status = AdminAIProposalStatus.RecoveryRequired;
        recoveringFinanceProposal.CompletedAt = null;
        await verifyDb.SaveChangesAsync();
        Assert.Equal(1, await new AdminAIExternalOperationReconciler(verifyDb, [financeResolver])
            .ReconcileAsync(100, default));
        Assert.Equal(AdminAIExecutionStatus.Succeeded, recoveringFinanceExecution.Status);
        Assert.Equal(AdminAIProposalStatus.Succeeded, recoveringFinanceProposal.Status);
        Assert.Equal(25m, (await verifyDb.TeacherAccounts.AsNoTracking()
            .SingleAsync(item => item.TeacherId == teacher.Id)).CurrentBalance);
        var recoveringExecution = await verifyDb.AdminAIActionExecutions
            .SingleAsync(item => item.Id == approvalExecution.Id);
        var recoveringProposal = await verifyDb.AdminAIActionProposals
            .SingleAsync(item => item.Id == recoveringExecution.ProposalId);
        recoveringExecution.Status = AdminAIExecutionStatus.RecoveryRequired;
        recoveringExecution.CompletedAt = null;
        recoveringProposal.Status = AdminAIProposalStatus.RecoveryRequired;
        recoveringProposal.CompletedAt = null;
        await verifyDb.SaveChangesAsync();
        var reconciler = new AdminAIExternalOperationReconciler(verifyDb, [resolver]);
        Assert.Equal(1, await reconciler.ReconcileAsync(100, default));
        Assert.Equal(AdminAIExecutionStatus.Succeeded, recoveringExecution.Status);
        Assert.Equal(AdminAIProposalStatus.Succeeded, recoveringProposal.Status);
        await verifyDb.Entry(recoveringExecution).ReloadAsync();
        Assert.Equal(approvalExecution.SafeResultJson, recoveringExecution.SafeResultJson);
        Assert.Equal(2, await verifyDb.VideoOverrides.AsNoTracking().CountAsync());
        Assert.Equal(4, await verifyDb.OutboxEvents.AsNoTracking()
            .CountAsync(item => item.Type == "ExtraWatchRequestUpdated"));
        var subjectUpdate = await verifyDb.AdminAIActionExecutions.AsNoTracking()
            .SingleAsync(item => item.CapabilityKey == "admin.content.subject.update");
        using var subjectUpdateResult = System.Text.Json.JsonDocument.Parse(subjectUpdate.SafeResultJson);
        Assert.True(subjectUpdateResult.RootElement.GetProperty("updated").GetBoolean());
        Assert.Equal(19, await verifyDb.AdminAIActionExecutions.CountAsync());
    }

}

using System.Security.Cryptography;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.AdminAI.Catalog;
using NaderGorge.Application.Features.AdminAI.Commands;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Application.Features.AdminAI.Security;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.AdminAI;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Infrastructure.Services.AdminAI;
using NaderGorge.Infrastructure.Services.AdminAI.Actions;

namespace NaderGorge.Integration.Tests.AdminAI;

public sealed class AdminAIIdentityContentPostgresTests
{
    [Fact]
    public async Task RealPostgres_OrdinaryIdentityContentAndOperations_PersistOneEffectPerConfirmedProposal()
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
        db.AddRange(actor, student, baseline, policyVersion, conversation, message, turn,
            new UserRole { User = actor, Role = adminRole }, pipeline, task);
        await db.SaveChangesAsync();

        using var services = new ServiceCollection()
            .AddSingleton<IAppDbContext>(db)
            .AddMediatR(config => config.RegisterServicesFromAssembly(typeof(ApiResponse).Assembly))
            .BuildServiceProvider();
        var mediator = services.GetRequiredService<IMediator>();
        var preview = new AdminAIOrdinaryPreviewSource(
            new AdminAIIdentityContentPreviewSource(db), new AdminAIOperationsPreviewSource(db));
        IAdminAIActionCapability[] adapters =
        [
            new AdminAIAddStudentNoteAction(mediator, preview),
            new AdminAICreateSubjectAction(mediator, preview),
            new AdminAIUpdateSubjectAction(mediator, preview),
            new AdminAICreateVideoTypeAction(mediator, preview),
            new AdminAIUpdateVideoTypeAction(mediator, preview),
            new AdminAIAddTaskCommentAction(mediator, preview),
            new AdminAIUpdateTaskStatusAction(mediator, preview),
            new AdminAIResolveTaskApprovalAction(mediator, preview)
        ];
        var registry = new AdminAICapabilityRegistry(
            [.. AdminAIIdentityContentActionCatalog.CreateCandidates(), .. AdminAIOperationsActionCatalog.CreateCandidates()]);
        Assert.Equal(8, AdminAIActionCapabilityRegistration.ValidateOrdinaryCoverage(registry, adapters).Count);
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

        var noteProposal = await builder.BuildAsync(actor.Id, turn.Id, adapters[0].Key,
            new { studentId = student.Id, content = "Durable note", isPinned = false }, default);
        Assert.Empty(await db.StudentNotes.ToListAsync());
        var noteIntent = $"intent-{noteProposal.Id:N}";
        var noteResult = await commands.ConfirmAsync(actor.Id, noteProposal.Id,
            noteProposal.Version, null, noteIntent, default);
        Assert.Equal(AdminAIExecutionStatus.Succeeded, noteResult.Status);

        await ConfirmAsync(adapters[1].Key, new { name = "Physics", description = "New subject" });
        var subjectId = await db.Subjects.Select(item => item.Id).SingleAsync();
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
        var subject = await verifyDb.Subjects.AsNoTracking().SingleAsync();
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
        var subjectUpdate = await verifyDb.AdminAIActionExecutions.AsNoTracking()
            .SingleAsync(item => item.CapabilityKey == "admin.content.subject.update");
        using var subjectUpdateResult = System.Text.Json.JsonDocument.Parse(subjectUpdate.SafeResultJson);
        Assert.True(subjectUpdateResult.RootElement.GetProperty("updated").GetBoolean());
        Assert.Equal(10, await verifyDb.AdminAIActionExecutions.CountAsync());
    }

}

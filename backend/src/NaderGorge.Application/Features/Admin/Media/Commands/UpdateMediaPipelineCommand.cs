using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;
using TaskStatus = NaderGorge.Domain.Enums.TaskStatus;

namespace NaderGorge.Application.Features.Admin.Media.Commands;

public record UpdateMediaPipelineCommand(
    Guid Id,
    Guid UserId,
    string Title,
    string? Description,
    Guid? AssignedAgentId,
    string? AssetFolderUrl,
    int EditingErrorCount,
    MediaStage Stage,
    Guid? SupervisorId
) : IRequest<ApiResponse>;

public class UpdateMediaPipelineCommandValidator : AbstractValidator<UpdateMediaPipelineCommand>
{
    public UpdateMediaPipelineCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssetFolderUrl).MaximumLength(2000);
        RuleFor(x => x.EditingErrorCount).GreaterThanOrEqualTo(0);
    }
}

public class UpdateMediaPipelineCommandHandler : IRequestHandler<UpdateMediaPipelineCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public UpdateMediaPipelineCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(UpdateMediaPipelineCommand request, CancellationToken ct)
    {
        var pipeline = await _db.MediaProductionPipelines
            .FirstOrDefaultAsync(mp => mp.Id == request.Id, ct);

        if (pipeline == null)
        {
            throw new KeyNotFoundException("Media production item not found.");
        }

        // Validate agent assignee if changed
        if (request.AssignedAgentId.HasValue && request.AssignedAgentId.Value != Guid.Empty && request.AssignedAgentId != pipeline.AssignedAgentId)
        {
            var agent = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == request.AssignedAgentId.Value, ct);

            if (agent == null)
            {
                throw new KeyNotFoundException("Assigned agent user not found.");
            }

            var isStudent = agent.UserRoles.Any(ur => ur.Role.Type == RoleType.Student);
            if (isStudent)
            {
                throw new InvalidOperationException("Cannot assign media production tasks to student users.");
            }
        }

        // Enforce approval gate before publishing
        if (request.Stage == MediaStage.Published && pipeline.Stage != MediaStage.Approved && pipeline.Stage != MediaStage.Published)
        {
            return ApiResponse.Fail("Cannot publish content that has not been approved.");
        }

        // Handle transition to Review stage (create approval task)
        if (request.Stage == MediaStage.Review && pipeline.Stage != MediaStage.Review)
        {
            if (!request.SupervisorId.HasValue || request.SupervisorId.Value == Guid.Empty)
            {
                return ApiResponse.Fail("A supervisor must be selected to review the content.");
            }

            var supervisor = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == request.SupervisorId.Value, ct);

            if (supervisor == null)
            {
                throw new KeyNotFoundException("Selected supervisor not found.");
            }

            var isManager = supervisor.UserRoles.Any(ur => ur.Role.Type == RoleType.Admin || ur.Role.Type == RoleType.Supervisor);
            if (!isManager)
            {
                return ApiResponse.Fail("Selected user is not a manager (Admin or Supervisor).");
            }

            // Create TaskItem in operations module
            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                Title = $"مراجعة محتوى: {pipeline.Title}",
                Description = $"الرجاء مراجعة وتدقيق المحتوى المرفق.\nرابط المجلد: {request.AssetFolderUrl ?? "غير متوفر"}",
                AssigneeId = request.SupervisorId.Value,
                CreatedById = request.UserId,
                Priority = TaskPriority.High,
                Status = TaskStatus.Review,
                MediaPipelineId = pipeline.Id
            };

            _db.TaskItems.Add(task);
        }

        // Handle published date timestamping
        if (request.Stage == MediaStage.Published && pipeline.Stage != MediaStage.Published)
        {
            pipeline.PublishedAt = DateTime.UtcNow;
        }
        else if (request.Stage != MediaStage.Published)
        {
            pipeline.PublishedAt = null;
        }

        var oldValues = $"Title: {pipeline.Title}, Stage: {pipeline.Stage}, AssignedAgentId: {pipeline.AssignedAgentId}";

        // Apply properties updates
        pipeline.Title = request.Title;
        pipeline.Description = request.Description;
        pipeline.AssignedAgentId = (request.AssignedAgentId == Guid.Empty) ? null : request.AssignedAgentId;
        pipeline.AssetFolderUrl = request.AssetFolderUrl;
        pipeline.EditingErrorCount = request.EditingErrorCount;
        pipeline.Stage = request.Stage;

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "UpdateMediaPipeline",
            EntityType = nameof(MediaProductionPipeline),
            EntityId = pipeline.Id,
            PerformedByUserId = request.UserId != Guid.Empty ? request.UserId : null,
            OldValues = oldValues,
            NewValues = $"Title: {pipeline.Title}, Stage: {pipeline.Stage}, AssignedAgentId: {pipeline.AssignedAgentId}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok();
    }
}

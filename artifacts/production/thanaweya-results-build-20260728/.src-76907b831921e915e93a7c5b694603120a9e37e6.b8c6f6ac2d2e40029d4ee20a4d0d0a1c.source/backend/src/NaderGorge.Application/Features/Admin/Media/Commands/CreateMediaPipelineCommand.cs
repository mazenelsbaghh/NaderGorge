using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Media.Commands;

public record CreateMediaPipelineCommand(
    string Title,
    string? Description,
    Guid? AssignedAgentId,
    string? AssetFolderUrl,
    Guid PerformedByUserId = default
) : IRequest<ApiResponse<Guid>>;

public class CreateMediaPipelineCommandValidator : AbstractValidator<CreateMediaPipelineCommand>
{
    public CreateMediaPipelineCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssetFolderUrl).MaximumLength(2000);
    }
}

public class CreateMediaPipelineCommandHandler : IRequestHandler<CreateMediaPipelineCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateMediaPipelineCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateMediaPipelineCommand request, CancellationToken ct)
    {
        if (request.AssignedAgentId.HasValue && request.AssignedAgentId.Value != Guid.Empty)
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

        var pipeline = new MediaProductionPipeline
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            AssignedAgentId = (request.AssignedAgentId == Guid.Empty) ? null : request.AssignedAgentId,
            AssetFolderUrl = request.AssetFolderUrl,
            Stage = MediaStage.Preparation,
            EditingErrorCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.MediaProductionPipelines.Add(pipeline);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateMediaPipeline",
            EntityType = nameof(MediaProductionPipeline),
            EntityId = pipeline.Id,
            PerformedByUserId = request.PerformedByUserId != Guid.Empty ? request.PerformedByUserId : null,
            NewValues = $"Title: {pipeline.Title}, Stage: {pipeline.Stage}, AssignedAgentId: {pipeline.AssignedAgentId}",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(pipeline.Id, "Media production item created successfully.");
    }
}

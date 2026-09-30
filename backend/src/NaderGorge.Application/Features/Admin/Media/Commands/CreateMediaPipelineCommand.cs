using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
) : IRequest<ApiResponse<Guid>>
{
    public string? OperationId { get; init; }
}

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
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await CreateOnceAsync(request, null, null, ct);
        if (request.PerformedByUserId == Guid.Empty)
            return ApiResponse<Guid>.Fail("Operation actor is required.", ["INVALID_OPERATION_ACTOR"]);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Title, request.Description, request.AssignedAgentId,
                request.AssetFolderUrl, request.PerformedByUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "media-pipeline.create" && prior.ActorUserId == request.PerformedByUserId
                && prior.RequestHash == requestHash
                ? ApiResponse<Guid>.Ok(prior.ResultEntityId, "Media production item created successfully.")
                : ApiResponse<Guid>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);

        var created = await CreateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<Guid>> CreateOnceAsync(CreateMediaPipelineCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
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

        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "media-pipeline.create",
                ActorUserId = request.PerformedByUserId, RequestHash = requestHash!, ResultEntityId = pipeline.Id
            });

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(pipeline.Id, "Media production item created successfully.");
    }
}

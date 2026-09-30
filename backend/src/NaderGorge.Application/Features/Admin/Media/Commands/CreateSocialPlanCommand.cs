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

public record CreateSocialPlanCommand(
    string Title,
    string? Description,
    string? Script,
    SocialPlatform Platform,
    SocialPlanStatus Status,
    DateTime ScheduledDate,
    Guid? MediaProductionPipelineId = null,
    Guid PerformedByUserId = default
) : IRequest<ApiResponse<Guid>>
{
    public string? OperationId { get; init; }
}

public class CreateSocialPlanCommandValidator : AbstractValidator<CreateSocialPlanCommand>
{
    public CreateSocialPlanCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Script).MaximumLength(10000);
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ScheduledDate).NotEmpty();
    }
}

public class CreateSocialPlanCommandHandler : IRequestHandler<CreateSocialPlanCommand, ApiResponse<Guid>>
{
    private readonly IAppDbContext _db;

    public CreateSocialPlanCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<Guid>> Handle(CreateSocialPlanCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<Guid>.Fail("Invalid operation identifier.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await CreateOnceAsync(request, null, null, ct);
        if (request.PerformedByUserId == Guid.Empty)
            return ApiResponse<Guid>.Fail("Operation actor is required.", ["INVALID_OPERATION_ACTOR"]);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { request.Title, request.Description, request.Script,
                request.Platform, request.Status, request.ScheduledDate,
                request.MediaProductionPipelineId, request.PerformedByUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
            return prior.Scope == "social-plan.create" && prior.ActorUserId == request.PerformedByUserId
                && prior.RequestHash == requestHash
                ? ApiResponse<Guid>.Ok(prior.ResultEntityId, "Social media plan created successfully.")
                : ApiResponse<Guid>.Fail("Operation identifier already used for another action.", ["IDEMPOTENCY_CONFLICT"]);
        if (request.Status is not (SocialPlanStatus.Draft or SocialPlanStatus.Scripting))
            return ApiResponse<Guid>.Fail("Publishing or scheduling requires a high-risk action.",
                ["RISK_REQUIRES_STRONG_CONFIRMATION"]);

        var created = await CreateOnceAsync(request, operationId, requestHash, ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<Guid>> CreateOnceAsync(CreateSocialPlanCommand request,
        string? operationId, string? requestHash, CancellationToken ct)
    {
        if (request.MediaProductionPipelineId.HasValue && request.MediaProductionPipelineId.Value != Guid.Empty)
        {
            var pipelineExists = await _db.MediaProductionPipelines.AnyAsync(mp => mp.Id == request.MediaProductionPipelineId.Value, ct);
            if (!pipelineExists)
            {
                return ApiResponse<Guid>.Fail("Linked media production pipeline not found.");
            }
        }

        var plan = new SocialMediaPlan
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Script = request.Script,
            Platform = request.Platform,
            Status = request.Status,
            ScheduledDate = request.ScheduledDate,
            MediaProductionPipelineId = (request.MediaProductionPipelineId == Guid.Empty) ? null : request.MediaProductionPipelineId,
            CreatedAt = DateTime.UtcNow
        };

        _db.SocialMediaPlans.Add(plan);

        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CreateSocialPlan",
            EntityType = nameof(SocialMediaPlan),
            EntityId = plan.Id,
            PerformedByUserId = request.PerformedByUserId != Guid.Empty ? request.PerformedByUserId : null,
            NewValues = $"Title: {plan.Title}, Platform: {plan.Platform}, Status: {plan.Status}, ScheduledDate: {plan.ScheduledDate}",
            CreatedAt = DateTime.UtcNow
        });

        if (operationId is not null)
            _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
            {
                OperationId = operationId, Scope = "social-plan.create",
                ActorUserId = request.PerformedByUserId, RequestHash = requestHash!, ResultEntityId = plan.Id
            });

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(plan.Id, "Social media plan created successfully.");
    }
}

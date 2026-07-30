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
) : IRequest<ApiResponse<Guid>>;

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

        await _db.SaveChangesAsync(ct);

        return ApiResponse<Guid>.Ok(plan.Id, "Social media plan created successfully.");
    }
}

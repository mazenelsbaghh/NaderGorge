using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.VideoTypes;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.VideoTypes.Commands;

public record CreateVideoTypeCommand(string Name, int SortOrder, bool IsActive, Guid AdminUserId)
    : IRequest<ApiResponse<VideoTypeDto>>
{
    public string? OperationId { get; init; }
}

public sealed class CreateVideoTypeCommandValidator : AbstractValidator<CreateVideoTypeCommand>
{
    public CreateVideoTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name) && VideoTypeRules.CleanName(name).Length is >= VideoTypeRules.MinNameLength and <= VideoTypeRules.MaxNameLength)
            .WithMessage("اسم النوع يجب أن يكون بين حرفين و80 حرفاً.");
        RuleFor(command => command.SortOrder)
            .InclusiveBetween(VideoTypeRules.MinSortOrder, VideoTypeRules.MaxSortOrder);
    }
}

public sealed class CreateVideoTypeCommandHandler : IRequestHandler<CreateVideoTypeCommand, ApiResponse<VideoTypeDto>>
{
    private readonly IAppDbContext _db;

    public CreateVideoTypeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<VideoTypeDto>> Handle(CreateVideoTypeCommand request, CancellationToken ct)
    {
        var name = VideoTypeRules.CleanName(request.Name);
        var normalizedName = VideoTypeRules.NormalizeName(name);
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<VideoTypeDto>.Fail("معرّف العملية غير صالح.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await CreateNewAsync(request, name, normalizedName, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Name = name, request.SortOrder, request.IsActive, request.AdminUserId }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
        {
            if (prior.Scope != "video-type.create" || prior.ActorUserId != request.AdminUserId
                || prior.RequestHash != requestHash)
                return ApiResponse<VideoTypeDto>.Fail("معرّف العملية مستخدم لطلب آخر.", ["IDEMPOTENCY_CONFLICT"]);
            var priorDto = JsonSerializer.Deserialize<VideoTypeDto>(prior.SafeResultJson
                ?? throw new InvalidOperationException("Video type receipt has no result."));
            if (priorDto?.Id != prior.ResultEntityId)
                throw new InvalidOperationException("Video type receipt result does not match its entity identity.");
            return ApiResponse<VideoTypeDto>.Ok(priorDto, "تم إنشاء نوع الفيديو.");
        }

        var created = await CreateNewAsync(request, name, normalizedName, ct);
        if (!created.Success)
            return created;
        _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId,
            Scope = "video-type.create",
            ActorUserId = request.AdminUserId,
            RequestHash = requestHash,
            ResultEntityId = created.Data!.Id,
            SafeResultJson = JsonSerializer.Serialize(created.Data)
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return created;
    }

    private async Task<ApiResponse<VideoTypeDto>> CreateNewAsync(
        CreateVideoTypeCommand request, string name, string normalizedName, CancellationToken ct)
    {
        if (await _db.VideoTypes.AnyAsync(type => type.NormalizedName == normalizedName, ct))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        var type = new VideoType
        {
            Name = name,
            NormalizedName = normalizedName,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        _db.VideoTypes.Add(type);
        var audit = new AuditLog
        {
            Action = "CREATE_VIDEO_TYPE",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            NewValues = JsonSerializer.Serialize(new { type.Name, type.SortOrder, type.IsActive })
        };
        _db.AuditLogs.Add(audit);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (VideoTypeRules.IsDuplicateNameViolation(exception))
        {
            _db.Entry(type).State = EntityState.Detached;
            _db.Entry(audit).State = EntityState.Detached;
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        return ApiResponse<VideoTypeDto>.Ok(VideoTypeRules.ToDto(type), "تم إنشاء نوع الفيديو.");
    }
}

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

public record UpdateVideoTypeCommand(Guid Id, string Name, int SortOrder, Guid AdminUserId)
    : IRequest<ApiResponse<VideoTypeDto>>
{
    public string? OperationId { get; init; }
}

public sealed class UpdateVideoTypeCommandValidator : AbstractValidator<UpdateVideoTypeCommand>
{
    public UpdateVideoTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name) && VideoTypeRules.CleanName(name).Length is >= VideoTypeRules.MinNameLength and <= VideoTypeRules.MaxNameLength)
            .WithMessage("اسم النوع يجب أن يكون بين حرفين و80 حرفاً.");
        RuleFor(command => command.SortOrder)
            .InclusiveBetween(VideoTypeRules.MinSortOrder, VideoTypeRules.MaxSortOrder);
    }
}

public sealed class UpdateVideoTypeCommandHandler : IRequestHandler<UpdateVideoTypeCommand, ApiResponse<VideoTypeDto>>
{
    private readonly IAppDbContext _db;

    public UpdateVideoTypeCommandHandler(IAppDbContext db) => _db = db;

    public async Task<ApiResponse<VideoTypeDto>> Handle(UpdateVideoTypeCommand request, CancellationToken ct)
    {
        var operationId = request.OperationId?.Trim();
        if (request.OperationId is not null && (string.IsNullOrEmpty(operationId) || operationId.Length > 200))
            return ApiResponse<VideoTypeDto>.Fail("معرّف العملية غير صالح.", ["INVALID_OPERATION_ID"]);
        if (operationId is null)
            return await UpdateExistingAsync(request, ct);

        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                request.Id,
                Name = VideoTypeRules.CleanName(request.Name),
                request.SortOrder,
                request.AdminUserId
            }))));
        await using var transaction = await _db.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var prior = await _db.AuthoritativeOperationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationId == operationId, ct);
        if (prior is not null)
        {
            if (prior.Scope != "video-type.update" || prior.ActorUserId != request.AdminUserId
                || prior.RequestHash != requestHash)
                return ApiResponse<VideoTypeDto>.Fail("معرّف العملية مستخدم لطلب آخر.", ["IDEMPOTENCY_CONFLICT"]);
            var priorDto = JsonSerializer.Deserialize<VideoTypeDto>(prior.SafeResultJson
                ?? throw new InvalidOperationException("Video type receipt has no result."));
            if (priorDto?.Id != prior.ResultEntityId)
                throw new InvalidOperationException("Video type receipt result does not match its entity identity.");
            return ApiResponse<VideoTypeDto>.Ok(priorDto, "تم تحديث نوع الفيديو.");
        }

        var updated = await UpdateExistingAsync(request, ct);
        if (!updated.Success)
            return updated;
        _db.AuthoritativeOperationReceipts.Add(new AuthoritativeOperationReceipt
        {
            OperationId = operationId,
            Scope = "video-type.update",
            ActorUserId = request.AdminUserId,
            RequestHash = requestHash,
            ResultEntityId = updated.Data!.Id,
            SafeResultJson = JsonSerializer.Serialize(updated.Data)
        });
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return updated;
    }

    private async Task<ApiResponse<VideoTypeDto>> UpdateExistingAsync(
        UpdateVideoTypeCommand request, CancellationToken ct)
    {
        var type = await _db.VideoTypes.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (type == null)
        {
            return ApiResponse<VideoTypeDto>.Fail("نوع الفيديو غير موجود.", ["NOT_FOUND"]);
        }

        var name = VideoTypeRules.CleanName(request.Name);
        var normalizedName = VideoTypeRules.NormalizeName(name);
        if (await _db.VideoTypes.AnyAsync(item => item.Id != request.Id && item.NormalizedName == normalizedName, ct))
        {
            return ApiResponse<VideoTypeDto>.Fail("يوجد نوع فيديو بنفس الاسم.", ["VIDEO_TYPE_DUPLICATE"]);
        }

        var oldValues = new { type.Name, type.SortOrder };
        type.Name = name;
        type.NormalizedName = normalizedName;
        type.SortOrder = request.SortOrder;
        var audit = new AuditLog
        {
            Action = "UPDATE_VIDEO_TYPE",
            EntityType = nameof(VideoType),
            EntityId = type.Id,
            PerformedByUserId = request.AdminUserId,
            OldValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(new { type.Name, type.SortOrder })
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

        var count = await _db.LessonVideos.CountAsync(video => video.VideoTypeId == type.Id, ct);
        return ApiResponse<VideoTypeDto>.Ok(VideoTypeRules.ToDto(type, count), "تم تحديث نوع الفيديو.");
    }
}

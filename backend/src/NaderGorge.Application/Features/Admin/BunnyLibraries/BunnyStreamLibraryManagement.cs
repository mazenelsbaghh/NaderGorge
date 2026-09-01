using System.Globalization;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.BunnyLibraries;

public sealed record BunnyStreamLibraryDto(
    Guid Id,
    string Name,
    string LibraryId,
    bool IsActive,
    bool ApiKeyConfigured,
    int AssignedVideoCount,
    DateTime? LastValidatedAtUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record BunnyStreamLibraryOptionDto(
    Guid Id,
    string Name,
    string LibraryId,
    bool IsActive,
    bool ApiKeyConfigured);

internal sealed record AvailableBunnyStreamLibraryRow(
    Guid Id,
    string Name,
    long ExternalLibraryId);

internal sealed record BunnyStreamLibraryListRow(
    Guid Id,
    string Name,
    long ExternalLibraryId,
    bool IsActive,
    bool ApiKeyConfigured,
    int AssignedVideoCount,
    DateTime? LastValidatedAtUtc,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record GetBunnyStreamLibrariesQuery : IRequest<ApiResponse<IReadOnlyList<BunnyStreamLibraryDto>>>;

public sealed record GetAvailableBunnyStreamLibrariesQuery : IRequest<ApiResponse<IReadOnlyList<BunnyStreamLibraryOptionDto>>>;

public sealed record CreateBunnyStreamLibraryCommand(
    string Name,
    string LibraryId,
    string ApiKey,
    bool IsActive,
    Guid CurrentUserId) : IRequest<ApiResponse<BunnyStreamLibraryDto>>;

public sealed record UpdateBunnyStreamLibraryCommand(
    Guid Id,
    string Name,
    string LibraryId,
    string? ApiKey,
    bool IsActive,
    Guid CurrentUserId) : IRequest<ApiResponse<BunnyStreamLibraryDto>>;

public sealed record SetBunnyStreamLibraryStatusCommand(
    Guid Id,
    bool IsActive,
    Guid CurrentUserId) : IRequest<ApiResponse<BunnyStreamLibraryDto>>;

public sealed record DeleteBunnyStreamLibraryCommand(Guid Id, Guid CurrentUserId) : IRequest<ApiResponse>;

public sealed class GetBunnyStreamLibrariesQueryHandler
    : IRequestHandler<GetBunnyStreamLibrariesQuery, ApiResponse<IReadOnlyList<BunnyStreamLibraryDto>>>
{
    private readonly IAppDbContext _db;

    public GetBunnyStreamLibrariesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<IReadOnlyList<BunnyStreamLibraryDto>>> Handle(
        GetBunnyStreamLibrariesQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await BuildQuery().ToListAsync(cancellationToken);

        var libraries = rows
            .Select(library => new BunnyStreamLibraryDto(
                library.Id,
                library.Name,
                library.ExternalLibraryId.ToString(CultureInfo.InvariantCulture),
                library.IsActive,
                library.ApiKeyConfigured,
                library.AssignedVideoCount,
                library.LastValidatedAtUtc,
                library.CreatedAt,
                library.UpdatedAt))
            .ToList();

        return ApiResponse<IReadOnlyList<BunnyStreamLibraryDto>>.Ok(libraries);
    }

    internal IQueryable<BunnyStreamLibraryListRow> BuildQuery() =>
        _db.BunnyStreamLibraries
            .AsNoTracking()
            .OrderBy(library => library.Name)
            .Select(library => new BunnyStreamLibraryListRow(
                library.Id,
                library.Name,
                library.ExternalLibraryId,
                library.IsActive,
                library.ApiKeyCiphertext != null,
                library.Videos.Select(video => video.Id)
                    .Concat(_db.BunnyVideoAssets
                        .Where(asset => asset.BunnyStreamLibraryRecordId == library.Id)
                        .Select(asset => asset.LessonVideoId))
                    .Distinct()
                    .Count(),
                library.LastValidatedAtUtc,
                library.CreatedAt,
                library.UpdatedAt));
}

public sealed class GetAvailableBunnyStreamLibrariesQueryHandler
    : IRequestHandler<GetAvailableBunnyStreamLibrariesQuery, ApiResponse<IReadOnlyList<BunnyStreamLibraryOptionDto>>>
{
    private readonly IAppDbContext _db;

    public GetAvailableBunnyStreamLibrariesQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<IReadOnlyList<BunnyStreamLibraryOptionDto>>> Handle(
        GetAvailableBunnyStreamLibrariesQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await BuildQuery().ToListAsync(cancellationToken);

        var libraries = rows
            .Select(library => new BunnyStreamLibraryOptionDto(
                library.Id,
                library.Name,
                library.ExternalLibraryId.ToString(CultureInfo.InvariantCulture),
                true,
                true))
            .ToList();

        return ApiResponse<IReadOnlyList<BunnyStreamLibraryOptionDto>>.Ok(libraries);
    }

    internal IQueryable<AvailableBunnyStreamLibraryRow> BuildQuery() =>
        _db.BunnyStreamLibraries
            .AsNoTracking()
            .Where(library => library.IsActive
                && library.ApiKeyCiphertext != null)
            .OrderBy(library => library.Name)
            .Select(library => new AvailableBunnyStreamLibraryRow(
                library.Id,
                library.Name,
                library.ExternalLibraryId));
}

public sealed class CreateBunnyStreamLibraryCommandHandler
    : IRequestHandler<CreateBunnyStreamLibraryCommand, ApiResponse<BunnyStreamLibraryDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly IBunnyStreamLibrarySecretProtector _protector;

    public CreateBunnyStreamLibraryCommandHandler(
        IAppDbContext db,
        IBunnyStreamClientFactory clients,
        IBunnyStreamLibrarySecretProtector protector)
    {
        _db = db;
        _clients = clients;
        _protector = protector;
    }

    public async Task<ApiResponse<BunnyStreamLibraryDto>> Handle(
        CreateBunnyStreamLibraryCommand request,
        CancellationToken cancellationToken)
    {
        var input = BunnyStreamLibraryRules.Validate(request.Name, request.LibraryId, request.ApiKey, requireApiKey: true);
        if (!input.Success)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(input.Message!, [input.ErrorCode!]);
        }

        var duplicate = await BunnyStreamLibraryRules.FindDuplicateAsync(
            _db,
            input.NormalizedName!,
            input.ExternalLibraryId,
            excludeId: null,
            cancellationToken);
        if (duplicate is not null)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(duplicate.Value.Message, [duplicate.Value.Code]);
        }

        var validation = await _clients
            .Create(input.ExternalLibraryId, input.ApiKey!)
            .ValidateLibraryAccessAsync(cancellationToken);
        if (!validation.Success)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(validation.Message ?? "تعذر التحقق من مكتبة Bunny.", [validation.ErrorCode ?? "BUNNY_VALIDATION_FAILED"]);
        }

        var now = DateTime.UtcNow;
        var library = new BunnyStreamLibrary
        {
            Name = input.Name!,
            NormalizedName = input.NormalizedName!,
            ExternalLibraryId = input.ExternalLibraryId,
            IsActive = request.IsActive,
            LastValidatedAtUtc = now,
            CreatedAt = now
        };
        library.ApiKeyCiphertext = _protector.Protect(library.Id, input.ApiKey!);
        _db.BunnyStreamLibraries.Add(library);
        BunnyStreamLibraryRules.AddAudit(_db, "Create", library, request.CurrentUserId, apiKeyChanged: true);
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<BunnyStreamLibraryDto>.Ok(BunnyStreamLibraryRules.ToDto(library, 0));
    }
}

public sealed class UpdateBunnyStreamLibraryCommandHandler
    : IRequestHandler<UpdateBunnyStreamLibraryCommand, ApiResponse<BunnyStreamLibraryDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamClientFactory _clients;
    private readonly IBunnyStreamLibrarySecretProtector _protector;
    private readonly IBunnyStreamLibraryAccessService _access;

    public UpdateBunnyStreamLibraryCommandHandler(
        IAppDbContext db,
        IBunnyStreamClientFactory clients,
        IBunnyStreamLibrarySecretProtector protector,
        IBunnyStreamLibraryAccessService access)
    {
        _db = db;
        _clients = clients;
        _protector = protector;
        _access = access;
    }

    public async Task<ApiResponse<BunnyStreamLibraryDto>> Handle(
        UpdateBunnyStreamLibraryCommand request,
        CancellationToken cancellationToken)
    {
        var library = await _db.BunnyStreamLibraries
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (library is null)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail("مكتبة Bunny غير موجودة.", ["BUNNY_LIBRARY_NOT_FOUND"]);
        }

        var input = BunnyStreamLibraryRules.Validate(request.Name, request.LibraryId, request.ApiKey, requireApiKey: false);
        if (!input.Success)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(input.Message!, [input.ErrorCode!]);
        }

        var assignedVideoCount = await BunnyStreamLibraryReferenceCounter.CountAsync(
            _db,
            library.Id,
            cancellationToken);
        if (assignedVideoCount > 0 && input.ExternalLibraryId != library.ExternalLibraryId)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(
                "لا يمكن تغيير Library ID لمكتبة مرتبطة بفيديوهات. عطّلها وأضف مكتبة جديدة بدلًا من ذلك.",
                ["BUNNY_LIBRARY_IN_USE"]);
        }

        if (input.ExternalLibraryId != library.ExternalLibraryId && string.IsNullOrWhiteSpace(input.ApiKey))
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(
                "أدخل مفتاح API الخاص برقم المكتبة الجديد حتى يتم التحقق منه.",
                ["BUNNY_API_KEY_REQUIRED"]);
        }

        var duplicate = await BunnyStreamLibraryRules.FindDuplicateAsync(
            _db,
            input.NormalizedName!,
            input.ExternalLibraryId,
            library.Id,
            cancellationToken);
        if (duplicate is not null)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail(duplicate.Value.Message, [duplicate.Value.Code]);
        }

        var apiKeyChanged = !string.IsNullOrWhiteSpace(input.ApiKey);
        if (apiKeyChanged)
        {
            var validation = await _clients
                .Create(input.ExternalLibraryId, input.ApiKey!)
                .ValidateLibraryAccessAsync(cancellationToken);
            if (!validation.Success)
            {
                return ApiResponse<BunnyStreamLibraryDto>.Fail(validation.Message ?? "تعذر التحقق من مكتبة Bunny.", [validation.ErrorCode ?? "BUNNY_VALIDATION_FAILED"]);
            }

            library.ApiKeyCiphertext = _protector.Protect(library.Id, input.ApiKey!);
            library.LastValidatedAtUtc = DateTime.UtcNow;
        }
        else if (request.IsActive && !library.IsActive)
        {
            var validationFailure = await BunnyStreamLibraryRules.ValidateStoredAccessAsync(
                library.Id,
                _access,
                _clients,
                cancellationToken);
            if (validationFailure is not null)
            {
                return ApiResponse<BunnyStreamLibraryDto>.Fail(validationFailure.Value.Message, [validationFailure.Value.Code]);
            }

            library.LastValidatedAtUtc = DateTime.UtcNow;
        }

        library.Name = input.Name!;
        library.NormalizedName = input.NormalizedName!;
        library.ExternalLibraryId = input.ExternalLibraryId;
        library.IsActive = request.IsActive;
        library.UpdatedAt = DateTime.UtcNow;
        BunnyStreamLibraryRules.AddAudit(_db, "Update", library, request.CurrentUserId, apiKeyChanged);
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<BunnyStreamLibraryDto>.Ok(BunnyStreamLibraryRules.ToDto(library, assignedVideoCount));
    }
}

public sealed class SetBunnyStreamLibraryStatusCommandHandler
    : IRequestHandler<SetBunnyStreamLibraryStatusCommand, ApiResponse<BunnyStreamLibraryDto>>
{
    private readonly IAppDbContext _db;
    private readonly IBunnyStreamLibraryAccessService _access;
    private readonly IBunnyStreamClientFactory _clients;

    public SetBunnyStreamLibraryStatusCommandHandler(
        IAppDbContext db,
        IBunnyStreamLibraryAccessService access,
        IBunnyStreamClientFactory clients)
    {
        _db = db;
        _access = access;
        _clients = clients;
    }

    public async Task<ApiResponse<BunnyStreamLibraryDto>> Handle(
        SetBunnyStreamLibraryStatusCommand request,
        CancellationToken cancellationToken)
    {
        var library = await _db.BunnyStreamLibraries.FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (library is null)
        {
            return ApiResponse<BunnyStreamLibraryDto>.Fail("مكتبة Bunny غير موجودة.", ["BUNNY_LIBRARY_NOT_FOUND"]);
        }

        if (request.IsActive && !library.IsActive)
        {
            var validationFailure = await BunnyStreamLibraryRules.ValidateStoredAccessAsync(
                library.Id,
                _access,
                _clients,
                cancellationToken);
            if (validationFailure is not null)
            {
                return ApiResponse<BunnyStreamLibraryDto>.Fail(validationFailure.Value.Message, [validationFailure.Value.Code]);
            }
            library.LastValidatedAtUtc = DateTime.UtcNow;
        }

        library.IsActive = request.IsActive;
        library.UpdatedAt = DateTime.UtcNow;
        var count = await BunnyStreamLibraryReferenceCounter.CountAsync(_db, library.Id, cancellationToken);
        BunnyStreamLibraryRules.AddAudit(_db, request.IsActive ? "Enable" : "Disable", library, request.CurrentUserId, apiKeyChanged: false);
        await _db.SaveChangesAsync(cancellationToken);
        return ApiResponse<BunnyStreamLibraryDto>.Ok(BunnyStreamLibraryRules.ToDto(library, count));
    }
}

public sealed class DeleteBunnyStreamLibraryCommandHandler
    : IRequestHandler<DeleteBunnyStreamLibraryCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public DeleteBunnyStreamLibraryCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(DeleteBunnyStreamLibraryCommand request, CancellationToken cancellationToken)
    {
        var library = await _db.BunnyStreamLibraries.FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        if (library is null)
        {
            return ApiResponse.Fail("مكتبة Bunny غير موجودة.", ["BUNNY_LIBRARY_NOT_FOUND"]);
        }

        if (await BunnyStreamLibraryReferenceCounter.IsInUseAsync(_db, library.Id, cancellationToken))
        {
            return ApiResponse.Fail(
                "لا يمكن حذف مكتبة مستخدمة. يمكنك تعطيلها وسيستمر تشغيل فيديوهاتها القديمة.",
                ["BUNNY_LIBRARY_IN_USE"]);
        }

        BunnyStreamLibraryRules.AddAudit(_db, "Delete", library, request.CurrentUserId, apiKeyChanged: false);
        _db.BunnyStreamLibraries.Remove(library);
        await _db.SaveChangesAsync(cancellationToken);
        return ApiResponse.Ok("تم حذف مكتبة Bunny.");
    }
}

internal static class BunnyStreamLibraryReferenceCounter
{
    public static IQueryable<Guid> ReferencedLessonVideoIds(IAppDbContext db, Guid libraryId) =>
        db.LessonVideos
            .Where(video => video.BunnyStreamLibraryId == libraryId)
            .Select(video => video.Id)
            .Concat(db.BunnyVideoAssets
                .Where(asset => asset.BunnyStreamLibraryRecordId == libraryId)
                .Select(asset => asset.LessonVideoId));

    public static Task<int> CountAsync(IAppDbContext db, Guid libraryId, CancellationToken cancellationToken) =>
        ReferencedLessonVideoIds(db, libraryId)
            .Distinct()
            .CountAsync(cancellationToken);

    public static Task<bool> IsInUseAsync(IAppDbContext db, Guid libraryId, CancellationToken cancellationToken) =>
        ReferencedLessonVideoIds(db, libraryId)
            .AnyAsync(cancellationToken);
}

internal static class BunnyStreamLibraryRules
{
    internal sealed record ValidatedInput(
        bool Success,
        string? Name,
        string? NormalizedName,
        long ExternalLibraryId,
        string? ApiKey,
        string? ErrorCode,
        string? Message);

    public static ValidatedInput Validate(string name, string libraryId, string? apiKey, bool requireApiKey)
    {
        var trimmedName = name?.Trim().Normalize(NormalizationForm.FormC);
        if (string.IsNullOrWhiteSpace(trimmedName) || trimmedName.Length > 100)
        {
            return Fail("BUNNY_LIBRARY_NAME_INVALID", "اسم المكتبة مطلوب وبحد أقصى 100 حرف.");
        }

        if (!long.TryParse(libraryId?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLibraryId) || parsedLibraryId <= 0)
        {
            return Fail("BUNNY_LIBRARY_ID_INVALID", "Library ID يجب أن يكون رقمًا صحيحًا موجبًا.");
        }

        var trimmedApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        if (requireApiKey && trimmedApiKey is null)
        {
            return Fail("BUNNY_API_KEY_REQUIRED", "مفتاح API مطلوب عند إضافة المكتبة.");
        }
        if (trimmedApiKey is { Length: > 512 })
        {
            return Fail("BUNNY_API_KEY_INVALID", "مفتاح API غير صالح.");
        }

        return new ValidatedInput(
            true,
            trimmedName,
            trimmedName.ToUpperInvariant(),
            parsedLibraryId,
            trimmedApiKey,
            null,
            null);
    }

    public static async Task<(string Code, string Message)?> FindDuplicateAsync(
        IAppDbContext db,
        string normalizedName,
        long externalLibraryId,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var duplicateName = await db.BunnyStreamLibraries.AnyAsync(
            library => library.NormalizedName == normalizedName && (!excludeId.HasValue || library.Id != excludeId.Value),
            cancellationToken);
        if (duplicateName)
        {
            return ("BUNNY_LIBRARY_NAME_EXISTS", "يوجد بالفعل مكتبة Bunny بهذا الاسم.");
        }

        var duplicateId = await db.BunnyStreamLibraries.AnyAsync(
            library => library.ExternalLibraryId == externalLibraryId && (!excludeId.HasValue || library.Id != excludeId.Value),
            cancellationToken);
        return duplicateId
            ? ("BUNNY_LIBRARY_ID_EXISTS", "Library ID مستخدم بالفعل في مكتبة أخرى.")
            : null;
    }

    public static async Task<(string Code, string Message)?> ValidateStoredAccessAsync(
        Guid libraryId,
        IBunnyStreamLibraryAccessService accessService,
        IBunnyStreamClientFactory clients,
        CancellationToken cancellationToken)
    {
        var access = await accessService.ResolveAsync(libraryId, requireActive: false, cancellationToken);
        if (!access.Success || access.Access is null)
        {
            return (access.ErrorCode ?? "BUNNY_API_KEY_REQUIRED", access.Message ?? "مفتاح API مطلوب لهذه المكتبة.");
        }

        var validation = await clients
            .Create(access.Access.ExternalLibraryId, access.Access.ApiKey)
            .ValidateLibraryAccessAsync(cancellationToken);
        return validation.Success
            ? null
            : (validation.ErrorCode ?? "BUNNY_VALIDATION_FAILED", validation.Message ?? "تعذر التحقق من مكتبة Bunny.");
    }

    public static BunnyStreamLibraryDto ToDto(BunnyStreamLibrary library, int assignedVideoCount) =>
        new(
            library.Id,
            library.Name,
            library.ExternalLibraryId.ToString(CultureInfo.InvariantCulture),
            library.IsActive,
            library.ApiKeyCiphertext is { Length: > 0 },
            assignedVideoCount,
            library.LastValidatedAtUtc,
            library.CreatedAt,
            library.UpdatedAt);

    public static void AddAudit(
        IAppDbContext db,
        string action,
        BunnyStreamLibrary library,
        Guid actorUserId,
        bool apiKeyChanged)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = $"BunnyStreamLibrary.{action}",
            EntityType = nameof(BunnyStreamLibrary),
            EntityId = library.Id,
            PerformedByUserId = actorUserId,
            NewValues = JsonSerializer.Serialize(new
            {
                library.Name,
                LibraryId = library.ExternalLibraryId,
                library.IsActive,
                ApiKeyChanged = apiKeyChanged
            })
        });
    }

    private static ValidatedInput Fail(string code, string message) =>
        new(false, null, null, 0, null, code, message);
}

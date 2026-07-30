using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public enum ContentImageType
{
    Package,
    Term,
    Section
}

public record UploadContentImageCommand(
    Guid EntityId,
    ContentImageType ContentType,
    byte[] ImageBytes) : IRequest<ApiResponse<string>>;

public sealed class UploadContentImageCommandHandler
    : IRequestHandler<UploadContentImageCommand, ApiResponse<string>>
{
    private readonly IAppDbContext _db;
    private readonly IContentImageStorage _imageStorage;

    public UploadContentImageCommandHandler(IAppDbContext db, IContentImageStorage imageStorage)
    {
        _db = db;
        _imageStorage = imageStorage;
    }

    public async Task<ApiResponse<string>> Handle(
        UploadContentImageCommand request,
        CancellationToken cancellationToken)
    {
        var contentEntity = await FindContentEntityAsync(request, cancellationToken);
        if (contentEntity is null)
        {
            return ApiResponse<string>.Fail("Content entity not found");
        }

        await using var imageStream = new MemoryStream(request.ImageBytes, writable: false);
        var imageUrl = await _imageStorage.SaveAsWebpAsync(
            imageStream,
            request.ContentType.ToString().ToLowerInvariant(),
            cancellationToken);

        SetImageUrl(contentEntity, imageUrl);
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.Ok(imageUrl, "Content image updated successfully");
    }

    private async Task<object?> FindContentEntityAsync(
        UploadContentImageCommand request,
        CancellationToken cancellationToken) => request.ContentType switch
    {
        ContentImageType.Package => await _db.Packages.FirstOrDefaultAsync(
            package => package.Id == request.EntityId,
            cancellationToken),
        ContentImageType.Term => await _db.Terms.FirstOrDefaultAsync(
            term => term.Id == request.EntityId,
            cancellationToken),
        ContentImageType.Section => await _db.ContentSections.FirstOrDefaultAsync(
            section => section.Id == request.EntityId,
            cancellationToken),
        _ => null
    };

    private static void SetImageUrl(object contentEntity, string imageUrl)
    {
        switch (contentEntity)
        {
            case NaderGorge.Domain.Entities.Package package:
                package.ImageUrl = imageUrl;
                break;
            case NaderGorge.Domain.Entities.Term term:
                term.ImageUrl = imageUrl;
                break;
            case NaderGorge.Domain.Entities.ContentSection section:
                section.ImageUrl = imageUrl;
                break;
        }
    }
}

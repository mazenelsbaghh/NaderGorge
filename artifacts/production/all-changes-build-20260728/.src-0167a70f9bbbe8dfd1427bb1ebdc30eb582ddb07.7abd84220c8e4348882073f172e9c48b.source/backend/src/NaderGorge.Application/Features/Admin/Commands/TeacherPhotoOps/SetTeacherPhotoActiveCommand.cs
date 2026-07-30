using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;

public record SetTeacherPhotoActiveCommand(Guid TeacherId, Guid PhotoId) : IRequest<ApiResponse>;

public class SetTeacherPhotoActiveCommandHandler : IRequestHandler<SetTeacherPhotoActiveCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public SetTeacherPhotoActiveCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(SetTeacherPhotoActiveCommand request, CancellationToken ct)
    {
        var photos = await _db.TeacherPhotos
            .Where(p => p.TeacherId == request.TeacherId)
            .ToListAsync(ct);

        var targetPhoto = photos.FirstOrDefault(p => p.Id == request.PhotoId);
        if (targetPhoto == null) return ApiResponse.Fail("Photo not found");

        foreach (var photo in photos)
        {
            photo.IsActive = (photo.Id == request.PhotoId);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Photo set as active successfully");
    }
}

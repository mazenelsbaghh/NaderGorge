using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;

public record DeleteTeacherPhotoCommand(Guid TeacherId, Guid PhotoId) : IRequest<ApiResponse>;

public class DeleteTeacherPhotoCommandHandler : IRequestHandler<DeleteTeacherPhotoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public DeleteTeacherPhotoCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(DeleteTeacherPhotoCommand request, CancellationToken ct)
    {
        var photo = await _db.TeacherPhotos
            .FirstOrDefaultAsync(p => p.Id == request.PhotoId && p.TeacherId == request.TeacherId, ct);

        if (photo == null) return ApiResponse.Fail("Photo not found");

        _db.TeacherPhotos.Remove(photo);
        await _db.SaveChangesAsync(ct);

        // If we deleted the active photo, activate the latest remaining photo
        if (photo.IsActive)
        {
            var latestRemaining = await _db.TeacherPhotos
                .Where(p => p.TeacherId == request.TeacherId)
                .OrderByDescending(p => p.UploadedAt)
                .FirstOrDefaultAsync(ct);

            if (latestRemaining != null)
            {
                latestRemaining.IsActive = true;
                await _db.SaveChangesAsync(ct);
            }
        }

        return ApiResponse.Ok("Photo deleted successfully");
    }
}

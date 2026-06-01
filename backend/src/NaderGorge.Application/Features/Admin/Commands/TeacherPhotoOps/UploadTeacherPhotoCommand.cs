using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;

public record UploadTeacherPhotoCommand(Guid TeacherId, string Base64Image, string FileName) : IRequest<ApiResponse>;

public class UploadTeacherPhotoCommandHandler : IRequestHandler<UploadTeacherPhotoCommand, ApiResponse>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<UploadTeacherPhotoCommandHandler> _logger;

    public UploadTeacherPhotoCommandHandler(IAppDbContext db, ILogger<UploadTeacherPhotoCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ApiResponse> Handle(UploadTeacherPhotoCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Uploading new teacher photo for TeacherId: {TeacherId}", request.TeacherId);

        var teacher = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TeacherId, ct);

        if (teacher == null) return ApiResponse.Fail("Teacher not found");

        try
        {
            // Convert Base64 to Array
            // Ensure no header e.g. "data:image/png;base64,"
            var base64Data = request.Base64Image.Contains(",") ? request.Base64Image.Split(',')[1] : request.Base64Image;
            var bytes = Convert.FromBase64String(base64Data);

            // Construct local path
            // For production, this should ideally be in a configurable external directory or storage provider
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "teacher");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.FileName)}";
            var filePath = Path.Combine(uploadsDir, uniqueFileName);

            await File.WriteAllBytesAsync(filePath, bytes, ct);

            // Using relative URL suitable for frontend
            var relativeUrl = $"/uploads/teacher/{uniqueFileName}";

            var photo = new TeacherPhoto
            {
                TeacherId = request.TeacherId,
                FileUrl = relativeUrl,
                IsActive = true,
                UploadedAt = DateTime.UtcNow
            };

            await _db.TeacherPhotos.AddAsync(photo, ct);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Teacher photo uploaded successfully: {Url}", relativeUrl);

            return ApiResponse.Ok("Photo uploaded successfully");
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to parse Base64 image data");
            return ApiResponse.Fail("Invalid image data format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving teacher photo");
            return ApiResponse.Fail("Could not save the image. Please try again later.");
        }
    }
}

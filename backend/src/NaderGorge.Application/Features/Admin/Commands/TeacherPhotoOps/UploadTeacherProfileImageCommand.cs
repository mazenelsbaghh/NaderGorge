using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Application.Features.Admin.Commands.TeacherPhotoOps;

public record UploadTeacherProfileImageCommand(Guid TeacherId, string Base64Image, string FileName) : IRequest<ApiResponse<string>>;

public class UploadTeacherProfileImageCommandHandler : IRequestHandler<UploadTeacherProfileImageCommand, ApiResponse<string>>
{
    private readonly IAppDbContext _db;
    private readonly ILogger<UploadTeacherProfileImageCommandHandler> _logger;

    public UploadTeacherProfileImageCommandHandler(IAppDbContext db, ILogger<UploadTeacherProfileImageCommandHandler> _logger)
    {
        _db = db;
        this._logger = _logger;
    }

    public async Task<ApiResponse<string>> Handle(UploadTeacherProfileImageCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Uploading new teacher profile image for TeacherId/UserId: {TeacherId}", request.TeacherId);

        var profile = await _db.TeacherProfiles
            .FirstOrDefaultAsync(tp => tp.Id == request.TeacherId || tp.UserId == request.TeacherId, ct);

        if (profile == null) return ApiResponse<string>.Fail("Teacher profile not found");

        try
        {
            // Convert Base64 to Array
            var base64Data = request.Base64Image.Contains(",") ? request.Base64Image.Split(',')[1] : request.Base64Image;
            var bytes = Convert.FromBase64String(base64Data);

            // Construct local path
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "teacher");
            if (!Directory.Exists(uploadsDir))
                Directory.CreateDirectory(uploadsDir);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(request.FileName)}";
            var filePath = Path.Combine(uploadsDir, uniqueFileName);

            await File.WriteAllBytesAsync(filePath, bytes, ct);

            // Using relative URL suitable for frontend
            var relativeUrl = $"/uploads/teacher/{uniqueFileName}";

            profile.ProfileImageUrl = relativeUrl;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Teacher profile image updated successfully: {Url}", relativeUrl);

            return ApiResponse<string>.Ok(relativeUrl, "Profile image updated successfully");
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to parse Base64 image data");
            return ApiResponse<string>.Fail("Invalid image data format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while saving teacher profile image");
            return ApiResponse<string>.Fail("Could not save the image. Please try again later.");
        }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Queries;

public record GetStudentProfileQuery(Guid UserId) : IRequest<ApiResponse<StudentProfileDto>>;

public record StudentProfileDto(
    Guid UserId,
    string FullName,
    string PhoneNumber,
    string DateOfBirth,
    string Gender,
    string Governorate,
    string? District,
    string Address,
    string? SecondaryPhone,
    string? ParentPhone,
    string? SecondaryParentPhone,
    string? MotherPhone,
    string? SchoolName,
    string EducationStage,
    string GradeLevel,
    string? StudyTrack,
    int DeviceCount,
    int MaxDevices
);

public class GetStudentProfileQueryHandler : IRequestHandler<GetStudentProfileQuery, ApiResponse<StudentProfileDto>>
{
    private readonly IAppDbContext _db;

    public GetStudentProfileQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentProfileDto>> Handle(GetStudentProfileQuery request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<StudentProfileDto>.Fail("ملف الطالب غير موجود");
        }

        var deviceCount = await _db.Devices.CountAsync(d => d.UserId == request.UserId, ct);

        // Fetch max devices from setting or fallback to default
        var maxDevicesSetting = await _db.PlatformSettings
            .Where(s => s.Key == "DeviceLimits:MaxDevicesPerStudent")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        int maxDevices = 2; // Default fallback
        if (int.TryParse(maxDevicesSetting, out var parsedMax))
        {
            maxDevices = parsedMax;
        }

        var dto = new StudentProfileDto(
            profile.UserId,
            profile.User.FullName,
            profile.User.PhoneNumber,
            profile.DateOfBirth.ToString("yyyy-MM-dd"),
            profile.Gender.ToString(),
            profile.Governorate,
            profile.District,
            profile.Address,
            profile.SecondaryPhone,
            profile.ParentPhone,
            profile.SecondaryParentPhone,
            profile.MotherPhone,
            profile.SchoolName,
            profile.EducationStage.ToString(),
            profile.GradeLevel.ToString(),
            profile.StudyTrack?.ToString(),
            deviceCount,
            maxDevices
        );

        return ApiResponse<StudentProfileDto>.Ok(dto);
    }
}

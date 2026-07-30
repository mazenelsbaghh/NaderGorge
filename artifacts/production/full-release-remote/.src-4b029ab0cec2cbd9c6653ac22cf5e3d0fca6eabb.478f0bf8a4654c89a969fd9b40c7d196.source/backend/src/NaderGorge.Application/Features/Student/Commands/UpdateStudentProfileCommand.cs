using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NaderGorge.Application.Features.Student.Commands;

public record UpdateStudentProfileCommand(
    Guid UserId,
    string Address,
    string? SecondaryPhone,
    string? ParentPhone,
    string? SecondaryParentPhone,
    string? MotherPhone,
    string? SchoolName
) : IRequest<ApiResponse<bool>>;

public class UpdateStudentProfileCommandHandler : IRequestHandler<UpdateStudentProfileCommand, ApiResponse<bool>>
{
    private readonly IAppDbContext _db;

    public UpdateStudentProfileCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<bool>> Handle(UpdateStudentProfileCommand request, CancellationToken ct)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, ct);

        if (profile == null)
        {
            return ApiResponse<bool>.Fail("ملف الطالب غير موجود");
        }

        profile.Address = request.Address;
        profile.SecondaryPhone = request.SecondaryPhone;
        profile.ParentPhone = request.ParentPhone;
        profile.SecondaryParentPhone = request.SecondaryParentPhone;
        profile.MotherPhone = request.MotherPhone;
        profile.SchoolName = request.SchoolName;

        await _db.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }
}

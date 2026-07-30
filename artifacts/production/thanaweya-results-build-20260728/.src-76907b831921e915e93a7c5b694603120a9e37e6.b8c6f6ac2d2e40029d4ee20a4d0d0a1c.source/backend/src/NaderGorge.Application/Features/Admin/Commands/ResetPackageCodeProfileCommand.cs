using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record ResetPackageCodeProfileCommand(Guid PackageId, Guid UpdatedByUserId) : IRequest<ApiResponse>;

public class ResetPackageCodeProfileCommandHandler : IRequestHandler<ResetPackageCodeProfileCommand, ApiResponse>
{
    private readonly IAppDbContext _db;

    public ResetPackageCodeProfileCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse> Handle(ResetPackageCodeProfileCommand request, CancellationToken ct)
    {
        var package = await _db.Packages.FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);
        if (package == null)
        {
            return ApiResponse.Fail("Package not found");
        }

        var profile = await _db.PackageCodePageProfiles.FirstOrDefaultAsync(p => p.PackageId == request.PackageId, ct);
        if (profile == null)
        {
            profile = new PackageCodePageProfile
            {
                PackageId = request.PackageId,
            };
            _db.PackageCodePageProfiles.Add(profile);
        }

        profile.Status = PackageCodePageProfileStatus.Fallback;
        profile.HeroEyebrow = null;
        profile.HeroTitle = null;
        profile.HeroDescription = null;
        profile.OfferTitle = null;
        profile.OfferDescription = null;
        profile.ActivationTitle = null;
        profile.ActivationDescription = null;
        profile.SupportTitle = null;
        profile.SupportDescription = null;
        profile.ThemeAccentKey = null;
        profile.PublishedAt = null;
        profile.UpdatedByUserId = request.UpdatedByUserId;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse.Ok("Package code page profile reset to fallback.");
    }
}

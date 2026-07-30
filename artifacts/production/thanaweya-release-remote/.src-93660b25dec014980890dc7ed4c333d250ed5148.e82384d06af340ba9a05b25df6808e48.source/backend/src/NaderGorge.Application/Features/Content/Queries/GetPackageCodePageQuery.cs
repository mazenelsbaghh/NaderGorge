using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Content.Queries;

public record GetPackageCodePageQuery(Guid PackageId) : IRequest<ApiResponse<StudentPackageCodePageDto>>;

public record PackageCodePagePanelDto(string Title, string Description);
public record PackageCodePageHeroDto(string Eyebrow, string Title, string Description);

public record StudentPackageCodePageDto(
    Guid PackageId,
    string PackageName,
    string PackageDescription,
    decimal PackagePrice,
    bool IsPackageActive,
    bool IsUsingCustomProfile,
    PackageCodePageHeroDto Hero,
    PackageCodePagePanelDto OfferPanel,
    PackageCodePagePanelDto ActivationPanel,
    PackageCodePagePanelDto SupportPanel,
    string ThemeAccentKey
);

public class GetPackageCodePageQueryHandler : IRequestHandler<GetPackageCodePageQuery, ApiResponse<StudentPackageCodePageDto>>
{
    private readonly IAppDbContext _db;

    public GetPackageCodePageQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<StudentPackageCodePageDto>> Handle(GetPackageCodePageQuery request, CancellationToken ct)
    {
        var package = await _db.Packages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);

        if (package == null)
        {
            return ApiResponse<StudentPackageCodePageDto>.Fail("Package not found");
        }

        var profile = await _db.PackageCodePageProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PackageId == request.PackageId, ct);

        var defaults = PackageCodePageProfileDefaults.Build(package.Name, package.Description, package.Price);
        var useCustom = package.IsActive && profile?.Status == PackageCodePageProfileStatus.Published;

        var dto = new StudentPackageCodePageDto(
            PackageId: package.Id,
            PackageName: package.Name,
            PackageDescription: package.Description,
            PackagePrice: package.Price,
            IsPackageActive: package.IsActive,
            IsUsingCustomProfile: useCustom,
            Hero: new PackageCodePageHeroDto(
                useCustom ? profile?.HeroEyebrow ?? defaults.HeroEyebrow : defaults.HeroEyebrow,
                useCustom ? profile?.HeroTitle ?? defaults.HeroTitle : defaults.HeroTitle,
                useCustom ? profile?.HeroDescription ?? defaults.HeroDescription : defaults.HeroDescription
            ),
            OfferPanel: new PackageCodePagePanelDto(
                useCustom ? profile?.OfferTitle ?? defaults.OfferTitle : defaults.OfferTitle,
                useCustom ? profile?.OfferDescription ?? defaults.OfferDescription : defaults.OfferDescription
            ),
            ActivationPanel: new PackageCodePagePanelDto(
                useCustom ? profile?.ActivationTitle ?? defaults.ActivationTitle : defaults.ActivationTitle,
                useCustom ? profile?.ActivationDescription ?? defaults.ActivationDescription : defaults.ActivationDescription
            ),
            SupportPanel: new PackageCodePagePanelDto(
                useCustom ? profile?.SupportTitle ?? defaults.SupportTitle : defaults.SupportTitle,
                useCustom ? profile?.SupportDescription ?? defaults.SupportDescription : defaults.SupportDescription
            ),
            ThemeAccentKey: useCustom ? profile?.ThemeAccentKey ?? defaults.ThemeAccentKey : defaults.ThemeAccentKey
        );

        return ApiResponse<StudentPackageCodePageDto>.Ok(dto);
    }
}

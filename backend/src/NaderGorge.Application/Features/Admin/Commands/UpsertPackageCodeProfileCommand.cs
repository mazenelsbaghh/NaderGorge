using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Commands;

public record UpsertPackageCodeProfileCommand(
    Guid PackageId,
    PackageCodePageProfileStatus Status,
    string? HeroEyebrow,
    string? HeroTitle,
    string? HeroDescription,
    string? OfferTitle,
    string? OfferDescription,
    string? ActivationTitle,
    string? ActivationDescription,
    string? SupportTitle,
    string? SupportDescription,
    string? ThemeAccentKey,
    Guid UpdatedByUserId
) : IRequest<ApiResponse<UpsertPackageCodePageProfileResultDto>>;

public record UpsertPackageCodePageProfileResultDto(
    Guid PackageId,
    PackageCodePageProfileStatus Status,
    DateTime? PublishedAt
);

public class UpsertPackageCodeProfileCommandValidator : AbstractValidator<UpsertPackageCodeProfileCommand>
{
    public UpsertPackageCodeProfileCommandValidator()
    {
        RuleFor(x => x.PackageId).NotEmpty();
        RuleFor(x => x.UpdatedByUserId).NotEmpty();
        RuleFor(x => x.HeroEyebrow).MaximumLength(80);
        RuleFor(x => x.HeroTitle).MaximumLength(140);
        RuleFor(x => x.HeroDescription).MaximumLength(600);
        RuleFor(x => x.OfferTitle).MaximumLength(120);
        RuleFor(x => x.OfferDescription).MaximumLength(600);
        RuleFor(x => x.ActivationTitle).MaximumLength(120);
        RuleFor(x => x.ActivationDescription).MaximumLength(500);
        RuleFor(x => x.SupportTitle).MaximumLength(120);
        RuleFor(x => x.SupportDescription).MaximumLength(400);

        RuleFor(x => x.ThemeAccentKey)
            .Must(value => string.IsNullOrWhiteSpace(value) || PackageCodePageProfileDefaults.ThemeAccentKeys.Contains(value))
            .WithMessage("Theme accent key is invalid.");

        When(x => x.Status == PackageCodePageProfileStatus.Published, () =>
        {
            RuleFor(x => x.HeroTitle).NotEmpty().WithMessage("Hero title is required for publishing.");
            RuleFor(x => x.HeroDescription).NotEmpty().WithMessage("Hero description is required for publishing.");
            RuleFor(x => x.OfferTitle).NotEmpty().WithMessage("Offer title is required for publishing.");
            RuleFor(x => x.OfferDescription).NotEmpty().WithMessage("Offer description is required for publishing.");
            RuleFor(x => x.ActivationTitle).NotEmpty().WithMessage("Activation title is required for publishing.");
            RuleFor(x => x.ActivationDescription).NotEmpty().WithMessage("Activation description is required for publishing.");
        });
    }
}

public class UpsertPackageCodeProfileCommandHandler : IRequestHandler<UpsertPackageCodeProfileCommand, ApiResponse<UpsertPackageCodePageProfileResultDto>>
{
    private readonly IAppDbContext _db;

    public UpsertPackageCodeProfileCommandHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<UpsertPackageCodePageProfileResultDto>> Handle(UpsertPackageCodeProfileCommand request, CancellationToken ct)
    {
        var package = await _db.Packages.FirstOrDefaultAsync(p => p.Id == request.PackageId, ct);
        if (package == null)
        {
            return ApiResponse<UpsertPackageCodePageProfileResultDto>.Fail("Package not found");
        }

        var profile = await _db.PackageCodePageProfiles.FirstOrDefaultAsync(p => p.PackageId == request.PackageId, ct);
        var isNew = false;

        if (profile == null)
        {
            profile = new PackageCodePageProfile
            {
                PackageId = request.PackageId,
            };
            isNew = true;
        }

        profile.Status = request.Status;
        profile.HeroEyebrow = Normalize(request.HeroEyebrow);
        profile.HeroTitle = Normalize(request.HeroTitle);
        profile.HeroDescription = Normalize(request.HeroDescription);
        profile.OfferTitle = Normalize(request.OfferTitle);
        profile.OfferDescription = Normalize(request.OfferDescription);
        profile.ActivationTitle = Normalize(request.ActivationTitle);
        profile.ActivationDescription = Normalize(request.ActivationDescription);
        profile.SupportTitle = Normalize(request.SupportTitle);
        profile.SupportDescription = Normalize(request.SupportDescription);
        profile.ThemeAccentKey = Normalize(request.ThemeAccentKey) ?? PackageCodePageProfileDefaults.Build(package.Name, package.Description, package.Price).ThemeAccentKey;
        profile.UpdatedByUserId = request.UpdatedByUserId;
        profile.UpdatedAt = DateTime.UtcNow;

        if (request.Status == PackageCodePageProfileStatus.Published)
        {
            profile.PublishedAt ??= DateTime.UtcNow;
        }
        else
        {
            profile.PublishedAt = request.Status == PackageCodePageProfileStatus.Draft ? null : profile.PublishedAt;
        }

        if (isNew)
        {
            _db.PackageCodePageProfiles.Add(profile);
        }

        await _db.SaveChangesAsync(ct);

        return ApiResponse<UpsertPackageCodePageProfileResultDto>.Ok(
            new UpsertPackageCodePageProfileResultDto(profile.PackageId, profile.Status, profile.PublishedAt),
            "Package code page profile saved successfully."
        );
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

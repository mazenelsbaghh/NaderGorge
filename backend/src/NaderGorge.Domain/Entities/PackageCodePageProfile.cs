using NaderGorge.Domain.Common;

namespace NaderGorge.Domain.Entities;

public enum PackageCodePageProfileStatus
{
    Draft = 0,
    Published = 1,
    Fallback = 2,
}

public class PackageCodePageProfile : BaseEntity
{
    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public PackageCodePageProfileStatus Status { get; set; } = PackageCodePageProfileStatus.Fallback;

    public string? HeroEyebrow { get; set; }
    public string? HeroTitle { get; set; }
    public string? HeroDescription { get; set; }
    public string? OfferTitle { get; set; }
    public string? OfferDescription { get; set; }
    public string? ActivationTitle { get; set; }
    public string? ActivationDescription { get; set; }
    public string? SupportTitle { get; set; }
    public string? SupportDescription { get; set; }
    public string? ThemeAccentKey { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }

    public DateTime? PublishedAt { get; set; }
}

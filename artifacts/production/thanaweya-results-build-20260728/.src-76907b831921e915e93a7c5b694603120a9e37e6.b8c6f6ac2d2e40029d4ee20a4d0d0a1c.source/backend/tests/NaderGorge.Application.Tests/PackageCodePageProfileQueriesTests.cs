using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class PackageCodePageProfileQueriesTests
{
    [Fact]
    public async Task StudentQuery_ReturnsPublishedCustomProfile_WhenAvailable()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Physics Elite");

        db.PackageCodePageProfiles.Add(new PackageCodePageProfile
        {
            PackageId = packageId,
            Status = PackageCodePageProfileStatus.Published,
            HeroEyebrow = "ابدأ من هنا",
            HeroTitle = "عنوان مخصص",
            HeroDescription = "وصف مخصص",
            OfferTitle = "عرض مخصص",
            OfferDescription = "تفاصيل مخصصة",
            ActivationTitle = "فعّل الآن",
            ActivationDescription = "ادخل الكود",
            SupportTitle = "مساعدة",
            SupportDescription = "راسل الإدارة",
            ThemeAccentKey = "physics-gold",
        });
        await db.SaveChangesAsync();

        var handler = new GetPackageCodePageQueryHandler(db);
        var result = await handler.Handle(new GetPackageCodePageQuery(packageId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data!.IsUsingCustomProfile);
        Assert.Equal("عنوان مخصص", result.Data.Hero.Title);
        Assert.Equal("physics-gold", result.Data.ThemeAccentKey);
    }

    [Fact]
    public async Task StudentQuery_FallsBackToDefaults_WhenNoPublishedProfileExists()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Chemistry Pack", "Core chemistry", 0);

        var handler = new GetPackageCodePageQueryHandler(db);
        var result = await handler.Handle(new GetPackageCodePageQuery(packageId), CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.IsUsingCustomProfile);
        Assert.Contains("Chemistry Pack", result.Data.Hero.Title);
        Assert.Equal("default-gold", result.Data.ThemeAccentKey);
    }
}

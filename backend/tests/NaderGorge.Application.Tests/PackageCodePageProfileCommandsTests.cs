using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class PackageCodePageProfileCommandsTests
{
    [Fact]
    public async Task UpsertCommand_SavesAndReloadsPackageSpecificProfile()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Biology Pack");

        var handler = new UpsertPackageCodeProfileCommandHandler(db);
        var saveResult = await handler.Handle(
            new UpsertPackageCodeProfileCommand(
                packageId,
                PackageCodePageProfileStatus.Draft,
                "ابدأ",
                "عنوان الأحياء",
                "وصف الأحياء",
                "بعد التفعيل",
                "سيفتح المحتوى",
                "أدخل الكود",
                "يتم التحقق فورًا",
                "دعم",
                "اطلب المساعدة",
                "emerald-accent",
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(saveResult.Success);

        var queryHandler = new GetPackageCodeProfileQueryHandler(db);
        var queryResult = await queryHandler.Handle(new GetPackageCodeProfileQuery(packageId), CancellationToken.None);

        Assert.True(queryResult.Success);
        Assert.NotNull(queryResult.Data);
        Assert.Equal("عنوان الأحياء", queryResult.Data!.HeroTitle);
        Assert.Equal("emerald-accent", queryResult.Data.ThemeAccentKey);
        Assert.Equal(PackageCodePageProfileStatus.Draft, queryResult.Data.Status);
    }

    [Fact]
    public async Task UpsertCommand_DoesNotLeakBetweenPackages()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (packageOneId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Package One");
        var (packageTwoId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Package Two");

        var handler = new UpsertPackageCodeProfileCommandHandler(db);
        await handler.Handle(
            new UpsertPackageCodeProfileCommand(
                packageOneId,
                PackageCodePageProfileStatus.Published,
                "Eyebrow",
                "Package One Custom",
                "Desc",
                "Offer",
                "Offer Desc",
                "Activation",
                "Activation Desc",
                "Support",
                "Support Desc",
                "ocean-accent",
                Guid.NewGuid()),
            CancellationToken.None);

        var queryHandler = new GetPackageCodeProfileQueryHandler(db);
        var packageOneResult = await queryHandler.Handle(new GetPackageCodeProfileQuery(packageOneId), CancellationToken.None);
        var packageTwoResult = await queryHandler.Handle(new GetPackageCodeProfileQuery(packageTwoId), CancellationToken.None);

        Assert.Equal("Package One Custom", packageOneResult.Data!.HeroTitle);
        Assert.NotEqual("Package One Custom", packageTwoResult.Data!.HeroTitle);
        Assert.True(packageTwoResult.Data.IsUsingFallback);
    }
}

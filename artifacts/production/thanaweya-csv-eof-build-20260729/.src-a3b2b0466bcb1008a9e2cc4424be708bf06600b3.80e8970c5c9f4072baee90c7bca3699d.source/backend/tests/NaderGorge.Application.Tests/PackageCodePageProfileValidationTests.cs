using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Content.Queries;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public class PackageCodePageProfileValidationTests
{
    [Fact]
    public void Validator_RejectsPublishedProfile_WhenRequiredFieldsAreMissing()
    {
        var validator = new UpsertPackageCodeProfileCommandValidator();
        var result = validator.Validate(new UpsertPackageCodeProfileCommand(
            Guid.NewGuid(),
            PackageCodePageProfileStatus.Published,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "default-gold",
            Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "HeroTitle");
        Assert.Contains(result.Errors, error => error.PropertyName == "ActivationDescription");
    }

    [Fact]
    public async Task ResetCommand_RevertsStudentQueryToFallback()
    {
        await using AppDbContext db = TestAppDbContextFactory.Create();
        var (packageId, _) = await TestAppDbContextFactory.SeedPackageAsync(db, "Physics Reset");

        var upsertHandler = new UpsertPackageCodeProfileCommandHandler(db);
        await upsertHandler.Handle(new UpsertPackageCodeProfileCommand(
            packageId,
            PackageCodePageProfileStatus.Published,
            "ابدأ",
            "عنوان منشور",
            "وصف",
            "عرض",
            "تفاصيل",
            "فعّل",
            "أدخل الكود",
            "الدعم",
            "نحن هنا",
            "physics-gold",
            Guid.NewGuid()), CancellationToken.None);

        var resetHandler = new ResetPackageCodeProfileCommandHandler(db);
        var resetResult = await resetHandler.Handle(new ResetPackageCodeProfileCommand(packageId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(resetResult.Success);

        var studentQueryHandler = new GetPackageCodePageQueryHandler(db);
        var studentQueryResult = await studentQueryHandler.Handle(new GetPackageCodePageQuery(packageId), CancellationToken.None);

        Assert.True(studentQueryResult.Success);
        Assert.False(studentQueryResult.Data!.IsUsingCustomProfile);
        Assert.DoesNotContain("عنوان منشور", studentQueryResult.Data.Hero.Title);
    }
}

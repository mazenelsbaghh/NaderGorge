using NaderGorge.Application.Features.Admin.SharedPackages;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.Admin.SharedPackages;

public sealed class SharedPackageCommandTests
{
    [Fact]
    public async Task Create_rejects_invalid_draft_before_writing()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new CreateSharedPackageCommandHandler(db);
        var command = new CreateSharedPackageCommand(Guid.NewGuid(), " ", null, null, null, 0,
            SharedPackageDistributionMode.Percentage, false, null, null, null, null, [], [], null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(SharedPackageCommandStatus.Invalid, result.Status);
        Assert.Empty(db.SharedTeacherPackages);
    }

    [Fact]
    public async Task Publish_returns_not_found_without_creating_state()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = new PublishSharedPackageCommandHandler(db);

        var result = await handler.Handle(
            new PublishSharedPackageCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(SharedPackageCommandStatus.NotFound, result.Status);
        Assert.Empty(db.SharedTeacherPackages);
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using NaderGorge.Application.Features.Student.Recharge;
using NaderGorge.Application.Interfaces;
using NaderGorge.Application.Services;

namespace NaderGorge.Application.Tests;

public class SubmitRechargeProofValidationTests
{
    // Regression for the 2026-07-22 production failure where browser-normalized WEBP proofs were validated as PNG.
    [Fact]
    public async Task WebpProof_WithMatchingMetadata_PassesImageValidation()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = CreateHandler(db);

        var response = await handler.Handle(
            new SubmitRechargeCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "01012345678",
                CreateWebpHeader(),
                "proof.webp",
                "image/webp"),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("طلب الشحن هذا غير موجود", response.Message);
    }

    [Fact]
    public async Task WebpProof_DisguisedAsPng_IsRejected()
    {
        await using var db = TestAppDbContextFactory.Create();
        var handler = CreateHandler(db);

        var response = await handler.Handle(
            new SubmitRechargeCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "01012345678",
                CreateWebpHeader(),
                "proof.png",
                "image/png"),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("صورة إثبات التحويل يجب أن تكون صورة JPG أو PNG أو WEBP صالحة.", response.Message);
    }

    private static SubmitRechargeCommandHandler CreateHandler(Infrastructure.Data.AppDbContext db) =>
        new(
            db,
            new UnusedImageStorage(),
            new BalanceService(db, NullLogger<BalanceService>.Instance));

    private static byte[] CreateWebpHeader() =>
    [
        0x52, 0x49, 0x46, 0x46,
        0x04, 0x00, 0x00, 0x00,
        0x57, 0x45, 0x42, 0x50
    ];

    private sealed class UnusedImageStorage : IContentImageStorage
    {
        public Task<string> SaveAsWebpAsync(
            Stream imageStream,
            string contentFolder,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Storage must not be reached for a missing recharge request.");
    }
}

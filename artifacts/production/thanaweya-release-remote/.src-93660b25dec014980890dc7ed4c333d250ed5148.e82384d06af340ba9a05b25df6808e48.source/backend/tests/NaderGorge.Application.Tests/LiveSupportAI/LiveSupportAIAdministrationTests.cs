using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Services;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using Xunit;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIAdministrationTests
{
    [Fact]
    public async Task GetStatsAsync_WithInvalidPeriod_ThrowsException()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new LiveSupportAIAdminService(db, null!);

        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.GetStatsAsync("invalid-period", CancellationToken.None));
        
        Assert.Equal("INVALID_PERIOD", exception.Code);
    }

    [Fact]
    public async Task GetEvidenceAsync_WithInvalidPeriod_ThrowsException()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new LiveSupportAIAdminService(db, null!);

        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.GetEvidenceAsync("invalid-period", null, 10, CancellationToken.None));
        
        Assert.Equal("INVALID_PERIOD", exception.Code);
    }

    [Fact]
    public async Task GetEvidenceAsync_ClampsPageSize_AndPaginatesCorrectly()
    {
        await using var db = TestAppDbContextFactory.Create();
        var admin = await TestAppDbContextFactory.SeedUserAsync(db, "Admin User", "01222222221");

        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            db.LiveSupportAITurns.Add(new LiveSupportAITurn
            {
                Id = Guid.NewGuid(),
                ConversationId = Guid.NewGuid(),
                PolicyVersionId = Guid.NewGuid(),
                SourceMessageId = Guid.NewGuid(),
                Status = LiveSupportAITurnStatus.Completed,
                QueuedAt = now.AddMinutes(i),
                Version = 1
            });
        }
        await db.SaveChangesAsync();

        var service = new LiveSupportAIAdminService(db, null!);

        // Test clamping (pagesize too large -> clamped to 100)
        var clampedResult = await service.GetEvidenceAsync("all", null, 200, CancellationToken.None);
        Assert.Equal(5, clampedResult.Items.Count);

        // Test page size 2
        var page1 = await service.GetEvidenceAsync("all", null, 2, CancellationToken.None);
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);

        // Test page 2 using cursor
        var page2 = await service.GetEvidenceAsync("all", page1.NextCursor, 2, CancellationToken.None);
        Assert.Equal(2, page2.Items.Count);
        Assert.NotNull(page2.NextCursor);
    }

    [Fact]
    public async Task GetEvidenceAsync_WithInvalidCursor_ThrowsException()
    {
        await using var db = TestAppDbContextFactory.Create();
        var service = new LiveSupportAIAdminService(db, null!);

        var exception = await Assert.ThrowsAsync<LiveSupportAIAdminException>(
            () => service.GetEvidenceAsync("all", "invalid_base64_cursor_string", 10, CancellationToken.None));
        
        Assert.Equal("INVALID_CURSOR", exception.Code);
    }
}

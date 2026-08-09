using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests.Serialization;

public sealed class UtcDateTimeModelConventionTests
{
    [Fact]
    public void TimestampWithoutTimeZone_MaterializesAsUtc()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppDbContext(options);
        var property = db.Model.FindEntityType(typeof(AttendanceSession))!
            .FindProperty(nameof(AttendanceSession.ClockedInAt))!;
        var databaseTimestamp = new DateTime(2026, 8, 9, 10, 30, 0, DateTimeKind.Unspecified);

        var materialized = (DateTime)property.GetValueConverter()!.ConvertFromProvider(databaseTimestamp)!;

        Assert.Equal(DateTimeKind.Utc, materialized.Kind);
        Assert.Equal(databaseTimestamp.Ticks, materialized.Ticks);
    }
}

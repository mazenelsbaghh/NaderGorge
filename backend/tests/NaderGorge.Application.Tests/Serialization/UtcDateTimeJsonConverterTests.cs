using System.Text.Json;
using NaderGorge.API.Serialization;

namespace NaderGorge.Application.Tests.Serialization;

public sealed class UtcDateTimeJsonConverterTests
{
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void DatabaseTimestamp_SerializesAsExplicitUtc(DateTimeKind kind)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        var timestamp = DateTime.SpecifyKind(new DateTime(2026, 8, 9, 10, 30, 0), kind);

        var json = JsonSerializer.Serialize(timestamp, options);

        Assert.Equal("\"2026-08-09T10:30:00.0000000Z\"", json);
    }
}

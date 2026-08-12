using Microsoft.Extensions.Logging;
using NaderGorge.Infrastructure.Services.AdminAI;

namespace NaderGorge.Application.Tests.AdminAI;

public sealed class AdminAITelemetryTests
{
    [Fact]
    public void ReviewedLowCardinalityLabels_AreRecordedWithoutIdentifiersOrContent()
    {
        var logger = new CapturingLogger();
        var telemetry = new AdminAITelemetry(logger);

        telemetry.RecordDuration("model", 125, "success");
        telemetry.RecordOutcome("model", "success", "answer");
        telemetry.RecordProposalCount(2, "ordinary");

        var serialized = string.Join(' ', logger.Entries);
        Assert.Contains("model", serialized, StringComparison.Ordinal);
        Assert.Contains("success", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("turnId", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("model", "person@example.com", "answer")]
    [InlineData("model", "success", "3d0d38d2-8a08-4dc2-8752-cfabf4edeeea")]
    [InlineData("prompt", "success", "answer")]
    public void UnreviewedOrHighCardinalityLabels_AreRejected(string stage, string outcome, string decision) =>
        Assert.Throws<ArgumentException>(() => new AdminAITelemetry(new CapturingLogger()).RecordOutcome(stage, outcome, decision));

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void ProposalCountOutsideProtocolBounds_IsRejected(int count) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdminAITelemetry(new CapturingLogger()).RecordProposalCount(count, "ordinary"));

    private sealed class CapturingLogger : ILogger<AdminAITelemetry>
    {
        public List<string> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Entries.Add(formatter(state, exception));
    }
}

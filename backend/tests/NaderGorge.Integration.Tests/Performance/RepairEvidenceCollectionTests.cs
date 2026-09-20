using NaderGorge.API.AutoRepair;
using NaderGorge.Domain.Entities;
using NaderGorge.Infrastructure.Observability;

namespace NaderGorge.Integration.Tests.Performance;

public sealed class RepairEvidenceCollectionTests
{
    [Fact]
    public void Fresh_matching_measurements_reopen_diagnosis_only_twice()
    {
        var now = DateTimeOffset.UtcNow;
        var incident = new AutoRepairIncident { Status = "needs_evidence", Evidence = "Route=api/video/progress Method=POST", Summary = "latency unresolved" };
        var old = Measurement(now.AddSeconds(-1));
        RepairEvidenceCollector.Advance(incident, [old], now);
        Assert.Equal("collecting_evidence", incident.Status);
        RepairEvidenceCollector.Advance(incident, [old, Measurement(now.AddSeconds(1)) with { Message = "Route=api/other Method=POST" }], now.AddSeconds(2));
        Assert.Equal("collecting_evidence", incident.Status);
        var fresh = Measurement(now.AddSeconds(1));
        RepairEvidenceCollector.Advance(incident, [fresh, fresh], now.AddSeconds(2));
        Assert.Equal("queued", incident.Status);
        Assert.Single(incident.Events, x => x.Status == "evidence");
        Assert.Equal("Route=api/video/progress Method=POST", incident.Evidence);
        incident.Status = "needs_evidence";
        RepairEvidenceCollector.Advance(incident, [fresh], now.AddMinutes(1));
        RepairEvidenceCollector.Advance(incident, [fresh], now.AddMinutes(2));
        Assert.Equal("collecting_evidence", incident.Status);
        RepairEvidenceCollector.Advance(incident, [Measurement(now.AddMinutes(2))], now.AddMinutes(3));
        Assert.Equal("queued", incident.Status);
        incident.Status = "needs_evidence";
        RepairEvidenceCollector.Advance(incident, [], now.AddMinutes(4));
        Assert.Equal("needs_evidence", incident.Status);
        Assert.Single(incident.Events, x => x.Status == "collection_closed");
        Assert.Equal(2, incident.Events.Count(x => x.Status == "collecting_evidence"));
    }

    [Theory]
    [InlineData("unidentified failure", false)]
    [InlineData("Route=api/video/progress Method=POST", true)]
    public void Missing_route_or_expired_empty_window_requires_concrete_owner_action(string evidence, bool timedWindow)
    {
        var now = DateTimeOffset.UtcNow;
        var incident = new AutoRepairIncident { Status = "needs_evidence", Evidence = evidence, Summary = "original diagnosis" };
        RepairEvidenceCollector.Advance(incident, [], now);
        if (timedWindow) RepairEvidenceCollector.Advance(incident, [], now.AddMinutes(21));
        Assert.Equal("needs_evidence", incident.Status);
        Assert.Contains("original diagnosis", incident.Summary);
        Assert.Contains("المطلوب", incident.Summary);
        Assert.Single(incident.Events, x => x.Status == "collection_closed");
        Assert.DoesNotContain(incident.Events, x => x.Status == "evidence");
    }

    [Fact]
    public void Request_command_details_are_bounded_and_do_not_leak_between_scopes()
    {
        using var outer = RequestDbCommandScope.Begin();
        for (var i = 0; i < 30; i++) RequestDbCommandScope.RecordDetail("reader", TimeSpan.FromMilliseconds(i), true);
        RequestDbCommandScope.RecordConnection(TimeSpan.FromMilliseconds(7));
        Assert.Equal(24, outer.Commands.Length);
        using (var inner = RequestDbCommandScope.Begin())
        {
            Assert.Empty(inner.Commands);
            RequestDbCommandScope.RecordDetail("advisory_lock", TimeSpan.FromMilliseconds(3), true);
            Assert.Single(inner.Commands);
            Assert.Equal(0, inner.ConnectionMilliseconds);
        }
        Assert.Equal(7, outer.ConnectionMilliseconds);
        Assert.All(outer.Commands, x => Assert.Equal("reader", x.Operation));
    }

    private static RepairStore.RepairLog Measurement(DateTimeOffset timestamp) => new(Guid.NewGuid(), timestamp,
        "backend", "NaderGorge.API.Middleware.RequestPerformanceLoggingMiddleware", "warning",
        "Route=api/video/progress Method=POST EvidenceV=1 ConnectionOpenMs=7 Commands=[]", null);
}

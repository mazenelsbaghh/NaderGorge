namespace NaderGorge.Domain.Entities;

public sealed class AutoRepairIncident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Fingerprint { get; set; } = "";
    public string Source { get; set; } = "";
    public string Category { get; set; } = "";
    public string Level { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int Occurrences { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public Guid? LeaseToken { get; set; }
    public string ProposalHash { get; set; } = "";
    public string ApprovedHash { get; set; } = "";
    public string Summary { get; set; } = "";
    public string ReleaseId { get; set; } = "";
    public List<AutoRepairEvent> Events { get; set; } = [];
}

public sealed class AutoRepairEvent
{
    public long Id { get; set; }
    public Guid? IncidentId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Actor { get; set; } = "";
}

public sealed class AutoRepairControl
{
    public int Id { get; set; } = 1;
    public bool Paused { get; set; } = true;
    public bool AutoDeploy { get; set; }
    public DateTimeOffset? Heartbeat { get; set; }
    public string Runner { get; set; } = "";
    public DateTimeOffset? LogCursor { get; set; }
}

public sealed class AutoRepairLogReceipt
{
    public Guid Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

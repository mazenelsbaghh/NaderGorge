namespace NaderGorge.Domain.Events;

/// <summary>
/// Stable, privacy-safe envelope sent through the staff realtime channel.
/// Keep this contract free of entity snapshots and employee fields.
/// </summary>
public sealed record DataChangedEvent
{
    public const string CurrentSchemaVersion = "2";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public Guid? ActorUserId { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = [];
    public string? EntityType { get; init; }
    public IReadOnlyList<Guid> EntityIds { get; init; } = [];
    public string Operation { get; init; } = DataChangedOperations.Updated;
    public string? Version { get; init; }

    public bool IsValid()
    {
        return SchemaVersion == CurrentSchemaVersion
            && EventId != Guid.Empty
            && OccurredAt != default
            && Scopes.Count > 0
            && Scopes.All(DataChangedScopes.IsAllowed)
            && DataChangedOperations.IsAllowed(Operation)
            && EntityIds.All(id => id != Guid.Empty);
    }
}

public static class DataChangedScopes
{
    private static readonly IReadOnlySet<string> AllowedScopes = new HashSet<string>(StringComparer.Ordinal)
    {
        "activity", "ai", "assessments", "balance", "codes", "comments", "community", "content",
        "crm", "finance", "forms", "gamification", "hr", "media", "notifications", "operations",
        "reports", "settings", "subjects", "users", "watch-requests"
    };

    public static bool IsAllowed(string scope) => !string.IsNullOrWhiteSpace(scope) && AllowedScopes.Contains(scope);
}

public static class DataChangedOperations
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";
    public const string Bulk = "bulk";

    public static bool IsAllowed(string operation) => operation is Created or Updated or Deleted or Bulk;
}

# Query Invalidation Contract

Each migrated query has a typed key and one or more scope mappings. A mutation must declare one of:

- `setQueryData`: apply a trusted response to the exact key.
- `invalidateQueries`: mark affected active queries stale and refetch them according to policy.
- `removeQueries`: remove data only when the entity is deleted or access is no longer valid.

The adapter must match all relevant keys, deduplicate event IDs, debounce bursts, skip inactive queries, and expose counters for invalidation/refetch counts. A domain is not considered migrated while it uses an unregistered service-level cache or an unclassified mutation.

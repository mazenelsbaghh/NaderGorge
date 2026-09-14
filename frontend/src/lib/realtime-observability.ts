export type RealtimeMetric =
  | 'eventAccepted' | 'eventDuplicate' | 'invalidation' | 'refetch'
  | 'reconnect' | 'reconnectDuration' | 'snapshotReconciliation'
  | 'mutationVisibleRefresh' | 'invalidEvent';

const counters = new Map<RealtimeMetric, number>();

export function recordRealtimeMetric(metric: RealtimeMetric): void {
  counters.set(metric, (counters.get(metric) ?? 0) + 1);
}

export function readRealtimeMetrics(): Record<RealtimeMetric, number> {
  return {
    eventAccepted: counters.get('eventAccepted') ?? 0,
    eventDuplicate: counters.get('eventDuplicate') ?? 0,
    invalidation: counters.get('invalidation') ?? 0,
    refetch: counters.get('refetch') ?? 0,
    reconnect: counters.get('reconnect') ?? 0,
    reconnectDuration: counters.get('reconnectDuration') ?? 0,
    snapshotReconciliation: counters.get('snapshotReconciliation') ?? 0,
    mutationVisibleRefresh: counters.get('mutationVisibleRefresh') ?? 0,
    invalidEvent: counters.get('invalidEvent') ?? 0,
  };
}

export function recordMutationVisibleRefresh(): void {
  recordRealtimeMetric('mutationVisibleRefresh');
}

export function recordReconnectDuration(durationMs: number): void {
  if (Number.isFinite(durationMs) && durationMs >= 0) {
    counters.set('reconnectDuration', (counters.get('reconnectDuration') ?? 0) + durationMs);
  }
}

export function recordSnapshotReconciliation(): void {
  recordRealtimeMetric('snapshotReconciliation');
}

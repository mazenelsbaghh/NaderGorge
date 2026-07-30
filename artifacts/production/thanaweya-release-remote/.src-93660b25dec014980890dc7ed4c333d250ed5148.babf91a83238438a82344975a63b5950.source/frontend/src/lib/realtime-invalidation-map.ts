import { invalidateMany } from '@/lib/cache-invalidation';
import type { StaffDataChangedPayload } from '@/lib/staff-realtime-scopes';
import { recordRealtimeMetric } from '@/lib/realtime-observability';

export const REALTIME_SCOPE_KEYS: Record<string, string[]> = {
  users: ['employees', 'users', 'student:shell'],
  subjects: ['subjects', 'content:packages'],
  hr: [
    'hr:employees',
    'hr:organization',
    'hr:contracts',
    'hr:shifts',
    'hr:attendance',
    'hr:corrections',
    'hr:leave',
    'hr:approvals',
    'hr:payroll',
    'hr:financial-requests',
    'hr:documents',
    'hr:assets',
    'hr:performance',
    'hr:cases',
    'hr:recruitment',
    'hr:lifecycle',
    'hr:migration',
    'hr:reports',
  ],
  operations: ['operations:tasks', 'operations:dashboard'],
  crm: ['crm:queues', 'crm:calls', 'crm:reports'],
  support: ['support:staff', 'support:dashboard', 'support:ai'],
  content: ['content:packages', 'content:lessons'],
  codes: ['codes:groups', 'content:packages'],
  finance: ['finance:payroll', 'finance:teacher', 'student:balance', 'reports'],
  assessments: ['student:exams', 'student:homeworks', 'assessments'],
  community: ['community:posts'],
  comments: ['content:comments'],
  notifications: ['notifications', 'student:shell'],
  forms: ['forms'],
  reports: ['reports'],
  media: ['media'],
  settings: ['settings', 'session'],
  balance: ['student:balance', 'student:shell'],
};

const seenEvents = new Map<string, number>();
const EVENT_RETENTION_MS = 60_000;

export function invalidateForStaffDataChanged(payload: StaffDataChangedPayload): boolean {
  const eventId = payload.eventId;
  if (eventId) {
    const seenAt = seenEvents.get(eventId);
    if (seenAt && Date.now() - seenAt < EVENT_RETENTION_MS) {
      recordRealtimeMetric('eventDuplicate');
      return false;
    }
    seenEvents.set(eventId, Date.now());
    for (const [id, timestamp] of seenEvents) {
      if (Date.now() - timestamp >= EVENT_RETENTION_MS) seenEvents.delete(id);
    }
  }

  const keys = new Set<string>();
  for (const scope of payload.scopes) {
    for (const key of REALTIME_SCOPE_KEYS[scope] ?? []) keys.add(key);
  }
  if (keys.size > 0) {
    recordRealtimeMetric('eventAccepted');
    recordRealtimeMetric('invalidation');
    invalidateMany([...keys]);
  }
  return true;
}

/** Routes event handlers through the same scope registry used by staff events.
 * Unknown/detail keys are preserved for entity-specific consumers. */
export function invalidateCanonicalKeys(keys: readonly string[]): void {
  const canonical = new Set<string>();
  const extra = new Set<string>();
  for (const key of keys) {
    let mapped = false;
    for (const scopeKeys of Object.values(REALTIME_SCOPE_KEYS)) {
      if (scopeKeys.includes(key)) {
        mapped = true;
        canonical.add(key);
      }
    }
    if (!mapped) extra.add(key);
  }
  if (canonical.size > 0) {
    recordRealtimeMetric('invalidation');
    invalidateMany([...canonical]);
  }
  if (extra.size > 0) invalidateMany([...extra]);
}

export function resetRealtimeEventDedupe(): void {
  seenEvents.clear();
}

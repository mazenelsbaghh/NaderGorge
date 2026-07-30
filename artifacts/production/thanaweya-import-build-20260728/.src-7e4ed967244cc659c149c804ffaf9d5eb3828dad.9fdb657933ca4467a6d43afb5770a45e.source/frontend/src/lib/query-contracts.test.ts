import { authService, type CurrentSessionSnapshot } from '@/services/auth-service';
import {
  REALTIME_SCOPE_KEYS,
  invalidateForStaffDataChanged,
  resetRealtimeEventDedupe,
} from '@/lib/realtime-invalidation-map';
import {
  flushInvalidations,
  invalidate,
  invalidateMany,
  registerCacheStore,
} from '@/lib/cache-invalidation';
import type { StaffDataChangedPayload } from '@/lib/staff-realtime-scopes';
import { mutationContractRecords, validateQueryContracts } from '@/lib/query-contracts';
import { readRealtimeMetrics, recordMutationVisibleRefresh, recordReconnectDuration } from '@/lib/realtime-observability';

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) throw new Error(`Query contract failed: ${message}`);
}

// Executable contract check; the frontend currently has no Vitest/Jest runner.
const sessionSnapshot: CurrentSessionSnapshot = {
  user: {
    id: '00000000-0000-0000-0000-000000000001',
    fullName: 'موظف الاختبار',
    phone: '20000000003',
    roles: ['Assistant'],
    permissions: ['hr.manage'],
    profileComplete: true,
    allowedDomains: ['hr'],
    allowedNavbarItems: ['employees'],
    authorizationVersion: 7,
  },
  authorizationVersion: 7,
  serverTime: '2026-07-12T00:00:00.000Z',
};

assert(sessionSnapshot.authorizationVersion === sessionSnapshot.user.authorizationVersion, 'session versions stay aligned');
assert(typeof authService.getCurrentSession === 'function', 'current session endpoint is exposed by the auth service');

const invalidated: string[] = [];
const cleanupHrEmployees = registerCacheStore('hr:employees', () => invalidated.push('clear:employees'), () => invalidated.push('refetch:employees'));
const cleanupSession = registerCacheStore('session', () => invalidated.push('clear:session'), () => invalidated.push('refetch:session'));

resetRealtimeEventDedupe();
const currentPayload: StaffDataChangedPayload = {
  schemaVersion: '2',
  eventId: 'event-current-1',
  occurredAt: '2026-07-12T00:00:00.000Z',
  scopes: ['hr', 'settings', 'hr'],
  entityType: 'EmployeeProfile',
  entityIds: ['00000000-0000-0000-0000-000000000002'],
  operation: 'updated',
  version: 8,
};

assert(invalidateForStaffDataChanged(currentPayload), 'first current event is accepted');
flushInvalidations();
assert(invalidated.filter((entry) => entry === 'clear:employees').length === 1, 'HR mapping invalidates employee cache once');
assert(invalidated.filter((entry) => entry === 'clear:session').length === 1, 'settings mapping invalidates session cache');
assert(invalidateForStaffDataChanged(currentPayload) === false, 'duplicate event id is ignored');

// Legacy events did not carry eventId/schemaVersion; they remain valid.
const legacyPayload: StaffDataChangedPayload = { scopes: ['hr'] };
assert(invalidateForStaffDataChanged(legacyPayload), 'legacy payload without optional envelope fields is accepted');
flushInvalidations();
assert(invalidated.filter((entry) => entry === 'clear:employees').length === 2, 'legacy HR event still refreshes employees');

const mappedKeys = new Set(Object.values(REALTIME_SCOPE_KEYS).flat());
assert(mappedKeys.size > 0, 'realtime registry is not empty');
assert(Object.values(REALTIME_SCOPE_KEYS).every((keys) => new Set(keys).size === keys.length), 'scope mappings do not contain duplicate query keys');
assert(validateQueryContracts().length === 0, 'typed query contracts have no structural errors');
assert(mutationContractRecords.length === 27, 'every mutation service file has a typed source contract');
recordMutationVisibleRefresh();
recordReconnectDuration(25);
const metrics = readRealtimeMetrics();
assert(metrics.mutationVisibleRefresh >= 1, 'mutation-to-visible-refresh metric is recorded');
assert(metrics.reconnectDuration >= 25, 'reconnect duration metric is recorded');

const sharedConsumerRefreshes: string[] = [];
const cleanupFirstConsumer = registerCacheStore(
  'shared:employees',
  () => sharedConsumerRefreshes.push('clear:first'),
  () => sharedConsumerRefreshes.push('refetch:first'),
);
const cleanupSecondConsumer = registerCacheStore(
  'shared:employees',
  () => sharedConsumerRefreshes.push('clear:second'),
  () => sharedConsumerRefreshes.push('refetch:second'),
);

invalidate('shared:employees');
assert(sharedConsumerRefreshes.length === 4, 'all active consumers refresh for one cache key');
cleanupFirstConsumer();
invalidate('shared:employees');
assert(
  sharedConsumerRefreshes.slice(4).join(',') === 'clear:second,refetch:second',
  'cleaning up one consumer leaves the other active',
);
cleanupSecondConsumer();
invalidate('shared:employees');
assert(
  sharedConsumerRefreshes.join(',') ===
    'clear:first,refetch:first,clear:second,refetch:second,clear:second,refetch:second',
  'cleaning up all consumers stops future refreshes',
);

const cleanupBatchedConsumer = registerCacheStore(
  'batched:employees',
  () => sharedConsumerRefreshes.push('clear:batched'),
  () => sharedConsumerRefreshes.push('refetch:batched'),
);
invalidateMany(['batched:employees']);
invalidateMany(['batched:employees']);
flushInvalidations();
assert(
  sharedConsumerRefreshes.slice(-2).join(',') === 'clear:batched,refetch:batched',
  'duplicate keys in one debounce window invalidate once',
);
cleanupBatchedConsumer();

cleanupHrEmployees();
cleanupSession();
resetRealtimeEventDedupe();

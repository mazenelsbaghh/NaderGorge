import { recordRealtimeMetric } from '@/lib/realtime-observability';

/**
 * Centralized Cache Invalidation Registry
 *
 * Maps cache keys to store clear/refetch functions.
 * Platform events call `invalidateMany(keys)` to trigger targeted UI updates
 * instead of full page reloads.
 */

type CacheStoreEntry = {
  clear: () => void;
  refetch: () => void;
};

type CacheStoreRegistrations = Map<number, CacheStoreEntry>;

const cacheStores = new Map<string, CacheStoreRegistrations>();
let nextRegistrationId = 1;

// Debounce tracking: prevents stampeding when multiple events arrive in quick succession
let pendingInvalidations = new Set<string>();
let debounceTimer: ReturnType<typeof setTimeout> | null = null;
const DEBOUNCE_MS = 200;

/**
 * Register a cache store with a unique key prefix.
 * Example: registerCacheStore("content:packages", clearFn, refetchFn)
 */
export function registerCacheStore(
  name: string,
  clear: () => void,
  refetch: () => void
): () => void {
  const registrationId = nextRegistrationId++;
  const registrations = cacheStores.get(name) ?? new Map<number, CacheStoreEntry>();
  registrations.set(registrationId, { clear, refetch });
  cacheStores.set(name, registrations);

  let isCleanedUp = false;
  return () => {
    if (isCleanedUp) return;
    isCleanedUp = true;
    unregisterCacheStore(name, registrationId);
  };
}

/**
 * Unregister one cache store registration (for legacy cleanup callers).
 */
export function unregisterCacheStore(name: string, registrationId?: number): void {
  const registrations = cacheStores.get(name);
  if (!registrations) return;

  const idToRemove = registrationId ?? [...registrations.keys()].at(-1);
  if (idToRemove === undefined) return;
  registrations.delete(idToRemove);
  if (registrations.size === 0) cacheStores.delete(name);
}

/**
 * Invalidate a single cache key.
 * Matches by exact key or by prefix (e.g., "content:lesson:abc" matches store "content:lesson").
 */
export function invalidate(key: string): void {
  // Match exact keys and both prefix directions so a broad key reaches all active child queries.
  for (const [storeName, registrations] of cacheStores) {
    if (key.startsWith(storeName) || storeName.startsWith(key)) {
      for (const store of registrations.values()) {
        recordRealtimeMetric('invalidation');
        store.clear();
        recordRealtimeMetric('refetch');
        store.refetch();
      }
    }
  }
}

/**
 * Invalidate multiple cache keys with debouncing.
 * Deduplicates keys within a 200ms window to prevent stampeding.
 */
export function invalidateMany(keys: string[]): void {
  for (const key of keys) {
    pendingInvalidations.add(key);
  }

  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }

  debounceTimer = setTimeout(() => {
    const keysToInvalidate = new Set(pendingInvalidations);
    pendingInvalidations = new Set<string>();
    debounceTimer = null;

    for (const key of keysToInvalidate) {
      invalidate(key);
    }
  }, DEBOUNCE_MS);
}

/**
 * Immediately flush all pending invalidations (useful for tests or critical updates).
 */
export function flushInvalidations(): void {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
    debounceTimer = null;
  }

  const keysToInvalidate = new Set(pendingInvalidations);
  pendingInvalidations = new Set<string>();

  for (const key of keysToInvalidate) {
    invalidate(key);
  }
}

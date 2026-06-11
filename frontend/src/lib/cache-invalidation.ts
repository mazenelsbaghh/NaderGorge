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

const cacheStores = new Map<string, CacheStoreEntry>();

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
): void {
  cacheStores.set(name, { clear, refetch });
}

/**
 * Unregister a cache store (for cleanup).
 */
export function unregisterCacheStore(name: string): void {
  cacheStores.delete(name);
}

/**
 * Invalidate a single cache key.
 * Matches by exact key or by prefix (e.g., "content:lesson:abc" matches store "content:lesson").
 */
export function invalidate(key: string): void {
  // Try exact match first
  const exact = cacheStores.get(key);
  if (exact) {
    exact.clear();
    exact.refetch();
    return;
  }

  // Try prefix match (e.g., "content:lesson:abc-123" matches "content:lesson")
  for (const [storeName, store] of cacheStores) {
    if (key.startsWith(storeName)) {
      store.clear();
      store.refetch();
      return;
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

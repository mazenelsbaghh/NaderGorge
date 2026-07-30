import {
  queryKeyStartsWith,
  stableSerializeQueryKey,
  type PlatformQueryKey,
} from './query-keys.ts';

export type QueryStatus = 'idle' | 'pending' | 'success' | 'error';

export type QuerySnapshot<T> = {
  data: T | undefined;
  error: unknown;
  status: QueryStatus;
  isFetching: boolean;
  updatedAt: number;
};

export type QueryFunction<T> = (context: { signal: AbortSignal }) => Promise<T>;

type QueryListener = () => void;

type QueryEntry<T = unknown> = {
  key: PlatformQueryKey;
  hash: string;
  snapshot: QuerySnapshot<T>;
  listeners: Set<QueryListener>;
  promise: Promise<T> | null;
  controller: AbortController | null;
  generation: number;
};

export type FetchQueryOptions<T> = {
  queryKey: PlatformQueryKey;
  queryFn: QueryFunction<T>;
  staleTime?: number;
  force?: boolean;
};

const EMPTY_SNAPSHOT: QuerySnapshot<never> = Object.freeze({
  data: undefined,
  error: null,
  status: 'idle',
  isFetching: false,
  updatedAt: 0,
});

function now() {
  return Date.now();
}

function isAbortError(error: unknown) {
  return (
    (error instanceof DOMException && error.name === 'AbortError') ||
    (error instanceof Error &&
      (error.name === 'AbortError' || error.name === 'CanceledError'))
  );
}

function invalidatedSnapshot<T>(
  snapshot: QuerySnapshot<T>
): QuerySnapshot<T> {
  return {
    ...snapshot,
    status: snapshot.data === undefined ? 'idle' : 'success',
    isFetching: false,
    updatedAt: 0,
  };
}

export class PlatformQueryClient {
  private readonly entries = new Map<string, QueryEntry>();

  getSnapshot<T>(queryKey: PlatformQueryKey): QuerySnapshot<T> {
    return (
      (this.entries.get(stableSerializeQueryKey(queryKey))
        ?.snapshot as QuerySnapshot<T> | undefined) ??
      (EMPTY_SNAPSHOT as QuerySnapshot<T>)
    );
  }

  subscribe(queryKey: PlatformQueryKey, listener: QueryListener) {
    const entry = this.ensureEntry(queryKey);
    entry.listeners.add(listener);
    return () => {
      entry.listeners.delete(listener);
    };
  }

  fetchQuery<T>({
    queryKey,
    queryFn,
    staleTime = 0,
    force = false,
  }: FetchQueryOptions<T>): Promise<T> {
    const entry = this.ensureEntry<T>(queryKey);
    if (entry.promise) return entry.promise;
    if (
      !force &&
      entry.snapshot.status === 'success' &&
      now() - entry.snapshot.updatedAt <= staleTime
    ) {
      return Promise.resolve(entry.snapshot.data as T);
    }

    const controller = new AbortController();
    const generation = entry.generation;
    entry.controller = controller;
    entry.snapshot = {
      ...entry.snapshot,
      error: null,
      status: entry.snapshot.data === undefined ? 'pending' : entry.snapshot.status,
      isFetching: true,
    };
    this.notify(entry);

    const promise = queryFn({ signal: controller.signal })
      .then((data) => {
        if (controller.signal.aborted) {
          throw new DOMException('Query cancelled', 'AbortError');
        }
        entry.snapshot =
          entry.generation === generation
            ? {
                data,
                error: null,
                status: 'success',
                isFetching: false,
                updatedAt: now(),
              }
            : invalidatedSnapshot(entry.snapshot);
        return data;
      })
      .catch((error: unknown) => {
        if (entry.generation !== generation) {
          entry.snapshot = invalidatedSnapshot(entry.snapshot);
        } else if (isAbortError(error)) {
          entry.snapshot = {
            ...entry.snapshot,
            status: entry.snapshot.data === undefined ? 'idle' : 'success',
            isFetching: false,
          };
        } else {
          entry.snapshot = {
            ...entry.snapshot,
            error,
            status: 'error',
            isFetching: false,
          };
        }
        throw error;
      })
      .finally(() => {
        if (entry.controller === controller) entry.controller = null;
        if (entry.promise === promise) entry.promise = null;
        this.notify(entry);
      });

    entry.promise = promise;
    return promise;
  }

  setQueryData<T>(
    queryKey: PlatformQueryKey,
    update: T | ((current: T | undefined) => T)
  ) {
    const entry = this.ensureEntry<T>(queryKey);
    entry.controller?.abort();
    entry.generation += 1;
    const nextData =
      typeof update === 'function'
        ? (update as (current: T | undefined) => T)(entry.snapshot.data)
        : update;
    entry.snapshot = {
      data: nextData,
      error: null,
      status: 'success',
      isFetching: false,
      updatedAt: now(),
    };
    this.notify(entry);
  }

  invalidateQueries(prefix: PlatformQueryKey) {
    for (const entry of this.entries.values()) {
      if (!queryKeyStartsWith(entry.key, prefix)) continue;
      entry.generation += 1;
      entry.snapshot = { ...entry.snapshot, updatedAt: 0 };
      this.notify(entry);
    }
  }

  removeQueries(prefix: PlatformQueryKey = []) {
    for (const [hash, entry] of this.entries) {
      if (!queryKeyStartsWith(entry.key, prefix)) continue;
      entry.controller?.abort();
      entry.snapshot = EMPTY_SNAPSHOT;
      this.notify(entry);
      this.entries.delete(hash);
    }
  }

  cancelQueries(prefix: PlatformQueryKey = []) {
    for (const entry of this.entries.values()) {
      if (queryKeyStartsWith(entry.key, prefix)) entry.controller?.abort();
    }
  }

  isStale(queryKey: PlatformQueryKey, staleTime: number) {
    const snapshot = this.getSnapshot(queryKey);
    return (
      snapshot.status !== 'success' ||
      now() - snapshot.updatedAt > Math.max(0, staleTime)
    );
  }

  private ensureEntry<T>(queryKey: PlatformQueryKey): QueryEntry<T> {
    const hash = stableSerializeQueryKey(queryKey);
    const existing = this.entries.get(hash);
    if (existing) return existing as QueryEntry<T>;
    const entry: QueryEntry<T> = {
      key: queryKey,
      hash,
      snapshot: EMPTY_SNAPSHOT as QuerySnapshot<T>,
      listeners: new Set(),
      promise: null,
      controller: null,
      generation: 0,
    };
    this.entries.set(hash, entry);
    return entry;
  }

  private notify(entry: QueryEntry) {
    for (const listener of entry.listeners) listener();
  }
}

export const platformQueryClient = new PlatformQueryClient();

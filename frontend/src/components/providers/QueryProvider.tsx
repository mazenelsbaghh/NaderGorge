'use client';

import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useSyncExternalStore,
  type ReactNode,
} from 'react';

import {
  PlatformQueryClient,
  platformQueryClient,
  type QueryFunction,
} from '@/lib/query-client';
import {
  stableSerializeQueryKey,
  type PlatformQueryKey,
} from '@/lib/query-keys';

const QueryClientContext = createContext<PlatformQueryClient | null>(null);

export function QueryProvider({ children }: { children: ReactNode }) {
  return (
    <QueryClientContext.Provider value={platformQueryClient}>
      {children}
    </QueryClientContext.Provider>
  );
}

export function usePlatformQueryClient() {
  const client = useContext(QueryClientContext);
  if (!client)
    throw new Error('usePlatformQueryClient must be used inside QueryProvider');
  return client;
}

export function usePlatformQuery<T>({
  queryKey,
  queryFn,
  staleTime = 0,
  enabled = true,
}: {
  queryKey: PlatformQueryKey;
  queryFn: QueryFunction<T>;
  staleTime?: number;
  enabled?: boolean;
}) {
  const client = usePlatformQueryClient();
  const keyHash = stableSerializeQueryKey(queryKey);
  const stableKey = useMemo(
    () => JSON.parse(keyHash) as PlatformQueryKey,
    [keyHash]
  );
  const snapshot = useSyncExternalStore(
    (listener) => client.subscribe(stableKey, listener),
    () => client.getSnapshot<T>(stableKey),
    () => client.getSnapshot<T>(stableKey)
  );

  useEffect(() => {
    if (!enabled || !client.isStale(stableKey, staleTime)) return;
    void client
      .fetchQuery({ queryKey: stableKey, queryFn, staleTime })
      .catch(() => undefined);
  }, [
    client,
    enabled,
    queryFn,
    snapshot.isFetching,
    snapshot.updatedAt,
    stableKey,
    staleTime,
  ]);

  return {
    ...snapshot,
    refetch: () =>
      client.fetchQuery({
        queryKey: stableKey,
        queryFn,
        staleTime,
        force: true,
      }),
  };
}

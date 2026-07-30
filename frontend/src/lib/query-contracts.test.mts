import assert from 'node:assert/strict';
import test from 'node:test';

import { PlatformQueryClient } from './query-client.ts';
import {
  normalizeQueryParameters,
  queryKeyStartsWith,
  stableSerializeQueryKey,
} from './query-keys.ts';

test('query keys normalize parameters and preserve resource boundaries', () => {
  const normalized = normalizeQueryParameters({
    search: '  طالب  ',
    pageSize: 25,
    omitted: undefined,
  });

  assert.deepEqual(normalized, { pageSize: 25, search: 'طالب' });
  assert.equal(
    stableSerializeQueryKey(['students', { search: 'x', page: 1 }]),
    stableSerializeQueryKey(['students', { page: 1, search: 'x' }]),
  );
  assert.equal(
    queryKeyStartsWith(
      ['student', 'packages', 'user-a'],
      ['student', 'packages'],
    ),
    true,
  );
  assert.equal(
    queryKeyStartsWith(['student', 'teachers'], ['student', 'packages']),
    false,
  );
});

test('identical in-flight and fresh reads reuse the authoritative result', async () => {
  const client = new PlatformQueryClient();
  const key = ['student', 'dashboard', 'user-a'] as const;
  let resolveFetch: ((value: string) => void) | undefined;
  let fetchCount = 0;
  const queryFn = ({ signal }: { signal: AbortSignal }) =>
    new Promise<string>((resolve, reject) => {
      fetchCount += 1;
      resolveFetch = resolve;
      signal.addEventListener(
        'abort',
        () => reject(new DOMException('cancelled', 'AbortError')),
        { once: true },
      );
    });

  const first = client.fetchQuery({ queryKey: key, queryFn, staleTime: 60_000 });
  const duplicate = client.fetchQuery({
    queryKey: key,
    queryFn,
    staleTime: 60_000,
  });

  assert.strictEqual(first, duplicate);
  assert.equal(fetchCount, 1);
  resolveFetch?.('ready');
  assert.equal(await first, 'ready');
  assert.equal(client.getSnapshot<string>(key).data, 'ready');

  const fresh = await client.fetchQuery({
    queryKey: key,
    queryFn: async () => 'unexpected',
    staleTime: 60_000,
  });
  assert.equal(fresh, 'ready');
  assert.equal(fetchCount, 1);
});

test('targeted invalidation and identity removal update observable cache state', async () => {
  const client = new PlatformQueryClient();
  const key = ['student', 'dashboard', 'user-a'] as const;
  await client.fetchQuery({
    queryKey: key,
    queryFn: async () => 'ready',
    staleTime: 60_000,
  });

  client.invalidateQueries(['student', 'dashboard']);
  assert.equal(client.isStale(key, 60_000), true);

  client.removeQueries(['student']);
  assert.equal(client.getSnapshot(key).status, 'idle');
});

test('cancelled reads expose AbortError without caching an error', async () => {
  const client = new PlatformQueryClient();
  const key = ['admin', 'students', { search: 'old' }] as const;
  const cancelled = client.fetchQuery({
    queryKey: key,
    queryFn: ({ signal }) =>
      new Promise<string>((resolve, reject) => {
        signal.addEventListener(
          'abort',
          () => reject(new DOMException('cancelled', 'AbortError')),
          { once: true },
        );
      }),
  });

  client.cancelQueries(['admin', 'students']);

  await assert.rejects(
    cancelled,
    (error: unknown) =>
      error instanceof DOMException && error.name === 'AbortError',
  );
  assert.equal(client.getSnapshot(key).status, 'idle');
});

test('an invalidated pre-event response cannot overwrite post-event state', async () => {
  const client = new PlatformQueryClient();
  const key = ['student', 'dashboard', 'race-user'] as const;
  let resolvePreEvent:
    | ((value: { version: 'before' | 'after' }) => void)
    | undefined;
  const preEventRead = client.fetchQuery({
    queryKey: key,
    queryFn: () =>
      new Promise((resolve) => {
        resolvePreEvent = resolve;
      }),
    staleTime: 60_000,
  });

  client.invalidateQueries(['student', 'dashboard']);
  resolvePreEvent?.({ version: 'before' });
  await preEventRead;

  assert.equal(client.getSnapshot(key).data, undefined);
  assert.equal(client.isStale(key, 60_000), true);

  const postEventRead = await client.fetchQuery({
    queryKey: key,
    queryFn: async () => ({ version: 'after' as const }),
    staleTime: 60_000,
  });
  assert.equal(postEventRead.version, 'after');
  assert.equal(
    client.getSnapshot<{ version: string }>(key).data?.version,
    'after',
  );
});

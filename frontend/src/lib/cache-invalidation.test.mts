import assert from 'node:assert/strict';
import test from 'node:test';
import { flushInvalidations, invalidate, invalidateMany, registerCacheStore } from './cache-invalidation.ts';

test('2026-09-06 overlapping events refetch a lesson once without confusing comments or other ids', () => {
  let requests = 0;
  const cleanup = registerCacheStore('content:lesson:123:detail', () => {}, () => { requests++; });
  try {
    invalidateMany(['content:lesson:123', 'content:lesson:123:detail']);
    flushInvalidations();
    assert.equal(requests, 1);
    invalidate('content:lesson:123:comments');
    invalidate('content:lesson:12');
    assert.equal(requests, 1);
    invalidate('content:lesson:123');
    assert.equal(requests, 2);
  } finally {
    cleanup();
  }
});

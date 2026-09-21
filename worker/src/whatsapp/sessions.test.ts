import assert from 'node:assert/strict';
import test from 'node:test';
import { connectionSnapshot } from './sessions.js';

test('connection state exposes a live QR to status polling', () => {
  const snapshot = connectionSnapshot('connecting', 'data:image/png;base64,dGVzdA==', 31_000, 1_000);

  assert.deepEqual(snapshot, {
    instance: { state: 'connecting' },
    base64: 'data:image/png;base64,dGVzdA==',
    qrExpiresAt: 31_000,
  });
});

test('connection state never exposes an expired QR', () => {
  assert.deepEqual(
    connectionSnapshot('connecting', 'data:image/png;base64,b2xk', 1_000, 1_000),
    { instance: { state: 'connecting' } },
  );
});

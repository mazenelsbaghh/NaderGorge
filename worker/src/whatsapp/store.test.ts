import test from 'node:test';
import assert from 'node:assert/strict';
import { randomBytes } from 'node:crypto';
import { Pool } from 'pg';
import { BaileysStateStore } from './store.js';

test('session credentials survive encryption without exposing keys and cannot be moved between accounts', () => {
  const pool = new Pool();
  const store = new BaileysStateStore(pool, randomBytes(32).toString('base64'));
  const credentials = { privateKey: randomBytes(32), token: 'session-token-private' };
  const encrypted = store.encrypt(credentials, 'account-a:creds');
  assert.equal(encrypted.includes(credentials.token), false);
  assert.deepEqual(store.decrypt(encrypted, 'account-a:creds'), credentials);
  assert.throws(() => store.decrypt(encrypted, 'account-b:creds'));
  const corrupted = Buffer.from(encrypted, 'base64');
  corrupted[corrupted.length - 1] = corrupted[corrupted.length - 1]! ^ 1;
  assert.throws(() => store.decrypt(corrupted.toString('base64'), 'account-a:creds'));
});

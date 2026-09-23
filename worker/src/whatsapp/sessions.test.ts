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

test('a new connect request replaces a pairing session stalled without a QR', async () => {
  const { BaileysSessions } = await import('./sessions.js');
  const sessions = new BaileysSessions({} as never, {} as never);
  let ended = false;
  let opened = false;
  sessions['sessions'].set('session-a', {
    accountId: 'support-a', socket: { end: () => { ended = true; } },
    state: 'connecting', stopped: false, retries: 0, lastProgressAt: Date.now() - 46_000,
  } as never);
  sessions['open'] = async () => {
    opened = true;
    return { state: 'connecting', qr: 'data:image/png;base64,dGVzdA==', qrExpiresAt: Date.now() + 30_000 } as never;
  };

  const snapshot = await sessions.connection('session-a');
  assert.equal(ended, true);
  assert.equal(opened, true);
  assert.equal(snapshot.base64, 'data:image/png;base64,dGVzdA==');
  sessions.close();
});

test('phone replies and historical messages enter the durable callback queue', async () => {
  const { BaileysSessions } = await import('./sessions.js');
  const queued: unknown[] = [];
  const store = { enqueue: async (_account: string, payload: unknown) => { queued.push(payload); } };
  const sessions = new BaileysSessions({} as never, store as never);
  const session = { accountId: 'support-a', socket: {} } as never;
  await sessions['receive']('session-a', session, {
    key: { remoteJid: '201099999999@s.whatsapp.net', id: 'mobile-1', fromMe: true },
    message: { conversation: 'phone reply' }, messageTimestamp: 1789236000,
  });
  await sessions['receive']('session-a', session, {
    key: { remoteJid: '201099999999@s.whatsapp.net', id: 'old-1' },
    message: { conversation: 'old message' }, messageTimestamp: 1789235000,
  }, true);
  assert.equal(queued.length, 2);
  const callbacks = queued as Array<{ history: boolean; data: { key: { fromMe?: boolean }; messageTimestamp: string } }>;
  assert.equal(callbacks[0]?.data.key.fromMe, true);
  assert.equal(callbacks[0]?.history, false);
  assert.equal(callbacks[1]?.history, true);
  assert.equal(callbacks[1]?.data.messageTimestamp, '1789235000');
});

test('a phone mapping arriving late is resolved on a subsequent delivery attempt', async () => {
  const { BaileysSessions } = await import('./sessions.js');
  const sessions = new BaileysSessions({} as never, {} as never);
  let phone: string | null = null;
  sessions['sessions'].set('session-a', { socket: { signalRepository: { lidMapping: {
    getPNForLID: async () => phone,
  } } } } as never);
  const callback = { sessionId: 'session-a', event: 'message', data: {
    key: { remoteJid: '123@lid', id: 'message-1' } as { remoteJid: string; id: string; remoteJidAlt?: string },
  } };
  await assert.rejects(sessions.prepareCallback(callback), /mapping pending/);
  phone = '201099999999@s.whatsapp.net';
  assert.equal(await sessions.prepareCallback(callback), callback);
  assert.equal(callback.data.key.remoteJidAlt, phone);
});

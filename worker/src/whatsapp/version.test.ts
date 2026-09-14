import assert from 'node:assert/strict';
import test from 'node:test';
import { currentWhatsAppVersion } from './version.js';

test('pairing uses the live WhatsApp revision instead of the bundled revision', async context => {
  context.mock.method(globalThis, 'fetch', async (url: string, options: RequestInit) => {
    assert.equal(url, 'https://web.whatsapp.com/sw.js');
    assert.ok(options.signal);
    return new Response('{"client_revision":1047430678}');
  });
  assert.deepEqual(await currentWhatsAppVersion(), [2, 3000, 1047430678]);
});

for (const response of [
  { body: 'unavailable', status: 503 },
  { body: '{}', status: 200 },
]) {
  test(`pairing refuses an unverified bundled fallback: ${response.status} ${response.body}`, async context => {
    context.mock.method(globalThis, 'fetch', async () => new Response(response.body, { status: response.status }));
    await assert.rejects(currentWhatsAppVersion(), /Could not resolve the current WhatsApp Web version/);
  });
}

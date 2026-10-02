import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import { mintVcdnPlaybackSource } from './vcdn-playback-source.ts';
import { PlaybackRequestError } from './video-playback-session.ts';

const id = '11111111-1111-4111-8111-111111111111';
const origin = 'https://app.massar-academy.net';
const request = () => new Request(`${origin}/api/video/session`, {headers:{Referer: `${origin}/student/lessons/123`}});
const originalKey = process.env.VCDN_API_KEY;
const originalProtection = process.env.VCDN_REFERRER_PROTECTION_ENABLED;
const originalPlatformPlayback = process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED;
before(() => {process.env.VCDN_API_KEY = 'synthetic-secret'; process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'true';});
after(() => {
  if (originalKey === undefined) delete process.env.VCDN_API_KEY; else process.env.VCDN_API_KEY = originalKey;
  if (originalProtection === undefined) delete process.env.VCDN_REFERRER_PROTECTION_ENABLED; else process.env.VCDN_REFERRER_PROTECTION_ENABLED = originalProtection;
  if (originalPlatformPlayback === undefined) delete process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED; else process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED = originalPlatformPlayback;
});

test('explicit platform-session playback verifies project ownership, bounds our grant and never fetches media', async t => {
  process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'false';
  process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED = 'true';
  try {
    const contactedHosts: string[] = [];
    t.mock.method(globalThis, 'fetch', async (input: string | URL | Request, options?: RequestInit) => {
      const url = new URL(String(input)); contactedHosts.push(url.hostname);
      assert.doesNotMatch(url.pathname, /\.m3u8|\/seg\//);
      if (url.hostname === 'cdn.vcdn.me') {
        assert.equal(new Headers(options?.headers).get('X-API-Key'), 'synthetic-secret');
        return Response.json({ id, status: 'ready' });
      }
      assert.equal(new Headers(options?.headers).get('X-API-Key'), null);
      return Response.json({ videoId: id, mode: 'hls', expires: Math.floor(Date.now() / 1000) + 600,
        streamUrl: `https://cdn.vcdn.me/stream/${id}/providers/p1/master.m3u8?token=public-provider-token` });
    });
    const resolved = await mintVcdnPlaybackSource(request(), id, Date.now() + 600_000);
    assert.equal(resolved.protection, 'platform-session');
    assert.ok(resolved.expiresAt <= Date.now() + 180_000);
    assert.deepEqual(contactedHosts, ['cdn.vcdn.me', 'embed.vcdn.me']);
    assert.doesNotMatch(JSON.stringify(resolved), /synthetic-secret/);
  } finally { process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'true'; delete process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED; }
});

test('platform-session mode refuses another project video and mismatched provider config', async t => {
  process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'false';
  process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED = 'true';
  try {
    for (const scenario of ['unowned', 'another-source', 'progressive', 'expired'] as const) {
      t.mock.method(globalThis, 'fetch', async (input: string | URL | Request) => {
        if (String(input).includes('/api/v1/')) return Response.json({ id: scenario === 'unowned' ? 'another-id' : id, status: 'ready' });
        return Response.json({ videoId: id, mode: scenario === 'progressive' ? 'progressive' : 'hls',
          expires: scenario === 'expired' ? 1 : Math.floor(Date.now() / 1000) + 600,
          streamUrl: `https://cdn.vcdn.me/stream/${scenario === 'another-source' ? '22222222-2222-4222-8222-222222222222' : id}/providers/p1/master.m3u8?token=public` });
      });
      await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now() + 600_000), status(scenario === 'unowned' ? 422 : scenario === 'expired' ? 410 : 502));
      t.mock.restoreAll();
    }
  } finally { process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'true'; delete process.env.VCDN_PLATFORM_SESSION_PLAYBACK_ENABLED; }
});
const status = (expected: number) => (error: unknown) => error instanceof PlaybackRequestError && error.status === expected;
function grant() {
  return {videoId:id, token:'signed', exp: Math.floor(Date.now()/1000) + 300,
    streamUrl:`https://stream.vcdn.me/${id}/master.m3u8?token=signed`};
}

test('VCDN tokens are bounded by the watch session and bind only its origin without exposing lesson paths', async t => {
  const calls: RequestInit[] = [];
  t.mock.method(globalThis, 'fetch', async (_: unknown, options: RequestInit) => {calls.push(options); return Response.json(grant());});
  const sessionExpiry = Date.now() + 300_500;
  const result = await mintVcdnPlaybackSource(request(), id, sessionExpiry);
  assert.ok(result.expiresAt <= sessionExpiry);
  assert.ok(result.serverNowMs <= Date.now());
  const ttl = JSON.parse(String(calls[0].body)).ttlSeconds;
  assert.ok(ttl >= 299 && ttl <= 300);
  assert.equal(new Headers(calls[0].headers).get('Referer'), origin + '/');
});

for (const [name, patch] of [
  ['unsigned source', {streamUrl:`https://stream.vcdn.me/${id}/master.m3u8`}],
  ['foreign CDN', {streamUrl:`https://attacker.test/${id}/master.m3u8?token=signed`}],
  ['another video', {videoId:'22222222-2222-4222-8222-222222222222'}],
  ['mismatched token', {token:'different'}],
  ['duplicate token', {streamUrl:`https://stream.vcdn.me/${id}/master.m3u8?token=signed&token=other`}],
  ['expired source', {exp:1}],
  ['overlong source', {exp:Math.floor(Date.now()/1000)+3600}],
] as const) {
  test(`VCDN refuses ${name} rather than rendering an unprotected fallback`, async t => {
    t.mock.method(globalThis, 'fetch', async () => Response.json({...grant(), ...patch}));
    await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now()+600_000), status(502));
  });
}

test('missing credentials, unverified protection, expired sessions and foreign contexts never contact VCDN', async t => {
  const upstream = t.mock.method(globalThis, 'fetch', async () => {throw new Error('Should never contact VCDN');});
  delete process.env.VCDN_API_KEY;
  await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now()+600_000), status(503));
  process.env.VCDN_API_KEY = 'synthetic-secret';
  process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'false';
  try {await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now()+600_000), status(503));}
  finally {process.env.VCDN_REFERRER_PROTECTION_ENABLED = 'true';}
  await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now()+59_000), status(410));
  await assert.rejects(mintVcdnPlaybackSource(request(), id, NaN), status(410));
  const foreign = new Request(request().url, {headers:{Referer:'https://attacker.test/'}});
  await assert.rejects(mintVcdnPlaybackSource(foreign, id, Date.now()+600_000), status(403));
  await assert.rejects(mintVcdnPlaybackSource(new Request(request().url), id, Date.now()+600_000), status(403));
  assert.equal(upstream.mock.callCount(), 0);
});

test('VCDN provider outages do not expose provider responses or fall back to unsigned playback', async t => {
  t.mock.method(globalThis, 'fetch', async () => new Response('provider-private-details', {status:503}));
  await assert.rejects(mintVcdnPlaybackSource(request(), id, Date.now()+600_000), status(503));
});

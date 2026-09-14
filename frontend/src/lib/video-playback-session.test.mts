import assert from 'node:assert/strict';
import { after, before, test } from 'node:test';
import {
  createPlaybackCookie, fetchPlaybackMaterial, playbackCookieName, playbackErrorResponse,
  PlaybackRequestError, readPlaybackSession, readPlaybackBootstrap, sealPlaybackSession,
} from './video-playback-session.ts';

const sessionId = '11111111-1111-4111-8111-111111111111';
const otherSessionId = '22222222-2222-4222-8222-222222222222';
const authorization = 'Bearer synthetic.access.token';
const originalSecret = process.env.API_CALLBACK_SECRET;
before(() => { process.env.API_CALLBACK_SECRET = 'test-only-session-sealing-secret'; });
after(() => {
  if (originalSecret === undefined) delete process.env.API_CALLBACK_SECRET;
  else process.env.API_CALLBACK_SECRET = originalSecret;
});

function request(cookie?: string) {
  return new Request(`https://app.massar-academy.net/api/video/embed?s=${sessionId}`, {
    headers: { Authorization: authorization, 'X-App-Surface': 'student', ...(cookie ? { Cookie: cookie } : {}) },
  });
}

function isUnauthorized(error: unknown) {
  return error instanceof PlaybackRequestError && error.status === 401;
}

test('a copied iframe link without the browser cookie cannot obtain playback material', () => {
  assert.throws(() => readPlaybackBootstrap(request(), sessionId), isUnauthorized);
});

test('issued playback cookie hides credentials and binds the authorized browser to its video session', () => {
  const cookie = createPlaybackCookie(request(), sessionId, new Date(Date.now() + 3_600_000).toISOString());
  assert.match(cookie, /; HttpOnly; SameSite=Strict;/);
  assert.match(cookie, /; Secure$/);
  assert.doesNotMatch(cookie, /synthetic|Domain=/);
  const browser = readPlaybackBootstrap(request(cookie), sessionId);
  assert.equal(browser.authorization, authorization);
  assert.equal(browser.surface, 'student');
  assert.ok(browser.bootstrapExpiresAt <= Date.now() + 90_000);
  assert.ok(browser.expiresAt <= Date.now() + 35 * 60_000);
  const swappedCookie = cookie.replace(playbackCookieName(sessionId), playbackCookieName(otherSessionId));
  assert.throws(() => readPlaybackSession(request(swappedCookie), otherSessionId), isUnauthorized);
});

test('an expired bootstrap cannot reopen the player while its active relay grant still works', () => {
  const sealed = sealPlaybackSession({ sessionId, authorization, surface: 'student',
    bootstrapExpiresAt: Date.now() - 1, expiresAt: Date.now() + 60_000 });
  const browserRequest = request(`${playbackCookieName(sessionId)}=${sealed}`);
  assert.throws(() => readPlaybackBootstrap(browserRequest, sessionId), isUnauthorized);
  assert.equal(readPlaybackSession(browserRequest, sessionId).sessionId, sessionId);
});

test('expired and modified browser credentials are denied', async t => {
  for (const scenario of ['expired', 'tampered'] as const) {
    await t.test(scenario, () => {
      let sealed = sealPlaybackSession({ sessionId, authorization, surface: 'student',
        bootstrapExpiresAt: Date.now() + 60_000, expiresAt: Date.now() + (scenario === 'expired' ? -1 : 60_000) });
      if (scenario === 'tampered') sealed = (sealed[0] === 'A' ? 'B' : 'A') + sealed.slice(1);
      assert.throws(() => readPlaybackSession(request(`${playbackCookieName(sessionId)}=${sealed}`), sessionId), isUnauthorized);
    });
  }
});

test('short remaining watch sessions cap the browser grant', () => {
  const expiresAt = new Date(Date.now() + 30_000).toISOString();
  const cookie = createPlaybackCookie(request(), sessionId, expiresAt);
  const session = readPlaybackBootstrap(request(cookie), sessionId);
  assert.equal(session.expiresAt, Date.parse(expiresAt));
  assert.equal(session.bootstrapExpiresAt, Date.parse(expiresAt));
});

test('staff permissions do not overflow the browser cookie or lose the original bearer', () => {
  const claims = { permission: Array.from({ length: 180 }, (_, index) => `content.section.${index}.manage`) };
  const token = `Bearer header.${Buffer.from(JSON.stringify(claims)).toString('base64url')}.signature`;
  assert.ok(token.length > 3800);
  const staffRequest = new Request(`https://admin.massar-academy.net/api/video/session`, { headers: { Authorization: token } });
  const cookie = createPlaybackCookie(staffRequest, sessionId, new Date(Date.now() + 3_600_000).toISOString());
  assert.ok(cookie.length < 4096);
  assert.equal(readPlaybackBootstrap(request(cookie), sessionId).authorization, token);
});

test('material retrieval carries authenticated ownership to the backend and does not cache or redirect', async t => {
  t.mock.method(globalThis, 'fetch', async (url: string, options: RequestInit) => {
    assert.match(String(url), new RegExp(`/video-sessions/${sessionId}/embed-material`));
    const headers = new Headers(options.headers);
    assert.equal(headers.get('Authorization'), authorization);
    assert.equal(headers.get('X-App-Surface'), 'student');
    assert.equal(headers.get('X-Internal-Token'), process.env.API_CALLBACK_SECRET);
    assert.equal(options.cache, 'no-store');
    assert.equal(options.redirect, 'error');
    return Response.json({ token: 'encrypted-material', key: 'session-key' });
  });
  const material = await fetchPlaybackMaterial(request(), sessionId, { authorization, surface: 'student' });
  assert.equal(material.token, 'encrypted-material');
});

test('backend authorization failures remain denials without leaking upstream details', async t => {
  for (const status of [401, 403, 404, 410, 429]) {
    await t.test(String(status), async inner => {
      inner.mock.method(globalThis, 'fetch', async () => new Response('private provider URL and credentials', { status }));
      try {
        await fetchPlaybackMaterial(request(), sessionId, { authorization });
        assert.fail('Expected authorization failure');
      } catch (error) {
        const response = playbackErrorResponse(error);
        assert.equal(response.status, status);
        assert.equal(response.headers.get('Cache-Control'), 'no-store, private');
        assert.doesNotMatch(await response.text(), /provider|credentials/);
      }
    });
  }
});

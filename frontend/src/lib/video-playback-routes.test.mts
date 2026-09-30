import assert from 'node:assert/strict';
import { createCipheriv } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';
import { createYouTubeHlsFetchFixture } from './youtube-hls-test-fixtures.mts';

const root = fileURLToPath(new URL('../', import.meta.url));
const nativeRequire = createRequire(import.meta.url);
type RouteExports = Record<string, (request: Request) => Promise<Response>>;
const modules = new Map<string, { exports: RouteExports }>();

// Execute the actual route handlers and their local dependencies; only upstream HTTP is substituted.
function loadModule(path: string): RouteExports {
  const filename = path.endsWith('.ts') ? path : path + '.ts';
  const cached = modules.get(filename);
  if (cached) return cached.exports;
  const compiledModule = { exports: {} };
  modules.set(filename, compiledModule);
  const code = ts.transpileModule(readFileSync(filename, 'utf8'), { compilerOptions: {
    target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, esModuleInterop: true,
  } }).outputText;
  vm.runInNewContext(code, {
    module: compiledModule, exports: compiledModule.exports, process, Buffer, URL, URLSearchParams, Request, Response, Headers, AbortSignal,
    setTimeout, clearTimeout, DOMException, Uint8Array, ReadableStream,
    fetch: (...args: Parameters<typeof fetch>) => globalThis.fetch(...args),
    require: (specifier: string) => specifier.startsWith('@/') ? loadModule(resolve(root, specifier.slice(2)))
      : specifier.startsWith('.') ? loadModule(resolve(dirname(filename), specifier)) : nativeRequire(specifier),
  }, { filename });
  return compiledModule.exports;
}

const sessionId = '11111111-1111-4111-8111-111111111111';
const origin = 'https://app.massar-academy.net';
const originalSecret = process.env.API_CALLBACK_SECRET;
before(() => { process.env.API_CALLBACK_SECRET = 'synthetic-route-tests-secret'; });
after(() => {
  if (originalSecret === undefined) delete process.env.API_CALLBACK_SECRET;
  else process.env.API_CALLBACK_SECRET = originalSecret;
});

function backendMaterial(studentName = 'طالب تجربة', provider = 'youtube', videoId = 'testVideo12') {
  const key = Buffer.alloc(32, 7);
  const nonce = Buffer.alloc(12, 8);
  const cipher = createCipheriv('aes-256-gcm', key, nonce);
  const encrypted = Buffer.concat([cipher.update(JSON.stringify({ Provider: provider, VideoId: videoId, StudentName: studentName }), 'utf8'), cipher.final()]);
  return { token: Buffer.concat([nonce, encrypted, cipher.getAuthTag()]).toString('base64'), key: key.toString('base64'),
    expiresAt: new Date(Date.now() + 3_600_000).toISOString() };
}

function browserRequest(path: string, options: { cookie?: string; method?: string; body?: object; destination?: string } = {}) {
  return new Request(`${origin}/api/video/${path}`, {
    method: options.method ?? 'GET',
    headers: { Authorization: 'Bearer synthetic.access.token', Referer: `${origin}/student/lesson`,
      'Sec-Fetch-Site': 'same-origin', 'Sec-Fetch-Dest': options.destination ?? 'empty',
      'Content-Type': 'application/json', ...(options.cookie ? { Cookie: options.cookie } : {}) },
    body: options.body ? JSON.stringify(options.body) : undefined,
  });
}

test('authorized bootstrap keeps the initial document ID-free and rejects a copied link in another browser', async t => {
  t.mock.method(globalThis, 'fetch', async (input: string | URL | Request) => {
    assert.equal(new URL(String(input)).pathname, `/api/v1/internal/video-sessions/${sessionId}/embed-material`);
    return Response.json(backendMaterial());
  });
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const embedRoute = loadModule(resolve(root, 'app/api/video/embed/route'));
  const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  assert.equal(started.status, 200);
  assert.doesNotMatch(await started.text(), /testVideo12|token|key/);
  const cookie = started.headers.get('Set-Cookie')!;
  const shell = await embedRoute.GET(browserRequest(`embed?s=${sessionId}`, { cookie, destination: 'iframe' }));
  assert.equal(shell.status, 200);
  assert.doesNotMatch(await shell.text(), /testVideo12|youtube\.com|_vid|_k\s*=/);
  const copied = await materialRoute.GET(browserRequest(`material?s=${sessionId}`));
  assert.equal(copied.status, 401);
  const actual = await materialRoute.GET(browserRequest(`material?s=${sessionId}`, { cookie }));
  assert.equal(actual.status, 200);
  assert.match(await actual.text(), /window\.onYouTubeIframeAPIReady/);
});

test('a revoked permission at material issuance is not bypassed by an earlier browser grant', async t => {
  const upstream = t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial()));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  upstream.mock.mockImplementation(async () => new Response('sensitive-backend-denial', { status: 403 }));
  const denied = await materialRoute.GET(browserRequest(`material?s=${sessionId}`, { cookie: started.headers.get('Set-Cookie')! }));
  assert.equal(denied.status, 403);
  assert.doesNotMatch(await denied.text(), /sensitive-backend-denial|testVideo12/);
});

test('profile text cannot break out of YouTube player scripts', async t => {
  t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial('</script><script>window.stolen=true</script>')));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  const response = await materialRoute.GET(browserRequest(`material?s=${sessionId}`, { cookie: started.headers.get('Set-Cookie')! }));
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.doesNotMatch(html, /<script>window\.stolen/);
  assert.match(html, /\\u003c\/script>/);
});

test('Bunny renewal forwards current authorization and compatibility mode, returning only the renewable source', async t => {
  const expires = Math.floor(Date.now() / 1000) + 300;
  const source = `https://library.b-cdn.net/bcdn_token=HS256-synthetic&expires=${expires}&token_path=%2F${sessionId}%2F/${sessionId}/playlist.m3u8`;
  const upstream = t.mock.method(globalThis, 'fetch', async (url: string | URL | Request, options?: RequestInit) => {
    assert.match(String(url), /nativeHls=true/);
    assert.equal(new Headers(options?.headers).get('authorization'), 'Bearer synthetic.access.token');
    return Response.json(backendMaterial('طالب', 'bunny-hls', source));
  });
  const route = loadModule(resolve(root, 'app/api/video/session/route'));
  const response = await route.POST(browserRequest('session', {
    method: 'POST', body: { sessionId, purpose: 'renew', nativeHls: true },
  }));
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.data.source, source);
  assert.equal(body.data.signedSourceExpiresAtMs, expires * 1000);
  assert.ok(body.data.sessionExpiresAtMs > Date.now());
  assert.match(response.headers.get('set-cookie') ?? '', /HttpOnly/);
  assert.equal(body.data.key, undefined);
  upstream.mock.mockImplementation(async () => new Response('revoked-secret', { status: 403 }));
  const denied = await route.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'renew' } }));
  assert.equal(denied.status, 403);
  assert.equal(denied.headers.get('set-cookie'), null);
  assert.doesNotMatch(await denied.text(), /revoked-secret|b-cdn/);
});

test('native Bunny material signs both player hosts without exposing the signing key', async t => {
  const query = `token=${'a'.repeat(64)}&expires=${Math.floor(Date.now() / 1000) + 3600}`;
  t.mock.method(globalThis, 'fetch', async () => Response.json({
    ...backendMaterial('طالب', 'bunny', `123/${sessionId}`), bunnyEmbedQuery: query,
  }));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const route = loadModule(resolve(root, 'app/api/video/material/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  const response = await route.GET(browserRequest(`material?s=${sessionId}`, { cookie: started.headers.get('set-cookie')! }));
  assert.equal(response.status, 200);
  const html = await response.text();
  for (const host of ['iframe.mediadelivery.net', 'player.mediadelivery.net']) {
    assert.ok(html.includes(`https://${host}/embed/123/${sessionId}?autoplay=false&playsinline=true&disableIosPlayer=true&${query}`));
  }
  assert.doesNotMatch(html, /synthetic-route-tests-secret|synthetic.access.token/);
});

test('logout removes playback cookies only and rejects cross-origin cleanup', async () => {
  const route = loadModule(resolve(root, 'app/api/video/session/route'));
  const cookie = 'ng_video_11111111111141118111111111111111=sealed; ng_refresh=private; other=keep';
  const response = await route.DELETE(browserRequest('session', { method: 'DELETE', cookie }));
  assert.equal(response.status, 204);
  const deletedCookies = response.headers.get('Set-Cookie');
  assert.ok(deletedCookies);
  assert.match(deletedCookies, /ng_video_.*Max-Age=0/);
  assert.doesNotMatch(deletedCookies, /ng_refresh|other/);
  const denied = await route.DELETE(new Request(`${origin}/api/video/session`, { method: 'DELETE', headers: { Cookie: cookie, Referer: 'https://other.test/' } }));
  assert.equal(denied.status, 403);
});

test('closing one player clears its cookie while another open lesson remains authorized', async () => {
  const route = loadModule(resolve(root, 'app/api/video/session/route'));
  const cookie = 'ng_video_11111111111141118111111111111111=sealed; ng_video_22222222222242228222222222222222=other-session';
  const response = await route.DELETE(browserRequest(`session?s=${sessionId}`, { method: 'DELETE', cookie }));
  assert.equal(response.status, 204);
  assert.match(response.headers.get('set-cookie') ?? '', /ng_video_11111111111141118111111111111111=;/);
  assert.doesNotMatch(response.headers.get('set-cookie') ?? '', /ng_video_222/);
});

test('in-app browser without optional metadata still needs an authorized playback cookie', async t => {
  t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial()));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
  const embedRoute = loadModule(resolve(root, 'app/api/video/embed/route'));
  const anonymous = await materialRoute.GET(new Request(`${origin}/api/video/material?s=${sessionId}`));
  assert.equal(anonymous.status, 401);
  const missingJwt = await sessionRoute.POST(new Request(`${origin}/api/video/session`, {
    method: 'POST', body: JSON.stringify({ sessionId, purpose: 'start' }),
  }));
  assert.equal(missingJwt.status, 401);
  const started = await sessionRoute.POST(new Request(`${origin}/api/video/session`, {
    method: 'POST', headers: { Authorization: 'Bearer synthetic.access.token' },
    body: JSON.stringify({ sessionId, purpose: 'start' }),
  }));
  assert.equal(started.status, 200);
  const headers = { Cookie: started.headers.get('Set-Cookie')! };
  assert.equal((await embedRoute.GET(new Request(`${origin}/api/video/embed?s=${sessionId}`, { headers }))).status, 200);
  assert.equal((await materialRoute.GET(new Request(`${origin}/api/video/material?s=${sessionId}`, { headers }))).status, 200);
});

for (const enabled of [false, true]) {
  test(`YouTube native quality follows server choice (${enabled}), never the request query`, async t => {
    t.mock.method(globalThis, 'fetch', async () => Response.json({ ...backendMaterial(), youTubeQualityEnabled: enabled, youTubeQualityBottomCoverPercent: 20, youTubeQualityMobileBottomCoverPercent: 10 }));
    const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
    const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
    const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
    const response = await materialRoute.GET(browserRequest(`material?s=${sessionId}&youtubeQualityEnabled=true&youtubeQualityBottomCoverPercent=40`, { cookie: started.headers.get('Set-Cookie')! }));
    assert.equal(response.status, 200);
    const html = await response.text();
    assert.equal(html.includes('function closeNativeQualityArea()'), enabled);
    assert.equal(html.includes('height:calc(76px + 20%)'), enabled);
    assert.equal(html.includes('height:calc(76px + 10%)'), enabled);
    assert.equal(html.includes('height:calc(76px + 40%)'), false);
    assert.match(html, /window\.onYouTubeIframeAPIReady/);
  });
}

test('YouTube HLS material keeps the video ID private and uses the authorized platform player', async t => {
  t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial(
    '</script><script>window.stolen=true</script>', 'youtube-hls', 'testVideo12',
  )));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const materialRoute = loadModule(resolve(root, 'app/api/video/material/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  const response = await materialRoute.GET(browserRequest(`material?s=${sessionId}`, { cookie: started.headers.get('Set-Cookie')! }));
  assert.equal(response.status, 200);
  const html = await response.text();
  assert.match(html, /<video\b/);
  assert.match(html, /\/api\/video\/youtube-hls\?s=/);
  assert.doesNotMatch(html, /testVideo12|onYouTubeIframeAPIReady|hls\.js|<script>window\.stolen/);
});

test('YouTube HLS playlists reject copied grants, cross-origin requests and videos using the ordinary player', async t => {
  const upstream = t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial()));
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const hlsRoute = loadModule(resolve(root, 'app/api/video/youtube-hls/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  const cookie = started.headers.get('Set-Cookie')!;
  const endpoint = `youtube-hls?s=${sessionId}&playlist=master`;
  const initialRequests = upstream.mock.callCount();
  assert.equal((await hlsRoute.GET(browserRequest(endpoint))).status, 401);
  const crossOrigin = browserRequest(endpoint, { cookie });
  crossOrigin.headers.set('Sec-Fetch-Site', 'cross-site');
  assert.equal((await hlsRoute.GET(crossOrigin)).status, 403);
  assert.equal(upstream.mock.callCount(), initialRequests);
  const disabled = await hlsRoute.GET(browserRequest(endpoint, { cookie }));
  assert.equal(disabled.status, 403);
  assert.doesNotMatch(await disabled.text(), /testVideo12|googlevideo/);
});

test('authorized YouTube HLS delivers versioned playlists with Google media and rechecks permission on cached sources', async t => {
  const fixture = createYouTubeHlsFetchFixture();
  let revoked = false;
  const sharedSources = new Map<string, object>();
  let latestSourceVersion = '';
  t.mock.method(globalThis, 'fetch', async (input: string | URL | Request, options?: RequestInit) => {
    if (new URL(String(input)).hostname.endsWith('.googlevideo.com') && options?.method === undefined) {
      const range = new Headers(options?.headers).get('range');
      if (range !== 'bytes=0-15') throw new Error('Unexpected relay range');
      return new Response(new Uint8Array(16), { status: 206, headers: {
        'Content-Range': 'bytes 0-15/322', 'Content-Length': '16',
      } });
    }
    if (String(input).includes('/youtube-hls-source')) {
      if (revoked) return new Response(null, { status: 403 });
      if (options?.method === 'PUT') {
        const source = JSON.parse(String(options.body));
        sharedSources.set(source.version, source);
        latestSourceVersion = source.version;
        return new Response(null, { status: 204 });
      }
      const version = new URL(String(input)).searchParams.get('v') ?? latestSourceVersion;
      const source = sharedSources.get(version);
      return source ? Response.json(source) : new Response(null, { status: 404 });
    }
    if (String(input).includes('/v1/internal/video-sessions/')) {
      return revoked ? new Response(null, { status: 403 }) : Response.json(backendMaterial('طالب', 'youtube-hls', 'routeTest12'));
    }
    return fixture.fetch(input, options);
  });
  const sessionRoute = loadModule(resolve(root, 'app/api/video/session/route'));
  const route = loadModule(resolve(root, 'app/api/video/youtube-hls/route'));
  const started = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'start' } }));
  const cookie = started.headers.get('Set-Cookie')!;
  const information = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&info=1`, { cookie }));
  assert.equal(information.status, 200);
  const metadata = await information.json();
  assert.deepEqual(metadata.qualities.map((level: { height: number }) => level.height), [360, 720]);
  assert.doesNotMatch(JSON.stringify(metadata), /googlevideo|routeTest12/);
  const master = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=master&quality=720&v=${metadata.version}`, { cookie }));
  const masterText = await master.text();
  assert.equal(master.status, 200);
  assert.match(masterText, /playlist=audio&v=/);
  assert.match(masterText, /playlist=720&v=/);
  assert.doesNotMatch(masterText, /playlist=360/);
  const media = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=720&v=${metadata.version}`, { cookie }));
  assert.equal(media.status, 200);
  const mediaText = await media.text();
  assert.match(mediaText, /#EXT-X-BYTERANGE:/);
  assert.match(mediaText, /https:\/\/rr1\.googlevideo\.com\/videoplayback/);
  assert.doesNotMatch(mediaText, /\/api\/video\//);
  const relayMaster = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=master&relay=1&v=${metadata.version}`, { cookie }));
  assert.match(await relayMaster.text(), /playlist=720&v=[^\n]+&relay=1/);
  const relayMedia = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=720&relay=1&v=${metadata.version}`, { cookie }));
  const relayText = await relayMedia.text();
  assert.match(relayText, /media=720&part=0/);
  assert.doesNotMatch(relayText, /googlevideo/);
  const relayedInit = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&v=${metadata.version}&media=720&part=init`, { cookie }));
  assert.equal(relayedInit.status, 200);
  assert.equal((await relayedInit.arrayBuffer()).byteLength, 16);
  assert.equal((await route.GET(browserRequest(`youtube-hls?s=${sessionId}&v=${metadata.version}&media=720&part=../0`, { cookie }))).status, 400);
  // A second Next process has no local source cache and must still serve this version.
  modules.clear();
  const secondRoute = loadModule(resolve(root, 'app/api/video/youtube-hls/route'));
  const extractionRequests = fixture.requests.length;
  const secondNode = await secondRoute.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=720&v=${metadata.version}`, { cookie }));
  assert.equal(secondNode.status, 200);
  assert.equal(await secondNode.text(), mediaText);
  assert.equal(fixture.requests.length, extractionRequests);
  const stale = await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=master&v=missingVersion12`, { cookie }));
  assert.equal(stale.status, 410);
  const renewed = await sessionRoute.POST(browserRequest('session', { method: 'POST', body: { sessionId, purpose: 'renew', nativeHls: true } }));
  assert.equal(renewed.status, 200);
  assert.equal((await renewed.json()).data.source, `/api/video/youtube-hls?s=${sessionId}&playlist=master`);
  assert.match(renewed.headers.get('Set-Cookie') ?? '', /HttpOnly/);
  revoked = true;
  assert.equal((await route.GET(browserRequest(`youtube-hls?s=${sessionId}&playlist=audio&v=${metadata.version}`, { cookie }))).status, 403);
  assert.equal((await route.GET(browserRequest(`youtube-hls?s=${sessionId}&v=${metadata.version}&media=720&part=0`, { cookie }))).status, 403);
});

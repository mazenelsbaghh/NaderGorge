import assert from 'node:assert/strict';
import { createCipheriv } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';
import { after, before, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import ts from 'typescript';

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
  t.mock.method(globalThis, 'fetch', async () => Response.json(backendMaterial()));
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

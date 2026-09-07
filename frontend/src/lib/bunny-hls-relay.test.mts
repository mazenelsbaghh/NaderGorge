import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import * as relay from './bunny-hls-relay.ts';
import * as material from './video-embed-material.ts';
import * as guard from './video-embed-request-guard.ts';

const sessionId = '00000000-0000-0000-0000-000000000001';
const videoId = '4512bcd5-2688-4a53-bbd1-e41a20b8ce6c';
const source = `https://vz-example.b-cdn.net/bcdn_token=HS256-test&expires=2000000000&token_path=%2F${videoId}%2F/${videoId}/playlist.m3u8`;
const root = relay.bunnyHlsRoot(source);
const localPlaylist = `/api/video/hls?s=${sessionId}&path=720p%2Fvideo.m3u8`;

test('same-origin relay rewrites variant, segment and encryption-key URIs within the signed video scope', () => {
  assert.equal(relay.rewriteBunnyPlaylist('#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=300000\n720p/video.m3u8', new URL(source), sessionId, root),
    `#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=300000\n${localPlaylist}`);
  const mediaPlaylist = '#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI="key.bin"\n#EXTINF:6,\nsegment.ts';
  const rewritten = relay.rewriteBunnyPlaylist(mediaPlaylist, new URL('720p/video.m3u8', root), sessionId, root);
  assert.ok(rewritten.includes(`URI="/api/video/hls?s=${sessionId}&path=720p%2Fkey.bin"`));
  assert.ok(rewritten.endsWith(`/api/video/hls?s=${sessionId}&path=720p%2Fsegment.ts`));
  assert.ok(!rewritten.includes('bcdn_token'));
});

test('relay cannot fetch arbitrary hosts, other videos, encoded traversal or non-media resources', () => {
  for (const path of ['../other/playlist.m3u8', '%2e%2e/playlist.m3u8', '/playlist.m3u8', 'https://evil.example/a.ts', 'file.html', 'x.ts?url=secret']) {
    assert.throws(() => relay.bunnyHlsResource(root, path), /Invalid HLS resource/, path);
  }
  for (const uri of ['https://evil.example/a.ts', `https://vz-example.b-cdn.net/${videoId}/a.ts`, '../../other/a.ts']) {
    assert.throws(() => relay.rewriteBunnyPlaylist(`#EXTM3U\n${uri}`, new URL(source), sessionId, root));
  }
  for (const url of [source.replace('https:', 'http:'), source.replace('vz-example.b-cdn.net', 'localhost'), source.replace('vz-example', 'user@vz-example'), `${source}?token=other`]) {
    assert.throws(() => relay.bunnyHlsRoot(url));
  }
});

test('relay forwards only the byte range and preserves partial segment bytes', async (context) => {
  const bytes = new Uint8Array([7, 8, 9]);
  context.mock.method(globalThis, 'fetch', async (_url: unknown, options: RequestInit) => {
    assert.deepEqual(options.headers, { Range: 'bytes=4-6' });
    assert.equal(options.redirect, 'error');
    return new Response(bytes, { status: 206, headers: { 'content-range': 'bytes 4-6/10', 'content-length': '3' } });
  });
  const response = await relay.relayBunnyResource(new URL('720p/part.ts', root), sessionId, root,
    new Request('https://app.massar-academy.net/api/video/hls', { headers: { range: 'bytes=4-6', cookie: 'private', authorization: 'private' } }));
  assert.equal(response.status, 206);
  assert.equal(response.headers.get('content-range'), 'bytes 4-6/10');
  assert.equal(response.headers.get('cache-control'), 'private, no-store');
  assert.deepEqual(new Uint8Array(await response.arrayBuffer()), bytes);
});

test('relay preserves upstream rejection instead of converting it to successful video', async (context) => {
  context.mock.method(globalThis, 'fetch', async () => new Response('private error', { status: 403 }));
  const response = await relay.relayBunnyResource(new URL(source), sessionId, root, new Request('https://app.massar-academy.net'));
  assert.equal(response.status, 403);
  assert.equal(await response.text(), '');
});

test('oversized chunked playlists fail without buffering an unbounded body', async (context) => {
  context.mock.method(globalThis, 'fetch', async () => new Response(new ReadableStream({
    start(controller) { controller.enqueue(new Uint8Array(1024 * 1024 + 1)); controller.close(); },
  })));
  await assert.rejects(relay.relayBunnyResource(new URL(source), sessionId, root, new Request('https://app.massar-academy.net')), /exceeds limit/);
});

async function relayRoute() {
  const sourceCode = await readFile(new URL('../app/api/video/hls/route.ts', import.meta.url), 'utf8');
  const compiled = ts.transpileModule(sourceCode, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText;
  const modules: Record<string, unknown> = {
    '@/lib/bunny-hls-relay': relay, '@/lib/video-embed-material': material, '@/lib/video-embed-request-guard': guard,
  };
  const exports: { GET?: (request: Request) => Promise<Response> } = {};
  vm.runInNewContext(compiled, {
    exports, require: (name: string) => modules[name], URL, Headers, Response, AbortSignal,
    fetch: (...args: Parameters<typeof fetch>) => globalThis.fetch(...args),
    process: { env: { INTERNAL_API_URL: 'https://backend.example/api', API_CALLBACK_SECRET: 'test-only-secret' } },
  });
  return exports.GET!;
}

function relayRequest(headers: Record<string, string> = {}) {
  return new Request(`https://app.massar-academy.net/api/video/hls?s=${sessionId}`, { headers: {
    referer: `https://app.massar-academy.net/api/video/embed?s=${sessionId}`, 'sec-fetch-site': 'same-origin', ...headers,
  } });
}

test('expired or superseded session never reaches the CDN', async (context) => {
  let requests = 0;
  context.mock.method(globalThis, 'fetch', async (url: string) => {
    requests += 1;
    assert.equal(url, `https://backend.example/api/v1/internal/video-sessions/${sessionId}/embed-material`);
    return new Response(null, { status: 404 });
  });
  const get = await relayRoute();
  assert.equal((await get(relayRequest())).status, 410);
  assert.equal(requests, 1);
});

test('cross-origin and direct navigation cannot use the relay even with a session ID', async (context) => {
  context.mock.method(globalThis, 'fetch', async () => { throw new Error('No network expected'); });
  const get = await relayRoute();
  for (const request of [relayRequest({ referer: 'https://evil.example', 'sec-fetch-site': 'cross-site' }),
    new Request(`https://app.massar-academy.net/api/video/hls?s=${sessionId}`)]) {
    assert.equal((await get(request)).status, 403);
  }
});

test('active encrypted HLS session returns rewritten playlist without exposing internal credentials', async (context) => {
  const key = crypto.randomBytes(32), iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);
  const encrypted = Buffer.concat([cipher.update(JSON.stringify({ Provider: 'bunny-hls', VideoId: source })), cipher.final()]);
  const token = Buffer.concat([iv, encrypted, cipher.getAuthTag()]).toString('base64');
  context.mock.method(globalThis, 'fetch', async (url: string | URL, options: RequestInit) => {
    if (String(url).startsWith('https://backend.example')) return Response.json({ token, key: key.toString('base64') });
    assert.equal(String(url), source);
    assert.deepEqual(options.headers, {});
    return new Response('#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=300000\n720p/video.m3u8');
  });
  const get = await relayRoute();
  const response = await get(relayRequest());
  assert.equal(response.status, 200);
  assert.ok((await response.text()).endsWith(localPlaylist));
});

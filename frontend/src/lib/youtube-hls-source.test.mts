import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getYouTubeHlsPlaylist, resolveYouTubeHlsSource, YouTubeHlsError,
  type YouTubeHlsSource } from './youtube-hls-source.ts';
import { getYouTubeHlsMediaRange, parseYouTubeSidx, youTubeMediaUrl, type YouTubeHlsFormat } from './youtube-hls-playlist.ts';
import { relayYouTubeHlsMedia } from './youtube-hls-media.ts';
import { createYouTubeHlsFetchFixture, youtubeIndexFixture } from './youtube-hls-test-fixtures.mts';

let sequence = 0;
const videoId = () => `yt${String(++sequence).padStart(9, '0')}`;
const sessionId = '11111111-1111-4111-8111-111111111111';
const errorCode = (code: string) => (error: unknown) => error instanceof YouTubeHlsError && error.code === code;

function indexedFormat(): YouTubeHlsFormat {
  return { url: 'https://rr1.googlevideo.com/videoplayback?expire=2000000000', codec: 'avc1.4d401f',
    bandwidth: 1000000, width: 1280, height: 720, fps: 30, contentLength: 322,
    expiresAt: 2000000000000, initRange: { start: 0, end: 15 }, indexRange: { start: 16, end: 71 } };
}

test('concurrent viewers share extraction and receive direct-media playlists pinned to their source generation', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', fixture.fetch);
  const id = videoId();
  const [first, second] = await Promise.all([resolveYouTubeHlsSource(id), resolveYouTubeHlsSource(id)]);
  assert.equal(first, second);
  assert.deepEqual(first.qualities.map(quality => quality.height), [360, 720]);
  assert.equal(first.durationSeconds, 10);
  assert.equal(fixture.requests.length, 8);
  assert.equal(await resolveYouTubeHlsSource(id, first.version), first);
  assert.equal(fixture.requests.length, 8);
  const master = getYouTubeHlsPlaylist(first, { sessionId, playlist: 'master', quality: '720' });
  assert.match(master, /AUDIO="audio"/);
  assert.ok(master.includes(`s=${sessionId}&playlist=audio&v=${first.version}`));
  assert.ok(master.includes(`s=${sessionId}&playlist=720&v=${first.version}`));
  assert.ok(!master.includes('playlist=360'));
  const media = getYouTubeHlsPlaylist(first, { sessionId, playlist: '720' });
  assert.match(media, /BYTERANGE="16@0"/);
  assert.match(media, /#EXT-X-BYTERANGE:100@72/);
  assert.match(media, /#EXT-X-BYTERANGE:150@172/);
  assert.ok(media.endsWith('#EXT-X-ENDLIST\n'));
  assert.ok(media.split('\n').filter(line => line && !line.startsWith('#')).every(line => line.startsWith('https://rr1.googlevideo.com/')));
  for (const request of fixture.requests) {
    const headers = new Headers(request.options.headers);
    assert.equal(request.options.credentials, 'omit');
    assert.equal(headers.has('cookie'), false);
    assert.equal(headers.has('authorization'), false);
    if (request.url.hostname.endsWith('.googlevideo.com')) assert.match(headers.get('range')!, /^bytes=(0-15|16-71)$/);
  }
});

test('HLS.js playlists use session-scoped media URLs and relay only indexed byte ranges', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const fetchMock = context.mock.method(globalThis, 'fetch', fixture.fetch);
  const source = await resolveYouTubeHlsSource(videoId());
  const master = getYouTubeHlsPlaylist(source, { sessionId, playlist: 'master', relay: true });
  const media = getYouTubeHlsPlaylist(source, { sessionId, playlist: '360', relay: true });
  assert.match(master, /playlist=360&v=[^\n]+&relay=1/);
  assert.match(media, /media=360&part=init/);
  assert.match(media, /media=360&part=0/);
  assert.doesNotMatch(media, /googlevideo|BYTERANGE/);
  assert.deepEqual(getYouTubeHlsMediaRange(source, '360', '0').range, { start: 72, end: 171 });
  for (const [track, part] of [['360', '../0'], ['9999', '0'], ['audio', '99999']]) {
    assert.throws(() => getYouTubeHlsMediaRange(source, track, part), errorCode('invalid-playlist'));
  }

  const requests: { url: URL; headers: Headers; credentials: RequestCredentials | undefined }[] = [];
  fetchMock.mock.mockImplementation(async (input: string | URL | Request, options: RequestInit = {}) => {
    const url = new URL(input instanceof Request ? input.url : String(input));
    const headers = new Headers(options.headers);
    requests.push({ url, headers, credentials: options.credentials });
    const range = headers.get('range');
    if (range !== 'bytes=72-171') throw new Error('Unexpected media range');
    return new Response(new Uint8Array(100).fill(7), { status: 206, headers: {
      'Content-Range': 'bytes 72-171/322', 'Content-Length': '100',
    } });
  });
  const response = await relayYouTubeHlsMedia(source, '360', '0', new AbortController().signal);
  assert.equal(response.status, 200);
  assert.equal(response.headers.get('content-length'), '100');
  assert.equal((await response.arrayBuffer()).byteLength, 100);
  assert.equal(requests.length, 1);
  assert.equal(requests[0].url.hostname, 'rr1.googlevideo.com');
  assert.equal(requests[0].credentials, 'omit');
  assert.equal(requests[0].headers.has('authorization'), false);
  assert.equal(requests[0].headers.has('cookie'), false);
});

test('media relay rejects an upstream response that ignores the indexed range', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const fetchMock = context.mock.method(globalThis, 'fetch', fixture.fetch);
  const source = await resolveYouTubeHlsSource(videoId());
  let cancelled = false;
  fetchMock.mock.mockImplementation(async () => new Response(new ReadableStream({
    cancel() { cancelled = true; },
  }), { status: 200 }));
  await assert.rejects(relayYouTubeHlsMedia(source, '360', '0', new AbortController().signal), errorCode('upstream'));
  assert.equal(cancelled, true);
});

test('media relay never follows a redirect outside the approved video host', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const fetchMock = context.mock.method(globalThis, 'fetch', fixture.fetch);
  const source = await resolveYouTubeHlsSource(videoId());
  const hosts: string[] = [];
  fetchMock.mock.mockImplementation(async (input: string | URL | Request) => {
    hosts.push(new URL(input instanceof Request ? input.url : String(input)).hostname);
    return new Response(null, { status: 302, headers: { Location: 'http://127.0.0.1/private' } });
  });
  await assert.rejects(relayYouTubeHlsMedia(source, 'audio', '0', new AbortController().signal), errorCode('upstream'));
  assert.deepEqual(hosts, ['rr1.googlevideo.com']);
});

test('version-one SIDX offsets include preceding boxes and first_offset without downloading segments', () => {
  const index = Buffer.alloc(64);
  index.writeUInt32BE(64, 0); index.write('sidx', 4); index[8] = 1;
  index.writeUInt32BE(1000, 16); index.writeUInt32BE(7, 32); index.writeUInt16BE(2, 38);
  for (const [offset, length] of [[40, 100], [52, 150]]) {
    index.writeUInt32BE(length, offset); index.writeUInt32BE(5000, offset + 4); index.writeUInt32BE(0x90000000, offset + 8);
  }
  const prefix = Buffer.alloc(8); prefix.writeUInt32BE(8, 0); prefix.write('free', 4);
  const format = { ...indexedFormat(), contentLength: 345, indexRange: { start: 16, end: 87 } };
  const track = parseYouTubeSidx(Buffer.concat([prefix, index]), format);
  assert.deepEqual(track.segments.map(segment => segment.offset), [95, 195]);
  assert.equal(track.durationSeconds, 10);
});

test('malformed or unsupported indexes never produce out-of-resource or undecodable byte ranges', async context => {
  const cases = [
    ['truncated', (index: Buffer) => index.subarray(0, 40)],
    ['zero timescale', (index: Buffer) => { index.writeUInt32BE(0, 16); return index; }],
    ['nested reference', (index: Buffer) => { index.writeUInt32BE(0x80000064, 32); return index; }],
    ['non-independent fragment', (index: Buffer) => { index.writeUInt32BE(0, 40); return index; }],
    ['zero duration', (index: Buffer) => { index.writeUInt32BE(0, 36); return index; }],
    ['past resource end', (index: Buffer) => { index.writeUInt32BE(1000, 44); return index; }],
  ] as const;
  for (const [scenario, mutate] of cases) await context.test(scenario, () => {
    assert.throws(() => parseYouTubeSidx(mutate(youtubeIndexFixture().index), indexedFormat()), YouTubeHlsError);
  });
});

test('unsafe CDN URLs cannot become server requests or playlist directives', async context => {
  for (const url of ['http://rr1.googlevideo.com/videoplayback', 'https://googlevideo.com.evil.test/videoplayback',
    'https://127.0.0.1/videoplayback', 'https://user@rr1.googlevideo.com/videoplayback',
    'https://rr1.googlevideo.com:8443/videoplayback', 'https://rr1.googlevideo.com/other',
    'https://rr1.googlevideo.com/videoplayback?x="\n#EXT-X-KEY:URI="https://evil.test']) {
    await context.test(url.split('?')[0], () => assert.throws(() => youTubeMediaUrl(url), errorCode('upstream')));
  }
});

test('a CDN redirect outside Google is rejected before following it', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const hosts: string[] = [];
  context.mock.method(globalThis, 'fetch', async (input: string | URL, options: RequestInit) => {
    const url = new URL(input); hosts.push(url.hostname);
    if (url.hostname.endsWith('.googlevideo.com')) return new Response(null, { status: 302, headers: { Location: 'http://127.0.0.1/private' } });
    return fixture.fetch(input, options);
  });
  await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('upstream'));
  assert.ok(!hosts.includes('127.0.0.1'));
});

test('ignored range requests are cancelled rather than downloading the video', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  let cancelled = 0;
  context.mock.method(globalThis, 'fetch', async (input: string | URL, options: RequestInit) => {
    if (new URL(input).hostname.endsWith('.googlevideo.com')) {
      return new Response(new ReadableStream({ cancel() { cancelled += 1; } }), { status: 200 });
    }
    return fixture.fetch(input, options);
  });
  await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('upstream'));
  assert.ok(cancelled > 0);
});

test('oversized chunked metadata is cancelled even without a Content-Length header', async context => {
  let cancelled = false;
  context.mock.method(globalThis, 'fetch', async () => new Response(new ReadableStream({
    start(controller) { controller.enqueue(new Uint8Array(2 * 1024 * 1024 + 1)); },
    cancel() { cancelled = true; },
  })));
  await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('upstream'));
  assert.equal(cancelled, true);
});

test('the deadline also cancels a stalled response body after headers arrive', async context => {
  const deadline = new AbortController();
  let cancelled = false;
  context.mock.method(AbortSignal, 'timeout', () => deadline.signal);
  context.mock.method(globalThis, 'fetch', async () => {
    setImmediate(() => deadline.abort(new DOMException('Deadline reached', 'TimeoutError')));
    return new Response(new ReadableStream({ cancel() { cancelled = true; } }));
  });
  await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('upstream'));
  assert.equal(cancelled, true);
});

test('unavailable videos fail with a redacted error before media metadata is requested', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', async (input: string | URL, options: RequestInit) => {
    if (new URL(input).pathname.endsWith('/player')) return Response.json({ playabilityStatus: {
      status: 'LOGIN_REQUIRED', reason: 'Sensitive title and signed token must never enter errors',
    } });
    return fixture.fetch(input, options);
  });
  await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('unavailable'));
  assert.ok(fixture.requests.every(request => !request.url.hostname.endsWith('.googlevideo.com')));
});

test('renewal replaces the current generation while existing child playlists remain valid until their own expiry', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', fixture.fetch);
  let now = Date.now(); context.mock.method(Date, 'now', () => now);
  const id = videoId(); const first = await resolveYouTubeHlsSource(id);
  now = first.expiresAt - 90_000;
  const renewed = await resolveYouTubeHlsSource(id);
  assert.notEqual(renewed.version, first.version);
  assert.equal(await resolveYouTubeHlsSource(id, first.version), first);
  assert.equal(await resolveYouTubeHlsSource(id), renewed);
  await assert.rejects(resolveYouTubeHlsSource(videoId(), first.version), errorCode('source-expired'));
  now = first.expiresAt + 1;
  await assert.rejects(resolveYouTubeHlsSource(id, first.version), errorCode('source-expired'));
  assert.equal(await resolveYouTubeHlsSource(id), renewed);
});

test('bounded cache eviction requires an old master to renew instead of silently changing its source', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', fixture.fetch);
  const first = await resolveYouTubeHlsSource(videoId());
  for (let index = 0; index < 32; index += 1) await resolveYouTubeHlsSource(videoId());
  await assert.rejects(resolveYouTubeHlsSource(first.videoId, first.version), errorCode('source-expired'));
});

test('too many simultaneous extractions fail without opening more upstream requests', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  context.mock.method(globalThis, 'fetch', async (input: string | URL, options: RequestInit) => {
    if (new URL(input).pathname.endsWith('/visitor_id')) await gate;
    return fixture.fetch(input, options);
  });
  const active = Array.from({ length: 4 }, () => resolveYouTubeHlsSource(videoId()));
  try { await assert.rejects(resolveYouTubeHlsSource(videoId()), errorCode('busy')); } finally { release(); }
  await Promise.all(active);
});

test('invalid IDs, unknown versions and injected playlist inputs cannot reach upstream services', async context => {
  context.mock.method(globalThis, 'fetch', async () => { throw new Error('No upstream expected'); });
  for (const id of ['', 'https://youtu.be/example', 'a'.repeat(12), '../abcdefgh']) {
    await assert.rejects(resolveYouTubeHlsSource(id), errorCode('invalid-video'));
  }
  await assert.rejects(resolveYouTubeHlsSource(videoId(), 'A'.repeat(16)), errorCode('source-expired'));
  const track = parseYouTubeSidx(youtubeIndexFixture().index, indexedFormat());
  const source = { videoId: videoId(), version: 'A'.repeat(16), expiresAt: Date.now() + 60000, durationSeconds: 10,
    qualities: [{ height: 720, width: 1280, fps: 30, bandwidth: 1000000 }], videos: [track], audio: track };
  for (const request of [{ sessionId: 'injected\n', playlist: 'master' }, { sessionId, playlist: '../secret' },
    { sessionId, playlist: 'master', quality: '720\n#EXT-X-KEY' }]) {
    assert.throws(() => getYouTubeHlsPlaylist(source, request), errorCode('invalid-playlist'));
  }
});

test('a child routed to another frontend restores its exact shared generation without contacting Google', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const fetchMock = context.mock.method(globalThis, 'fetch', fixture.fetch);
  const extracted = await resolveYouTubeHlsSource(videoId());
  const stored: YouTubeHlsSource = JSON.parse(JSON.stringify(extracted));
  stored.videoId = videoId(); stored.version = 'shared0000000001';
  fetchMock.mock.mockImplementation(async () => { throw new Error('Shared restore must not extract'); });
  const restored = await resolveYouTubeHlsSource(stored.videoId, stored.version, {
    read: async () => stored, write: async () => { throw new Error('Existing shared source needs no write'); },
  });
  assert.equal(restored.version, stored.version);
  assert.deepEqual(restored.qualities, extracted.qualities);
  assert.match(getYouTubeHlsPlaylist(restored, { sessionId, playlist: '720' }), /#EXT-X-BYTERANGE:100@72/);
});

test('new source generations become visible only after the shared write succeeds', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', fixture.fetch);
  let release!: () => void, started!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const writing = new Promise<void>(resolve => { started = resolve; });
  let stored: YouTubeHlsSource | undefined;
  const adapter = { read: async () => undefined, write: async (source: YouTubeHlsSource) => {
    stored = source; started(); await gate;
  } };
  const id = videoId(); const pending = resolveYouTubeHlsSource(id, undefined, adapter);
  await writing;
  try { await assert.rejects(resolveYouTubeHlsSource(id, stored!.version), errorCode('source-expired')); } finally { release(); }
  const published = await pending;
  assert.equal(published.version, stored!.version);
  assert.equal(await resolveYouTubeHlsSource(id, published.version), published);
});

test('corrupted shared metadata cannot inject external URLs or invalid media ranges', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  const fetchMock = context.mock.method(globalThis, 'fetch', fixture.fetch);
  const extracted = await resolveYouTubeHlsSource(videoId());
  fetchMock.mock.mockImplementation(async () => { throw new Error('No upstream expected'); });
  const cases = [
    ['host', (source: YouTubeHlsSource) => { source.videos[0].format.url = 'https://evil.example/videoplayback'; }],
    ['range', (source: YouTubeHlsSource) => { source.videos[0].segments[0].length = 999999; }],
    ['expiry', (source: YouTubeHlsSource) => { source.expiresAt += 86400000; }],
  ] as const;
  for (const [scenario, corrupt] of cases) await context.test(scenario, async () => {
    const stored: YouTubeHlsSource = JSON.parse(JSON.stringify(extracted));
    stored.videoId = videoId(); stored.version = 'shared0000000002'; corrupt(stored);
    await assert.rejects(resolveYouTubeHlsSource(stored.videoId, stored.version, {
      read: async () => stored, write: async () => {},
    }), errorCode('upstream'));
  });
});

test('failed shared persistence does not publish a local-only version or poison the next extraction', async context => {
  const fixture = createYouTubeHlsFetchFixture();
  context.mock.method(globalThis, 'fetch', fixture.fetch);
  const id = videoId();
  await assert.rejects(resolveYouTubeHlsSource(id, undefined, {
    read: async () => undefined, write: async () => { throw new YouTubeHlsError('upstream'); },
  }), errorCode('upstream'));
  const recovered = await resolveYouTubeHlsSource(id, undefined, { read: async () => undefined, write: async () => {} });
  assert.equal(recovered.videoId, id);
  assert.equal(fixture.requests.length, 16);
});

test('one viewer cancelling a cache operation cannot cancel another viewer sharing the Google extraction', async context => {
  for (const cancelledStage of ['read', 'write'] as const) await context.test(cancelledStage, async scenario => {
    const fixture = createYouTubeHlsFetchFixture();
    scenario.mock.method(globalThis, 'fetch', fixture.fetch);
    let cancelledReadCount = 0, activeReadCount = 0;
    let cancelledVersion: string | undefined, persistedVersion: string | undefined;
    const cancelledAdapter = {
      read: async () => {
        cancelledReadCount += 1;
        if (cancelledStage === 'read') throw new DOMException('Viewer disconnected', 'AbortError');
        return undefined;
      },
      write: async (source: YouTubeHlsSource) => {
        cancelledVersion = source.version;
        throw new DOMException('Viewer disconnected', 'AbortError');
      },
    };
    const activeAdapter = {
      read: async () => { activeReadCount += 1; return undefined; },
      write: async (source: YouTubeHlsSource) => { persistedVersion = source.version; },
    };
    const id = videoId();
    const [cancelled, active] = await Promise.allSettled([
      resolveYouTubeHlsSource(id, undefined, cancelledAdapter),
      resolveYouTubeHlsSource(id, undefined, activeAdapter),
    ]);
    assert.equal(cancelled.status, 'rejected');
    if (cancelled.status === 'rejected') assert.ok(errorCode('upstream')(cancelled.reason));
    assert.equal(active.status, 'fulfilled');
    if (active.status !== 'fulfilled') return;
    assert.equal(cancelledReadCount, 1);
    assert.equal(activeReadCount, 1);
    assert.equal(persistedVersion, active.value.version);
    if (cancelledStage === 'write') assert.equal(cancelledVersion, active.value.version);
    assert.equal(fixture.requests.length, 8);
    assert.equal(await resolveYouTubeHlsSource(id, active.value.version), active.value);
  });
});

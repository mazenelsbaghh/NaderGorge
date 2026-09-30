import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { generateYouTubeHlsEmbedHtml } from './youtube-hls-embed.ts';

const origin = 'https://app.massar-academy.net';
const source = '/api/video/youtube-hls?s=11111111-1111-4111-8111-111111111111&playlist=master';
const epoch = 1_700_000_000_000;
const flush = () => new Promise<void>(resolve => setImmediate(resolve));
type Message = { type: string; data: Record<string, unknown> };
type Listener = (event?: unknown) => void;

async function runPlayer(options: { native?: boolean; status?: number; hlsjs?: boolean } = {}) {
  const messages: Message[] = [];
  const requests: string[] = [];
  const videoListeners = new Map<string, Listener[]>();
  const windowListeners = new Map<string, Listener>();
  const documentListeners = new Map<string, Listener>();
  const timers: { callback: () => void; active: boolean; at: number }[] = [];
  let clock = 0;
  let metadata = {
    qualities: [{ height: 360, width: 640, fps: 30, bandwidth: 350_000 }, { height: 720, width: 1280, fps: 30, bandwidth: 900_000 }],
    durationSeconds: 600, version: 'version-one', serverNowMs: epoch, expiresAt: epoch + 21_600_000,
  };
  function emit(name: string) { for (const callback of videoListeners.get(name) ?? []) callback(); }
  const video = {
    src: '', currentTime: 0, duration: Number.NaN, paused: true, ended: false, volume: 1, muted: false, playbackRate: 1,
    addEventListener(name: string, callback: Listener) {
      videoListeners.set(name, [...videoListeners.get(name) ?? [], callback]);
    },
    canPlayType() { return options.native === false ? '' : 'probably'; },
    removeAttribute(name: string) { if (name === 'src') this.src = ''; },
    load() { this.duration = Number.NaN; this.currentTime = 0; this.paused = true; },
    pause() { this.paused = true; emit('pause'); },
    play() { this.paused = false; emit('play'); emit('playing'); return Promise.resolve(); },
  };
  const hlsInstances: Array<{
    loadedSource: string; currentLevel: number; nextLevel: number; levels: Array<{ height: number }>;
    emit: (event: string, data?: unknown) => void; destroyed: boolean;
  }> = [];
  class MockHls {
    static Events = { MANIFEST_PARSED: 'manifest', FRAG_BUFFERED: 'fragment', LEVEL_SWITCHED: 'level', ERROR: 'error' };
    static ErrorTypes = { MEDIA_ERROR: 'media' };
    static isSupported() { return true; }
    loadedSource = '';
    currentLevel = -1;
    nextLevel = -1;
    levels = [{ height: 360 }, { height: 720 }];
    destroyed = false;
    private listeners = new Map<string, Array<(_event: string, data?: unknown) => void>>();
    constructor() { hlsInstances.push(this); }
    attachMedia() {}
    loadSource(sourceUrl: string) { this.loadedSource = sourceUrl; }
    on(event: string, listener: (_event: string, data?: unknown) => void) {
      this.listeners.set(event, [...this.listeners.get(event) ?? [], listener]);
    }
    emit(event: string, data?: unknown) { for (const listener of this.listeners.get(event) ?? []) listener(event, data); }
    recoverMediaError() {}
    destroy() { this.destroyed = true; }
  }
  const parentWindow = { postMessage(message: Message) { messages.push(message); } };
  const windowLike = {
    Hls: options.hlsjs ? MockHls : undefined,
    addEventListener(name: string, listener: Listener) { windowListeners.set(name, listener); },
    setTimeout(callback: () => void) { callback(); },
    location: { origin, replace() {} },
    parent: parentWindow,
  };
  const html = generateYouTubeHlsEmbedHtml(source, 'Student', '');
  vm.runInNewContext(html.slice(html.indexOf('(function(){'), html.lastIndexOf('</script>')), {
    URL, AbortController, parent: parentWindow, window: windowLike, location: windowLike.location,
    performance: { now: () => clock },
    document: {
      hidden: false,
      getElementById() { return video; },
      addEventListener(name: string, listener: Listener) { documentListeners.set(name, listener); },
    },
    fetch(url: string) {
      requests.push(url);
      return Promise.resolve({ ok: !options.status || options.status === 200, status: options.status ?? 200,
        json: () => Promise.resolve(metadata) });
    },
    setTimeout(callback: () => void, delay: number) {
      const timer = { callback, at: clock + delay, active: true }; timers.push(timer); return timer;
    },
    clearTimeout(timer: { active: boolean }) { timer.active = false; },
  });
  await flush();
  function send(command: Record<string, unknown>, eventOrigin = origin, eventSource: unknown = parentWindow) {
    windowListeners.get('message')?.({ origin: eventOrigin, source: eventSource, data: command });
  }
  return {
    video, messages, requests, emit, send, hlsInstances,
    loaded() { video.duration = 600; emit('loadedmetadata'); },
    renew(version = metadata.version) {
      metadata = { ...metadata, version, serverNowMs: epoch + clock, expiresAt: epoch + clock + 21_600_000 };
      send({ type: 'renewSource', source, serverNowMs: epoch + clock, sessionExpiresAtMs: epoch + clock + 3_600_000 });
      return flush();
    },
    advance(milliseconds: number) {
      const target = clock + milliseconds;
      for (;;) {
        const next = timers.filter(timer => timer.active && timer.at <= target).sort((a, b) => a.at - b.at)[0];
        if (!next) break;
        clock = next.at; next.active = false; next.callback();
      }
      clock = target;
    },
    resumeAfter(milliseconds: number) { clock += milliseconds; documentListeners.get('visibilitychange')?.(); },
    keydown(key: string) { windowListeners.get('keydown')?.({ key, preventDefault() {}, stopImmediatePropagation() {} }); },
  };
}

test('native HLS loads protected playlists and exposes only platform quality messages', async () => {
  const player = await runPlayer();
  assert.equal(player.requests.length, 1);
  assert.equal(new URL(player.requests[0]).origin, origin);
  assert.equal(new URL(player.requests[0]).searchParams.get('info'), '1');
  assert.equal(new URL(player.video.src).searchParams.get('v'), 'version-one');
  assert.equal(player.messages.some(message => message.type === 'ready'), false);
  player.loaded();
  assert.equal(player.messages.find(message => message.type === 'ready')?.data.provider, 'youtube-hls');
  const qualities = player.messages.find(message => message.type === 'qualityLevels')!.data.levels;
  assert.deepEqual(JSON.parse(JSON.stringify(qualities)), [
    { id: '360', label: '360p', height: 360, bitrate: 350_000 },
    { id: '720', label: '720p', height: 720, bitrate: 900_000 },
  ]);
  player.send({ type: 'play' });
  player.video.currentTime = 23;
  player.emit('timeupdate');
  assert.equal(player.messages.at(-1)?.data.currentTime, 23);
  assert.equal(player.messages.at(-1)?.data.isPlaying, true);
});

test('HLS.js plays on browsers without native HLS and switches quality in place', async () => {
  const player = await runPlayer({ native: false, hlsjs: true });
  const hls = player.hlsInstances[0];
  assert.ok(hls);
  assert.equal(new URL(hls.loadedSource).searchParams.get('v'), 'version-one');
  player.loaded();
  assert.equal(player.messages.find(message => message.type === 'ready')?.data.provider, 'youtube-hls');
  player.send({ type: 'play' });
  player.video.currentTime = 90;
  player.emit('timeupdate');
  player.send({ type: 'setQuality', quality: '720' });
  assert.equal(hls.currentLevel, 1);
  assert.equal(hls.nextLevel, 1);
  assert.equal(player.video.currentTime, 90);
  assert.equal(player.messages.filter(message => message.type === 'qualityLevels').at(-1)?.data.currentQuality, '720');
  player.advance(1_500_000);
  await player.renew('version-two');
  assert.equal(new URL(hls.loadedSource).searchParams.get('v'), 'version-two');
  assert.equal(new URL(hls.loadedSource).searchParams.has('quality'), false);
  hls.emit('manifest');
  assert.equal(hls.currentLevel, 1);
  hls.emit('fragment');
  assert.equal(player.video.currentTime, 90);
  player.send({ type: 'setQuality', quality: 'auto' });
  assert.equal(hls.currentLevel, -1);
  assert.equal(player.video.currentTime, 90);
});

test('quality changes preserve position, playback settings, and commands received during loading', async () => {
  const player = await runPlayer(); player.loaded();
  player.send({ type: 'play' });
  player.video.currentTime = 187;
  player.send({ type: 'setPlaybackRate', rate: 1.5 });
  player.send({ type: 'setVolume', volume: 40 });
  player.send({ type: 'mute' });
  player.send({ type: 'setQuality', quality: '720' });
  assert.equal(new URL(player.video.src).searchParams.get('quality'), '720');
  player.loaded();
  assert.equal(player.video.currentTime, 187);
  assert.equal(player.video.paused, false);
  assert.equal(player.video.playbackRate, 1.5);
  assert.equal(player.video.volume, .4);
  assert.equal(player.video.muted, true);
  player.send({ type: 'setQuality', quality: '360' });
  player.send({ type: 'setQuality', quality: '720' });
  player.send({ type: 'setPlaybackRate', rate: 2 });
  player.send({ type: 'seekTo', time: 245 });
  player.send({ type: 'pause' });
  player.loaded();
  assert.equal(player.video.currentTime, 245);
  assert.equal(player.video.paused, true);
  assert.equal(player.video.playbackRate, 2);
  assert.equal(new URL(player.video.src).searchParams.get('quality'), '720');
  assert.equal(player.messages.filter(message => message.type === 'stateChange').at(-1)?.data.state, 2);
  player.send({ type: 'setQuality', quality: 'auto' }); player.loaded();
  assert.equal(new URL(player.video.src).searchParams.has('quality'), false);
  assert.equal(player.video.currentTime, 245);
  assert.equal(player.video.paused, true);
  player.send({ type: 'setVolume', volume: 65 }); player.emit('volumechange');
  assert.equal(player.messages.at(-1)?.data.volume, 65);
  assert.equal(player.messages.at(-1)?.data.isPlaying, false);
});

test('foreign windows, invalid qualities, and an unrelated renewal cannot replace a stream', async () => {
  const player = await runPlayer(); player.loaded();
  const original = player.video.src;
  player.send({ type: 'setQuality', quality: '720' }, 'https://attacker.test');
  player.send({ type: 'setQuality', quality: '720' }, origin, {});
  player.send({ type: 'setQuality', quality: '9999' });
  player.send({ type: 'renewSource', source: 'https://attacker.test/media', serverNowMs: epoch });
  assert.equal(player.video.src, original);
  player.advance(1_500_000);
  player.send({ type: 'renewSource', source: '/api/video/youtube-hls?s=22222222-2222-4222-8222-222222222222&playlist=master', serverNowMs: epoch, sessionExpiresAtMs: epoch + 1_000_000 });
  assert.equal(player.video.src, '');
  assert.equal(player.messages.at(-1)?.type, 'error');
});

test('the browser grant renews after 25 minutes and new sources keep the chosen quality and position', async () => {
  const player = await runPlayer(); player.loaded();
  player.send({ type: 'setQuality', quality: '720' }); player.loaded();
  player.send({ type: 'play' }); player.video.currentTime = 280;
  player.advance(1_499_999);
  assert.equal(player.messages.some(message => message.type === 'renewSourceRequired'), false);
  player.advance(1);
  assert.equal(player.messages.at(-1)?.type, 'renewSourceRequired');
  await player.renew('version-two');
  assert.equal(new URL(player.video.src).searchParams.get('v'), 'version-two');
  assert.equal(new URL(player.video.src).searchParams.get('quality'), '720');
  player.loaded();
  assert.equal(player.video.currentTime, 280);
  assert.equal(player.video.paused, false);
  assert.equal(player.requests.length, 2);
});

test('native failures get one authorization refresh, then stop without a media proxy or iframe fallback', async () => {
  const player = await runPlayer(); player.loaded();
  player.send({ type: 'play' }); player.video.currentTime = 48;
  player.emit('error');
  assert.equal(player.messages.at(-1)?.type, 'renewSourceRequired');
  await player.renew(); player.loaded();
  assert.equal(player.video.currentTime, 48);
  player.emit('error');
  assert.equal(player.messages.at(-1)?.type, 'error');
  assert.equal(player.messages.at(-1)?.data.phase, 'native_media');
  assert.equal(player.video.src, '');
  assert.equal(player.video.paused, true);
  assert.ok(player.requests.every(url => new URL(url).searchParams.get('info') === '1'));
  player.advance(2_000_000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
});

test('returning to an inactive tab renews the grant before a quality change without duplicating renewal', async () => {
  const player = await runPlayer(); player.loaded();
  player.video.currentTime = 94;
  player.resumeAfter(2_000_000);
  player.send({ type: 'setQuality', quality: '720' });
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
  await player.renew(); player.loaded();
  assert.equal(new URL(player.video.src).searchParams.get('quality'), '720');
  assert.equal(player.video.currentTime, 94);
  assert.equal(player.video.paused, true);
});

test('a native stream that never loads cannot leave an indefinite spinner', async () => {
  const player = await runPlayer();
  player.advance(45_000);
  assert.equal(player.messages.at(-1)?.type, 'renewSourceRequired');
  player.advance(25_000);
  assert.equal(player.messages.at(-1)?.type, 'error');
  assert.equal(player.video.src, '');
});

test('unsupported browsers fail before any metadata request', async () => {
  const player = await runPlayer({ native: false });
  assert.equal(player.requests.length, 0);
  assert.equal(player.video.src, '');
  assert.equal(player.messages.at(-1)?.data.phase, 'unsupported_browser');
});

for (const status of [401, 403, 409, 410, 503]) {
  test(`denied or unavailable metadata (${status}) never starts media`, async () => {
    const player = await runPlayer({ status });
    assert.equal(player.video.src, '');
    assert.equal(player.messages.at(-1)?.data.code, status);
    assert.equal(player.messages.some(message => message.type === 'ready'), false);
  });
}

test('inspection suspension unloads native media and prevents later playback commands', async () => {
  const player = await runPlayer(); player.loaded(); player.send({ type: 'play' });
  player.keydown('F12');
  assert.equal(player.messages.at(-1)?.type, 'securityViolation');
  assert.equal(player.video.src, '');
  assert.equal(player.video.paused, true);
  player.send({ type: 'play' });
  assert.equal(player.video.paused, true);
});

test('student text is escaped and arbitrary playlist origins are rejected', () => {
  const html = generateYouTubeHlsEmbedHtml(source, '</div><script>alert(1)</script>', '<img src=x onerror=alert(2)>');
  assert.doesNotMatch(html, /<script>alert|<img src=x/);
  assert.match(html, /&lt;script&gt;alert/);
  assert.throws(() => generateYouTubeHlsEmbedHtml('https://video.example/master.m3u8', '', ''), /protected playback session/);
});

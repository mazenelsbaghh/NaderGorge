import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';
import { createRequire } from 'node:module';
import { isExpiredHlsSourceError } from './video-playback-recovery.ts';

import { generateVcdnEmbedHtml } from './vcdn-embed.ts';
import { generateBunnyHlsEmbedHtml, generateDirectHlsEmbedHtml } from './bunny-hls-embed.ts';

type PlayerMessage = {
  source?: string;
  type?: string;
  data?: { code?: number; message?: string; phase?: string; provider?: string; wasPlaying?: boolean; signedSourceExpiresAtMs?: number; currentTime?: number; duration?: number; native?: boolean; sourceRenewal?: string; levels?: Array<{ id: string; label: string }>; currentQuality?: string };
};

type HlsRuntime = 'hlsjs' | 'native-apple';
const vcdnTransport = createRequire(import.meta.url)('../../public/vendor/vcdn/vcdn-hls-transport.js');

async function runHlsPlayer(runtime: HlsRuntime = 'hlsjs', nativeManifestStatus = 200, relaySource = '', sourceOptions: string | { platformSource: string } = 'https://vz-example.b-cdn.net/signed/video/playlist.m3u8') {
  const platformSession = typeof sourceOptions === 'object';
  const signedSource = platformSession ? sourceOptions.platformSource : sourceOptions;
  let html: string;
  if (platformSession || signedSource.startsWith('https://stream.vcdn.me/')) {
    html = relaySource
      ? generateDirectHlsEmbedHtml({ playlistUrl: new URL(signedSource), provider: 'vcdn', studentName: 'Test student', studentPhone: '', relaySource, sourceExpiresAtMs: 600_000, serverNowMs: 0 })
      : generateVcdnEmbedHtml('11111111-1111-4111-8111-111111111111', 'Test student', '', {source: signedSource, expiresAt: platformSession ? 180_000 : 600_000, serverNowMs: 0, ...(platformSession ? { protection: 'platform-session' as const } : {})});
  } else {
    html = generateBunnyHlsEmbedHtml(signedSource, 'Test student', '', { relaySource, serverNowMs: 0 });
  }
  const playerScript = html.slice(html.indexOf('(function(){'), html.lastIndexOf('</script>'));
  assert.doesNotMatch(playerScript, /\$\{/);

  const messages: PlayerMessage[] = [];
  const hlsListeners = new Map<string, (event: unknown, payload: unknown) => void>();
  const videoListeners = new Map<string, () => void>();
  const documentListeners = new Map<string, (event: { type: string }) => void>();
  let now = 0;
  let deviceClockOffset = 0;
  let nativeRequests = 0;
  const nativeRequestUrls: string[] = [];
  const timers: Array<{ callback: () => void; active: boolean; due: number }> = [];
  const video = {
    error: null as { code: number } | null,
    currentTime: 0,
    readyState: 4,
    duration: Number.NaN,
    ended: false,
    muted: false,
    paused: true,
    playbackRate: 1,
    volume: 1,
    src: '',
    loadCalls: 0,
    removeAttribute(name: string) { if (name === 'src') this.src = ''; },
    addEventListener(eventName: string, callback: () => void) {
      videoListeners.set(eventName, callback);
    },
    removeEventListener(eventName: string, callback: () => void) { if (videoListeners.get(eventName) === callback) videoListeners.delete(eventName); },
    canPlayType() { return runtime === 'native-apple' ? 'probably' : ''; },
    load() { this.loadCalls += 1; this.currentTime = 0; this.paused = true; },
    pause() { this.paused = true; videoListeners.get('pause')?.(); },
    play() { this.paused = false; return Promise.resolve(); },
  };

  const networkRequests: string[] = [];
  class NetworkLoader {
    stats = {};
    load(context: { url: string }) { networkRequests.push(context.url); }
    abort() {}
    destroy() {}
  }

  class FakeHls {
    static DefaultConfig = { loader: NetworkLoader };
    static Events = { ERROR: 'error', LEVEL_SWITCHED: 'levelSwitched', MANIFEST_PARSED: 'manifestParsed', LEVEL_LOADED: 'levelLoaded', FRAG_LOADING: 'fragmentLoading', FRAG_LOADED: 'fragmentLoaded' };
    static ErrorTypes = { MEDIA_ERROR: 'mediaError', NETWORK_ERROR: 'networkError' };
    static isSupported() { return true; }
    levels: unknown[] = [];
    autoLevelEnabled = true;
    currentLevel = -1;
    nextLevel = -1;
    startLoadCalls = 0;
    destroyCalls = 0;
    mediaRecoveryCalls = 0;
    config: Record<string, unknown>;
    source = '';
    constructor(config: Record<string, unknown>) { this.config = config; }
    loadSource(source: string) { this.source = source; }
    attachMedia() {}
    recoverMediaError() { this.mediaRecoveryCalls += 1; video.currentTime = 0; video.pause(); }
    startLoad() { this.startLoadCalls += 1; }
    destroy() { this.destroyCalls += 1; }
    on(eventName: string, callback: (event: unknown, payload: unknown) => void) {
      hlsListeners.set(eventName, callback);
    }
  }

  const parentWindow = {
    postMessage(message: PlayerMessage) { messages.push(message); },
  };
  let receiveCommand: ((event: unknown) => void) | undefined;
  const windowLike: {
    Hls: typeof FakeHls | undefined;
    MassarVcdnTransport: typeof vcdnTransport;
    addEventListener: (name: string, listener: (event: unknown) => void) => void;
    location: { origin: string };
    parent: typeof parentWindow;
  } = {
    Hls: runtime === 'hlsjs' ? FakeHls : undefined,
    MassarVcdnTransport: vcdnTransport,
    addEventListener(name, listener) { if (name === 'message') receiveCommand = listener; },
    location: { origin: 'https://app.massar-academy.net' },
    parent: parentWindow,
  };
  const hlsInstances: FakeHls[] = [];
  const InstrumentedHls = class extends FakeHls {
    constructor(config: Record<string, unknown>) {
      super(config);
      hlsInstances.push(this);
    }
  };
  if (runtime === 'hlsjs') windowLike.Hls = InstrumentedHls;

  vm.runInNewContext(playerScript, {
    URL,
    Date: { now: () => now + deviceClockOffset },
    performance: { now: () => now },
    clearTimeout(timer: { active: boolean }) { timer.active = false; },
    document: {
      addEventListener(name: string, listener: (event: { type: string }) => void) { documentListeners.set(name, listener); },
      getElementById(id: string) {
        return id === 'video' ? video : { style: { transform: '' } };
      },
    },
    fetch(url: string) {
      if (runtime === 'hlsjs') throw new Error('Native HLS fetch must not run when Hls.js is supported.');
      nativeRequestUrls.push(url);
      nativeRequests += 1;
      if (nativeManifestStatus === -1 && nativeRequests === 1) return Promise.reject(new TypeError('Network unavailable'));
      const status = nativeManifestStatus === -1 ? 200 : nativeManifestStatus;
      return Promise.resolve({
        ok: status === 200,
        status,
        text: () => Promise.resolve('#EXTM3U\n#EXT-X-STREAM-INF:RESOLUTION=1280x720\n720p/video.m3u8\n'),
      });
    },
    isFinite,
    location: windowLike.location,
    Math,
    Number,
    parent: parentWindow,
    Promise,
    setInterval() { return 1; },
    setTimeout(callback: () => void, delay: number) {
      const timer = { callback, active: true, due: now + delay };
      timers.push(timer);
      return timer;
    },
    window: windowLike,
  });

  return {
    setDeviceClockOffset(offset: number) { deviceClockOffset = offset; },
    emitFatalNetworkError(status: number) {
      hlsListeners.get('error')?.(null, {
        fatal: true,
        type: 'networkError',
        details: 'manifestLoadError',
        response: { code: status },
      });
    },
    emitManifestParsed() {
      hlsListeners.get('manifestParsed')?.(null, {});
    },
    emitFatalMediaError() {
      hlsListeners.get('error')?.(null, { fatal: true, type: 'mediaError', details: 'bufferAppendError' });
    },
    emitLevelLoaded() { hlsListeners.get('levelLoaded')?.(null, {}); },
    emitFragmentLoaded() { hlsListeners.get('fragmentLoaded')?.(null, {}); },
    beginFragmentDownload() {
      const stats = { loaded: 0 };
      hlsListeners.get('fragmentLoading')?.(null, { frag: { stats } });
      return stats;
    },
    hls: () => hlsInstances[0] ?? null,
    hlsInstances,
    nativeSource: () => video.src,
    nativeRequests: () => nativeRequests,
    nativeRequestUrls,
    interact(type: string) { documentListeners.get(type)?.({ type }); },
    messages,
    video,
    command(type: string, payload: Record<string, unknown> = {}) { receiveCommand?.({ origin: windowLike.location.origin, source: parentWindow, data: { type, ...payload } }); },
    networkRequests,
    requestResource(url: string) {
      const loaderConstructor = hlsInstances.at(-1)?.config.loader as new () => NetworkLoader;
      const loader = new loaderConstructor();
      let rejectedStatus = 0;
      const callbacks = { onError(error: { code: number }) { rejectedStatus = error.code; } };
      (loader.load as (context: { url: string }, config: object, callbacks: object) => void)({ url }, {}, callbacks);
      return { loader, rejectedStatus: () => rejectedStatus };
    },
    setMediaTime(time: number) { video.currentTime = time; },
    advanceTime(milliseconds: number) {
      now += milliseconds;
      for (const timer of timers) {
        if (timer.active && timer.due <= now) {
          timer.active = false;
          timer.callback();
        }
      }
    },
    triggerVideoEvent(eventName: string) {
      if (eventName === 'play' || eventName === 'playing') video.paused = false;
      if (eventName === 'pause') video.paused = true;
      videoListeners.get(eventName)?.();
    },
    triggerLoadDeadline() {
      for (const timer of timers) if (timer.active) timer.callback();
    },
};
}

test('portrait quality choices use the short resolution dimension and retain the actual HLS level IDs', async () => {
  const player = await runHlsPlayer();
  player.hls()!.levels = [
    { width: 360, height: 640, bitrate: 928000 },
    { width: 720, height: 1280, bitrate: 2628000 },
    { width: 1080, height: 1920, bitrate: 5128000 },
  ];
  player.emitManifestParsed();
  const levels = player.messages.findLast(message => message.type === 'qualityLevels')?.data?.levels;
  assert.deepEqual(levels?.map(level => [level.id, level.label]), [['0', '360p'], ['1', '720p'], ['2', '1080p']]);
  player.video.currentTime = 12;
  player.command('setQuality', { quality: '1' });
  assert.equal(player.hls()?.currentLevel, 1);
  assert.equal(player.hls()?.nextLevel, 1);
  assert.equal(player.video.currentTime, 12);
  player.command('setQuality', { quality: 'auto' });
  assert.equal(player.hls()?.currentLevel, -1);
  assert.equal(player.hls()?.nextLevel, -1);
});

test('VCDN decoder failure gives a file compatibility message instead of suggesting a domain restriction', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', { platformSource: 'https://cdn.vcdn.me/stream/11111111-1111-4111-8111-111111111111/providers/p1/master.m3u8?token=test' });
  player.video.error = { code: 3 };
  player.triggerVideoEvent('error');
  const failure = player.messages.find(message => message.type === 'error');
  assert.equal(failure?.data?.phase, 'media_error');
  assert.match(failure?.data?.message ?? '', /فك ترميز/);
  assert.doesNotMatch(failure?.data?.message ?? '', /النطاق/);
});

test('2026-09-11 Nader signed path expiry reaches the parent and permits renewal after CDN rejection', async () => {
  const videoId = '4512bcd5-2688-4a53-bbd1-e41a20b8ce6c';
  const expirySeconds = 2000000000;
  const source = `https://vz-example.b-cdn.net/bcdn_token=HS256-test&expires=${expirySeconds}&token_path=%2F${videoId}%2F/${videoId}/playlist.m3u8`;
  const player = await runHlsPlayer('hlsjs', 200, '', source);
  player.triggerVideoEvent('loadedmetadata');
  for (const type of ['providerLoaded', 'ready']) {
    const expiry = player.messages.find(message => message.type === type)?.data?.signedSourceExpiresAtMs ?? 0;
    assert.equal(expiry, expirySeconds * 1000, type);
    assert.equal(isExpiredHlsSourceError(403, expiry, expiry + 1), true);
    assert.equal(isExpiredHlsSourceError(403, expiry, expiry - 1), false);
  }
});

for (const phase of ['startup', 'playback'] as const) {
  test(`2026-09-11 Nader ${phase} keeps receiving a slow segment but still bounds an unusable stream`, async () => {
    const player = await runHlsPlayer();
    if (phase === 'startup') player.emitLevelLoaded();
    else {
      player.triggerVideoEvent('loadedmetadata');
      player.triggerVideoEvent('play');
      player.triggerVideoEvent('waiting');
    }
    const download = player.beginFragmentDownload();
    for (let elapsed = 15000; elapsed <= 45000; elapsed += 15000) {
      download.loaded += 1024;
      player.advanceTime(15000);
      assert.equal(player.messages.some(message => message.type === 'error'), false, `${elapsed}ms with incoming bytes`);
    }
    for (let elapsed = 50000; elapsed <= 125000; elapsed += 5000) {
      download.loaded += 1024;
      player.advanceTime(5000);
    }
    assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  });
}

test('2026-09-10 slow first fragment survives the old twenty-second cutoff', async () => {
  const player = await runHlsPlayer();
  player.advanceTime(12000);
  player.emitManifestParsed();
  player.advanceTime(6000);
  player.emitLevelLoaded();
  player.advanceTime(22000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.emitFragmentLoaded();
  player.triggerVideoEvent('loadedmetadata');
  player.advanceTime(60000);
  assert.equal(player.messages.filter(message => message.type === 'ready').length, 1);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
});

test('2026-09-10 repeated milestones cannot extend startup beyond its sixty-second cap', async () => {
  const player = await runHlsPlayer();
  player.advanceTime(15000);
  player.emitManifestParsed();
  player.advanceTime(15000);
  player.emitLevelLoaded();
  player.advanceTime(15000);
  player.emitFragmentLoaded();
  player.advanceTime(14000);
  player.emitLevelLoaded();
  player.emitFragmentLoaded();
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(1000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hls()?.destroyCalls, 1);
});

test('2026-09-03 Bunny HLS 403 stops loading with its real cause and never falls back', async () => {
  const player = await runHlsPlayer();

  player.emitFatalNetworkError(403);

  assert.equal(player.messages[0]?.type, 'providerLoaded');
  const errorMessage = player.messages.find((message) => message.type === 'error');
  assert.equal(errorMessage?.data?.provider, 'bunny-hls');
  assert.equal(errorMessage?.data?.code, 403);
  assert.equal(errorMessage?.data?.phase, 'manifestLoadError');
  assert.match(errorMessage?.data?.message ?? '', /Token Authentication Key/);
  assert.equal(player.hls()?.startLoadCalls, 0);
  assert.equal(player.hls()?.destroyCalls, 1);
  assert.equal(player.messages.some((message) => message.data?.provider === 'bunny'), false);
});

test('2026-09-07 pointer and touch inside the HLS iframe reveal the parent controls', async () => {
  const player = await runHlsPlayer();
  for (const type of ['pointermove', 'pointerdown', 'touchstart', 'keydown']) player.interact(type);
  assert.equal(player.messages.filter(message => message.type === 'playerInteraction').length, 4);
  player.interact('pointermove');
  assert.equal(player.messages.filter(message => message.type === 'playerInteraction').length, 4);
  player.advanceTime(200);
  player.interact('pointermove');
  assert.equal(player.messages.filter(message => message.type === 'playerInteraction').length, 5);
});

test('2026-09-18 unreachable CDN switches to relay, retries once, then fails visibly', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.emitFatalNetworkError(0);
  assert.equal(player.hlsInstances.length, 2);
  assert.equal(player.hlsInstances[0].destroyCalls, 1);
  assert.equal(player.hlsInstances[1].source, 'https://app.massar-academy.net/api/video/hls?s=test-session');
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.emitFatalNetworkError(0);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.emitFatalNetworkError(0);
  assert.equal(player.hlsInstances.length, 2);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
});

test('relay is never attempted for rejected or missing HLS resources', async () => {
  for (const status of [401, 403, 404]) {
    const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
    player.emitFatalNetworkError(status);
    assert.equal(player.hlsInstances.length, 1);
    assert.equal(player.messages.find(message => message.type === 'error')?.data?.code, status);
  }
});

test('2026-09-12 relay manifest survives session validation plus upstream latency', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.emitFatalNetworkError(0);
  const config = player.hlsInstances[1].config;
  // The relay can spend 15s validating the session, then 20s retrieving a playlist.
  for (const name of ['manifestLoadPolicy', 'playlistLoadPolicy', 'keyLoadPolicy', 'fragLoadPolicy']) {
    const policy = config[name] as { default: { maxTimeToFirstByteMs: number; maxLoadTimeMs: number } };
    assert.ok(policy.default.maxTimeToFirstByteMs > 35000, name);
    assert.ok(policy.default.maxLoadTimeMs > 35000, name);
  }
  player.advanceTime(35000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.emitManifestParsed();
  player.advanceTime(35000);
  player.emitLevelLoaded();
  player.advanceTime(25000);
  player.triggerVideoEvent('loadedmetadata');
  assert.equal(player.messages.filter(message => message.type === 'ready').length, 1);
  player.advanceTime(120000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  assert.equal(player.hlsInstances.length, 2);
});

test('2026-09-12 relay playback waits for authenticated segment headers without restarting', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.emitFatalNetworkError(0);
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.triggerVideoEvent('waiting');
  player.advanceTime(35000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.setMediaTime(1);
  player.triggerVideoEvent('timeupdate');
  player.advanceTime(120000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  assert.equal(player.hlsInstances.length, 2);
});

test('relay milestones cannot extend startup beyond the overall two-minute cap', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.emitFatalNetworkError(0);
  player.advanceTime(44000);
  player.emitManifestParsed();
  player.advanceTime(44000);
  player.emitLevelLoaded();
  player.advanceTime(31000);
  player.emitFragmentLoaded();
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(1000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hlsInstances.length, 2);
});

test('relay startup gets one bounded deadline and does not loop', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.advanceTime(20000);
  assert.equal(player.hlsInstances.length, 2);
  player.advanceTime(44999);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(1);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  player.advanceTime(120000);
  assert.equal(player.hlsInstances.length, 2);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
});

test('repeated buffering events cannot extend the playback deadline forever', async () => {
  const player = await runHlsPlayer();
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  for (let i = 0; i < 3; i++) {
    player.advanceTime(4000);
    player.triggerVideoEvent('waiting');
  }
  player.advanceTime(3000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hls()?.destroyCalls, 1);
});

test('progress after seeking backwards clears the buffering deadline', async () => {
  const player = await runHlsPlayer();
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.setMediaTime(120);
  player.triggerVideoEvent('timeupdate');
  player.setMediaTime(30);
  player.triggerVideoEvent('seeking');
  player.triggerVideoEvent('waiting');
  player.advanceTime(5000);
  player.setMediaTime(31);
  player.triggerVideoEvent('timeupdate');
  player.advanceTime(15000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
});

test('pausing cancels a stall and resuming gets a full playback deadline', async () => {
  const player = await runHlsPlayer();
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.advanceTime(14000);
  player.triggerVideoEvent('pause');
  player.advanceTime(20000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.triggerVideoEvent('play');
  player.advanceTime(14000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(1000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
});

test('2026-09-03 Bunny HLS disables hidden manifest and segment reload loops', async () => {
  const player = await runHlsPlayer();
  const config = player.hls()?.config as {
    manifestLoadPolicy?: { default?: { timeoutRetry?: { maxNumRetry?: number }; errorRetry?: { maxNumRetry?: number } } };
    fragLoadPolicy?: { default?: { timeoutRetry?: { maxNumRetry?: number }; errorRetry?: { maxNumRetry?: number } } };
  } | undefined;

  assert.equal(config?.manifestLoadPolicy?.default?.timeoutRetry?.maxNumRetry, 0);
  assert.equal(config?.manifestLoadPolicy?.default?.errorRetry?.maxNumRetry, 0);
  assert.equal(config?.fragLoadPolicy?.default?.timeoutRetry?.maxNumRetry, 0);
  assert.equal(config?.fragLoadPolicy?.default?.errorRetry?.maxNumRetry, 0);
  assert.equal(player.hls()?.config.preferManagedMediaSource, true);
});

test('2026-09-03 stalled Bunny HLS exits the tablet spinner on its deadline', async () => {
  const player = await runHlsPlayer();

  player.triggerLoadDeadline();

  const errorMessage = player.messages.find((message) => message.type === 'error');
  assert.equal(errorMessage?.data?.provider, 'bunny-hls');
  assert.match(errorMessage?.data?.message ?? '', /انتهت مهلة تجهيز فيديو Bunny HLS/);
  assert.equal(errorMessage?.data?.phase, 'load_timeout_bootstrap');
  assert.equal(player.hls()?.destroyCalls, 1);
});

test('2026-09-04 parsed master playlist is not treated as playable video', async () => {
  const player = await runHlsPlayer();

  player.emitManifestParsed();

  assert.equal(player.messages.some((message) => message.type === 'ready'), false);
  player.triggerVideoEvent('loadedmetadata');
  assert.equal(player.messages.filter((message) => message.type === 'ready').length, 1);
});

test('2026-09-04 playback stall reports its exact phase instead of spinning forever', async () => {
  const player = await runHlsPlayer();
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.triggerVideoEvent('waiting');

  player.triggerLoadDeadline();

  const errorMessage = player.messages.find((message) => message.type === 'error');
  assert.equal(errorMessage?.data?.provider, 'bunny-hls');
  assert.equal(errorMessage?.data?.phase, 'playback_timeout_waiting');
  assert.match(errorMessage?.data?.message ?? '', /لم تصل بيانات جديدة/);
});

test('2026-09-04 Apple native HTTP 403 preserves the confirmed Bunny rejection', async () => {
  const player = await runHlsPlayer('native-apple', 403, '/api/video/hls?s=test-session');
  await new Promise<void>((resolve) => setImmediate(resolve));
  const error = player.messages.find(message => message.type === 'error');
  assert.equal(error?.data?.code, 403);
  assert.equal(error?.data?.phase, 'native_manifest_http');
  assert.match(error?.data?.message ?? '', /Token Authentication Key/);
  assert.equal(player.messages.some(message => message.type === 'ready'), false);
  assert.equal(player.nativeRequests(), 1);
});

test('native mobile HLS can use the same-origin relay after a direct network failure', async () => {
  const player = await runHlsPlayer('native-apple', -1, '/api/video/hls?s=test-session');
  await new Promise<void>((resolve) => setImmediate(resolve));
  assert.equal(player.nativeRequests(), 2);
  assert.equal(player.nativeSource(), 'https://app.massar-academy.net/api/video/hls?s=test-session');
  player.triggerVideoEvent('loadedmetadata');
  player.advanceTime(20000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  assert.equal(player.messages.filter(message => message.type === 'ready').length, 1);
});

test('unknown native media failure does not falsely blame Bunny domain protection', async () => {
  const player = await runHlsPlayer('native-apple');
  await new Promise<void>((resolve) => setImmediate(resolve));

  player.triggerVideoEvent('error');

  const errorMessage = player.messages.find((message) => message.type === 'error');
  assert.equal(errorMessage?.data?.provider, 'bunny-hls');
  assert.equal(errorMessage?.data?.phase, 'native_media_error');
  assert.match(errorMessage?.data?.message ?? '', /لم يحدد المتصفح سبب التعطل/);
  assert.doesNotMatch(errorMessage?.data?.message ?? '', /Allowed Domains|Hotlink Protection|403/);
  assert.equal(player.messages.some((message) => message.type === 'ready'), false);
});

for (const runtime of ['hlsjs', 'native-apple'] as const) {
  test(`2026-09-10 stall after metadata uses relay and resumes the same position: ${runtime}`, async () => {
    const player = await runHlsPlayer(runtime, 200, '/api/video/hls?s=test-session');
    await new Promise<void>((resolve) => setImmediate(resolve));
    player.video.duration = 600;
    player.triggerVideoEvent('loadedmetadata');
    player.setMediaTime(123);
    player.video.playbackRate = 1.5;
    player.video.volume = 0.4;
    player.video.muted = true;
    player.triggerVideoEvent('play');
    player.triggerVideoEvent('waiting');
    player.advanceTime(15000);
    await new Promise<void>((resolve) => setImmediate(resolve));
    assert.equal(player.messages.some(message => message.type === 'error'), false);
    if (runtime === 'hlsjs') {
      assert.equal(player.hlsInstances.length, 2);
      assert.match(player.hlsInstances[1].source, /\/api\/video\/hls/);
      assert.equal(player.hlsInstances[1].config.startPosition, 123);
    } else {
      assert.equal(player.nativeRequests(), 2);
      assert.match(player.nativeSource(), /\/api\/video\/hls/);
    }
    player.setMediaTime(0);
    player.video.playbackRate = 1;
    player.triggerVideoEvent('loadedmetadata');
    assert.equal(player.video.currentTime, 123);
    assert.equal(player.video.playbackRate, 1.5);
    assert.equal(player.video.volume, 0.4);
    assert.equal(player.video.muted, true);
    assert.equal(player.video.paused, false);
    player.triggerVideoEvent('playing');
    player.advanceTime(60000);
    assert.equal(player.messages.filter(message => message.type === 'ready').length, 1);
    assert.equal(player.messages.some(message => message.type === 'error'), false);
  });
}

test('post-start relay honors a pause command and fails once if recovery never loads', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.emitFatalNetworkError(0);
  player.command('pause');
  player.triggerVideoEvent('loadedmetadata');
  assert.equal(player.video.paused, true);
  player.command('play');
  player.triggerVideoEvent('waiting');
  player.advanceTime(45000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
  assert.equal(player.hlsInstances.at(-1)?.startLoadCalls, 1);
  player.advanceTime(45000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hlsInstances.length, 2);
  assert.match(player.messages.find(message => message.type === 'error')?.data?.phase ?? '', /^relay_playback_timeout/);
});

test('recovery after metadata still has a bounded startup deadline', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.advanceTime(15000);
  player.advanceTime(12000);
  player.emitManifestParsed();
  player.advanceTime(15000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(30000);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hlsInstances.length, 2);
});

const renewableVideoId = '4512bcd5-2688-4a53-bbd1-e41a20b8ce6c';
const vcdnPlatformSource = 'https://cdn.vcdn.me/stream/11111111-1111-4111-8111-111111111111/providers/p1/master.m3u8?token=initial';

test('VCDN platform sessions stop buffered playback at the grant deadline even without more network requests', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', { platformSource: vcdnPlatformSource });
  player.triggerVideoEvent('loadedmetadata');
  player.video.paused = false;
  player.advanceTime(60_000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
  player.advanceTime(120_000);
  assert.equal(player.video.paused, true);
  assert.equal(player.messages.find(message => message.type === 'error')?.data?.code, 410);
  player.command('play');
  assert.equal(player.video.paused, true);
});

test('VCDN platform renewal preserves playback and expires the renewed grant instead of the original one', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', { platformSource: vcdnPlatformSource });
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 12;
  player.video.playbackRate = 1.5;
  player.advanceTime(60_000);
  const replacement = vcdnPlatformSource.replace('token=initial', 'token=renewed');
  player.command('renewSource', { source: replacement, signedSourceExpiresAtMs: 240_000 });
  const segment = 'https://edge.vcdn.me/seg/0123456789abcdefghijklmnop';
  player.requestResource(segment);
  assert.equal(player.networkRequests.at(-1), segment);
  player.requestResource(vcdnPlatformSource.replace('master.m3u8', '720p/playlist.m3u8'));
  assert.equal(player.networkRequests.at(-1), replacement.replace('master.m3u8', '720p/playlist.m3u8'));
  assert.equal(player.video.currentTime, 12);
  assert.equal(player.video.playbackRate, 1.5);
  player.advanceTime(120_000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  player.advanceTime(60_000);
  assert.equal(player.messages.find(message => message.type === 'error')?.data?.code, 410);
});

test('VCDN PNG delivery rejects native-only browsers rather than silently loading undecodable carrier bytes', async () => {
  const player = await runHlsPlayer('native-apple', 200, '', { platformSource: vcdnPlatformSource });
  assert.equal(player.nativeRequests(), 0);
  assert.equal(player.messages.find(message => message.type === 'error')?.data?.phase, 'vcdn_transport_unsupported');
});

function signedPlaylist(expires: number, token = 'initial', videoId = renewableVideoId) {
  return `https://vz-example.b-cdn.net/bcdn_token=${token}&expires=${expires}&token_path=%2F${videoId}%2F/${videoId}/playlist.m3u8`;
}

test('renewal updates future playlists, segments and keys without replacing the player or its playback state', async () => {
  const initial = signedPlaylist(300);
  const renewed = signedPlaylist(480, 'renewed');
  const player = await runHlsPlayer('hlsjs', 200, '', initial);
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 87;
  player.video.volume = 0.4;
  player.video.playbackRate = 1.5;
  player.video.paused = false;
  player.hls()!.currentLevel = 2;
  player.hls()!.nextLevel = 2;

  player.advanceTime(180000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
  player.command('renewSource', { source: renewed, signedSourceExpiresAtMs: 480000 });
  for (const resource of ['playlist.m3u8', '720p/video.m3u8', '720p/video0.ts', '720p/init.mp4', 'encryption.key']) {
    player.requestResource(new URL(resource, initial).href);
    assert.equal(player.networkRequests.at(-1), new URL(resource, renewed).href);
  }
  assert.equal(player.hlsInstances.length, 1);
  assert.equal(player.hls()?.destroyCalls, 0);
  assert.equal(player.hls()?.startLoadCalls, 0);
  assert.equal(player.hls()?.currentLevel, 2);
  assert.equal(player.hls()?.nextLevel, 2);
  assert.equal(player.video.currentTime, 87);
  assert.equal(player.video.volume, 0.4);
  assert.equal(player.video.playbackRate, 1.5);
  assert.equal(player.video.paused, false);
  assert.equal(player.video.loadCalls, 0);
  player.advanceTime(180000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
});

test('requests after expiry wait for a renewed signature and aborted requests never resume', async () => {
  const initial = signedPlaylist(300);
  const player = await runHlsPlayer('hlsjs', 200, '', initial);
  player.triggerVideoEvent('loadedmetadata');
  player.advanceTime(300000);
  player.requestResource(new URL('720p/video0.ts', initial).href);
  const cancelled = player.requestResource(new URL('720p/video1.ts', initial).href);
  cancelled.loader.abort();
  assert.equal(player.networkRequests.length, 0);

  player.command('renewSource', { source: signedPlaylist(600, 'fresh'), signedSourceExpiresAtMs: 600000 });
  assert.deepEqual(player.networkRequests, [new URL('720p/video0.ts', signedPlaylist(600, 'fresh')).href]);
  assert.equal(player.hlsInstances.length, 1);
});

test('a renewal cannot redirect the player or its authenticated resource requests outside the original video', async () => {
  const initial = signedPlaylist(300);
  for (const invalidSource of [
    signedPlaylist(600, 'foreign', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
    signedPlaylist(600).replace('vz-example.b-cdn.net', 'other.b-cdn.net'),
    signedPlaylist(600).replace('https:', 'http:'),
    signedPlaylist(600).replace(`%2F${renewableVideoId}%2F`, '%2Faaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa%2F'),
  ]) {
    const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=session', initial);
    player.command('renewSource', { source: invalidSource, signedSourceExpiresAtMs: 600000 });
    assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
    assert.equal(player.hls()?.destroyCalls, 0);
    assert.equal(player.hlsInstances.length, 1);
    assert.equal(player.networkRequests.length, 0);
  }
  const player = await runHlsPlayer('hlsjs', 200, '', initial);
  for (const invalidResource of [
    'https://other.b-cdn.net/segment.ts',
    new URL('../another-video/segment.ts', initial).href,
    new URL('segment.ts?token=untrusted', initial).href,
    signedPlaylist(300, 'foreign', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'),
  ]) {
    assert.equal(player.requestResource(invalidResource).rejectedStatus(), 403);
  }
  assert.equal(player.networkRequests.length, 0);
});

test('renewal failure retries are bounded and authorization denial never triggers a bandwidth relay', async () => {
  for (const status of [401, 403, 404, 409, 410, 503]) {
    const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=session', signedPlaylist(300));
    player.triggerVideoEvent('loadedmetadata');
    player.advanceTime(180000);
    const attempts = status === 503 ? 4 : 1;
    for (let attempt = 0; attempt < attempts; attempt++) {
      player.command('sourceRenewalFailed', { status });
      player.advanceTime(30000);
    }
    assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, attempts);
    assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
    assert.equal(player.hlsInstances.length, 1);
    assert.equal(player.hls()?.destroyCalls, 1);
    player.command('play');
    assert.equal(player.video.paused, true);
    player.advanceTime(300000);
    assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, attempts);
  }
});

test('native Safari receives a session-length grant before playback and refreshes access without a reload', async () => {
  const player = await runHlsPlayer('native-apple', 200, '', signedPlaylist(300));
  assert.equal(player.nativeRequests(), 0);
  assert.equal(player.messages.find(message => message.type === 'renewSourceRequired')?.data?.native, true);
  const nativeSource = signedPlaylist(3600, 'native');
  player.command('renewSource', { source: nativeSource, signedSourceExpiresAtMs: 3600000 });
  await new Promise<void>(resolve => setImmediate(resolve));
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 140;
  player.video.paused = false;
  assert.equal(player.nativeSource(), nativeSource);
  assert.equal(player.video.loadCalls, 1);

  player.advanceTime(180000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
  player.command('renewSource', { source: signedPlaylist(3600, 'native-updated'), signedSourceExpiresAtMs: 3600000 });
  assert.equal(player.nativeRequests(), 1);
  assert.equal(player.nativeSource(), nativeSource);
  assert.equal(player.video.loadCalls, 1);
  assert.equal(player.video.currentTime, 140);
  assert.equal(player.video.paused, false);

  player.advanceTime(180000);
  player.command('sourceRenewalFailed', { status: 403 });
  assert.equal(player.nativeSource(), '');
  assert.equal(player.video.paused, true);
});

test('a signature capped at the watch-session end stays usable and cannot trigger a renewal loop', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', signedPlaylist(300));
  player.triggerVideoEvent('loadedmetadata');
  player.advanceTime(290000);
  player.command('renewSource', {
    source: signedPlaylist(300, 'last-ten-seconds'), signedSourceExpiresAtMs: 300000, sessionExpiresAtMs: 300500,
  });
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
  player.advanceTime(9999);
  player.command('play');
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
  player.advanceTime(1);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
  player.command('sourceRenewalFailed', { status: 404 });
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
});

test('relay JWT expiry requests a parent auth refresh and resumes the same player with bounded retries', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=session', signedPlaylist(300));
  player.triggerVideoEvent('loadedmetadata');
  player.emitFatalNetworkError(0);
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 95;
  player.video.paused = false;
  const relay = player.hlsInstances[1];
  for (let attempt = 0; attempt < 2; attempt++) {
    player.emitFatalNetworkError(401);
    assert.equal(player.messages.some(message => message.type === 'error'), false);
    player.command('renewSource', { source: signedPlaylist(600, 'refreshed'), signedSourceExpiresAtMs: 600000, sessionExpiresAtMs: 3600000 });
    assert.equal(player.hlsInstances.length, 2);
    assert.equal(relay.destroyCalls, 0);
    assert.equal(player.video.currentTime, 95);
    assert.equal(player.video.paused, false);
  }
  assert.equal(relay.startLoadCalls, 2);
  player.emitFatalNetworkError(401);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
});

for (const offset of [-86400000, 86400000]) {
  test(`incident 2026-09-14: device clock offset ${offset} does not reject valid playback renewals`, async () => {
    const initial = signedPlaylist(300);
    const player = await runHlsPlayer('hlsjs', 200, '', initial);
    player.setDeviceClockOffset(offset);
    player.triggerVideoEvent('loadedmetadata');
    player.video.currentTime = 87;
    player.video.paused = false;
    player.advanceTime(180000);
    assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
    player.command('renewSource', {
      source: signedPlaylist(480, 'renewed'), serverNowMs: 180000,
      signedSourceExpiresAtMs: 480999, // Redundant metadata is not an authorization authority.
    });
    player.requestResource(new URL('720p/video0.ts', initial).href);
    assert.equal(player.networkRequests.at(-1), new URL('720p/video0.ts', signedPlaylist(480, 'renewed')).href);
    assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
    assert.equal(player.hlsInstances.length, 1);
    assert.equal(player.video.currentTime, 87);
    assert.equal(player.video.paused, false);
    player.advanceTime(180000);
    assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
  });
}

test('a delayed expired renewal retries without destroying playback', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', signedPlaylist(300));
  player.triggerVideoEvent('loadedmetadata');
  player.advanceTime(180000);
  player.command('renewSource', { source: signedPlaylist(170, 'delayed'), serverNowMs: 180000 });
  assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
  assert.equal(player.hls()?.destroyCalls, 0);
  player.advanceTime(5000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
  player.command('renewSource', { source: signedPlaylist(485, 'fresh'), serverNowMs: 185000 });
  assert.equal(player.messages.filter(message => message.type === 'sourceRenewed').length, 1);
});

test('a mismatched renewal reply retries while retaining the original authorized video', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', signedPlaylist(300));
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 77;
  player.advanceTime(180000);
  player.command('renewSource', { source: signedPlaylist(600, 'stale', 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa') });
  assert.equal(player.hls()?.destroyCalls, 0);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
  player.advanceTime(5000);
  assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 2);
  player.command('renewSource', { source: signedPlaylist(485, 'correct'), serverNowMs: 185000 });
  assert.equal(player.messages.filter(message => message.type === 'sourceRenewed').length, 1);
  assert.equal(player.video.currentTime, 77);
});

test('an early CDN rejection gets bounded source renewal without a bandwidth relay', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=session', signedPlaylist(1800));
  player.triggerVideoEvent('loadedmetadata');
  for (let attempt = 0; attempt < 2; attempt++) {
    player.emitFatalNetworkError(403);
    assert.equal(player.messages.filter(message => message.type === 'error').length, 0);
    player.command('renewSource', { source: signedPlaylist(1800, `renewed-${attempt}`), serverNowMs: 0 });
  }
  player.emitFatalNetworkError(403);
  assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  assert.equal(player.hlsInstances.length, 1);
});


test('2026-09-18 stalled downloads do not stop learning time while buffered media is playing', async () => {
  const player = await runHlsPlayer();
  player.triggerVideoEvent('loadedmetadata');
  player.triggerVideoEvent('play');
  player.triggerVideoEvent('stalled');
  const beforeBuffering = player.messages.filter(message => message.type === 'stateChange').at(-1);
  assert.equal((beforeBuffering?.data as { isPlaying?: boolean }).isPlaying, true);
  player.video.readyState = 2;
  player.triggerVideoEvent('stalled');
  const afterBuffering = player.messages.filter(message => message.type === 'stateChange').at(-1);
  assert.equal((afterBuffering?.data as { isPlaying?: boolean }).isPlaying, false);
});

test('2026-09-18 relay stall recovers in place and keeps position and speed', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
  player.emitFatalNetworkError(0);
  player.triggerVideoEvent('loadedmetadata');
  player.video.playbackRate = 1.5;
  player.setMediaTime(120);
  player.triggerVideoEvent('play');
  player.triggerVideoEvent('waiting');
  player.advanceTime(45000);
  assert.equal(player.hlsInstances.at(-1)?.startLoadCalls, 1);
  assert.equal(player.video.currentTime, 120);
  assert.equal(player.video.playbackRate, 1.5);
  player.setMediaTime(121);
  player.triggerVideoEvent('timeupdate');
  player.advanceTime(45000);
  assert.equal(player.messages.some(message => message.type === 'error'), false);
  assert.equal(player.hlsInstances.length, 2);
});

for (const status of [0, 408, 502, 503]) {
  test(`2026-09-18 transient relay failure ${status} resumes current position with one retry`, async () => {
    const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
    player.emitFatalNetworkError(0);
    player.triggerVideoEvent('loadedmetadata');
    player.video.currentTime = 123;
    player.video.playbackRate = 1.5;
    player.triggerVideoEvent('play');
    player.emitFatalNetworkError(status);
    assert.equal(player.messages.some(message => message.type === 'error'), false);
    assert.equal(player.video.currentTime, 123);
    assert.equal(player.video.playbackRate, 1.5);
    assert.equal(player.hlsInstances.at(-1)?.startLoadCalls, 1);
    player.setMediaTime(125);
    player.triggerVideoEvent('timeupdate');
    player.emitFatalNetworkError(status);
    assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
  });
}

for (const status of [401, 403, 404, 409, 410, 429]) {
  test(`relay rejection ${status} is not retried as a transient network error`, async () => {
    const player = await runHlsPlayer('hlsjs', 200, '/api/video/hls?s=test-session');
    player.emitFatalNetworkError(0);
    player.emitFatalNetworkError(status);
    assert.equal(player.messages.find(message => message.type === 'error')?.data?.code, status);
    assert.equal(player.hlsInstances.at(-1)?.startLoadCalls, 0);
  });
}

for (const runtime of ['hlsjs', 'native-apple'] as const) {
  test(`VCDN ${runtime} loads the canonical stream and obeys platform controls with a signed direct stream`, async () => {
    const source = 'https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/master.m3u8?token=initial';
    const player = await runHlsPlayer(runtime, 200, '', source);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(runtime === 'hlsjs' ? player.hls()?.source : player.nativeSource(), source);
    if (runtime === 'hlsjs') {
      const segment = 'https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/720p/video0.ts';
      player.requestResource(segment);
      assert.deepEqual(player.networkRequests, [segment + '?token=initial']);
    } else {
      assert.deepEqual(player.nativeRequestUrls, [source]);
    }
    assert.equal(player.messages.find(message => message.type === 'providerLoaded')?.data?.provider, 'vcdn');
    player.command('play');
    assert.equal(player.video.paused, false);
    player.command('seekTo', { time: 30 });
    assert.equal(player.video.currentTime, 30);
    player.command('setPlaybackRate', { rate: 1.5 });
    assert.equal(player.video.playbackRate, 1.5);
    player.video.duration = 120;
    player.triggerVideoEvent('loadedmetadata');
    assert.equal(player.messages.find(message => message.type === 'ready')?.data?.provider, 'vcdn');
    player.triggerVideoEvent('timeupdate');
    assert.equal(player.messages.findLast(message => message.type === 'timeUpdate')?.data?.currentTime, 30);
    player.command('pause');
    assert.equal(player.video.paused, true);
    assert.equal(player.messages.some(message => message.type === 'renewSourceRequired'), false);
  });
}

for (const runtime of ['hlsjs', 'native-apple'] as const) {
  test(`VCDN ${runtime} network failure stops playback without routing media through the application`, async () => {
    const source = 'https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/master.m3u8?token=initial';
    const player = await runHlsPlayer(runtime, -1, '/api/video/hls?s=test-session', source);
    if (runtime === 'hlsjs') player.emitFatalNetworkError(0);
    await new Promise(resolve => setImmediate(resolve));
    const errors = player.messages.filter(message => message.type === 'error');
    assert.equal(errors.length, 1);
    assert.equal(errors[0].data?.provider, 'vcdn');
    assert.equal(player.messages.some(message => message.type === 'renewSourceRequired'), false);
    if (runtime === 'hlsjs') assert.deepEqual(player.hlsInstances.map(instance => instance.source), [source]);
    else assert.deepEqual(player.nativeRequestUrls, [source]);
  });
}

const vcdnSource = 'https://stream.vcdn.me/11111111-1111-4111-8111-111111111111/master.m3u8?token=initial';
for (const status of [401, 403]) {
  test(`VCDN renews a rejected ${status} grant in place and signs segment requests with the new token`, async () => {
    const player = await runHlsPlayer('hlsjs', 200, '', vcdnSource);
    player.triggerVideoEvent('loadedmetadata');
    player.video.currentTime = 30;
    player.emitFatalNetworkError(status);
    assert.equal(player.messages.filter(message => message.type === 'renewSourceRequired').length, 1);
    assert.equal(player.messages.some(message => message.type === 'error'), false);
    player.command('renewSource', {source:vcdnSource.replace('initial', 'renewed'), signedSourceExpiresAtMs:900_000, serverNowMs:300_000, sessionExpiresAtMs:3_600_000});
    player.requestResource(vcdnSource.replace('master.m3u8?token=initial', '720p/segment.ts?token=initial'));
    assert.match(player.networkRequests[0], /segment\.ts\?token=renewed$/);
    assert.equal(player.video.currentTime, 30);
    assert.equal(player.hlsInstances.length, 1);
    assert.equal(player.hls()?.startLoadCalls, 1);
    assert.equal(player.networkRequests.some(url => url.includes('/api/video/')), false);
  });
}

test('VCDN never sends its token to resources or renewal sources outside the current video', async () => {
  const player = await runHlsPlayer('hlsjs', 200, '', vcdnSource);
  for (const resource of ['https://attacker.test/segment.ts', vcdnSource.replace('11111111-1111-4111-8111-111111111111', '22222222-2222-4222-8222-222222222222')]) {
    assert.equal(player.requestResource(resource).rejectedStatus(), 403);
  }
  player.command('renewSource', {source:'https://attacker.test/master.m3u8?token=stolen', signedSourceExpiresAtMs:900_000});
  assert.equal(player.hls()?.source, vcdnSource);
  assert.deepEqual(player.networkRequests, []);
});

test('VCDN native HLS renews directly and preserves playhead, rate and playing state', async () => {
  const player = await runHlsPlayer('native-apple', 200, '', vcdnSource);
  await new Promise(resolve => setImmediate(resolve));
  player.video.duration = 360;
  player.triggerVideoEvent('loadedmetadata');
  player.command('seekTo', {time:30});
  player.command('setPlaybackRate', {rate:1.5});
  player.command('play');
  player.command('renewSource', {source:vcdnSource.replace('initial', 'renewed'), signedSourceExpiresAtMs:780_000, serverNowMs:180_000, sessionExpiresAtMs:3_600_000});
  player.triggerVideoEvent('loadedmetadata');
  assert.match(player.nativeSource(), /token=renewed$/);
  assert.equal(player.video.currentTime, 30);
  assert.equal(player.video.playbackRate, 1.5);
  assert.equal(player.video.paused, false);
});

for (const mediaCode of [2, 3]) {
  test(`2026-10-09 browser media error ${mediaCode} recovers in place before stopping playback`, async () => {
    const player = await runHlsPlayer();
    player.video.duration = 600;
    player.triggerVideoEvent('loadedmetadata');
    player.video.currentTime = 87;
    player.video.playbackRate = 1.5;
    player.triggerVideoEvent('play');
    player.video.error = { code: mediaCode };
    player.triggerVideoEvent('error');
    player.triggerVideoEvent('loadedmetadata');
    assert.equal(player.hls()?.mediaRecoveryCalls, 1);
    assert.equal(player.hls()?.destroyCalls, 0);
    assert.equal(player.video.currentTime, 87);
    assert.equal(player.video.playbackRate, 1.5);
    assert.equal(player.messages.some(message => message.type === 'error'), false);
    // DOM and Hls.js failures share one budget; a broken source cannot retry forever.
    player.emitFatalMediaError();
    assert.equal(player.hls()?.mediaRecoveryCalls, 1);
    assert.equal(player.messages.filter(message => message.type === 'error').length, 1);
    assert.equal(player.messages.find(message => message.type === 'error')?.data?.wasPlaying, true);
  });
}

for (const playing of [true, false]) {
  test(`2026-10-09 expiry reports pre-cleanup playback intent when playing=${playing}`, async () => {
    const player = await runHlsPlayer('hlsjs', 200, '', signedPlaylist(300));
    player.triggerVideoEvent('loadedmetadata');
    if (playing) player.triggerVideoEvent('play');
    player.advanceTime(180000);
    player.command('sourceRenewalFailed', { status: 410 });
    assert.equal(player.video.paused, true);
    assert.equal(player.messages.find(message => message.type === 'error')?.data?.wasPlaying, playing);
  });
}

test('2026-10-09 media recovery without playable metadata stops on its deadline and retains resume intent', async () => {
  const player = await runHlsPlayer();
  player.video.duration = 600;
  player.triggerVideoEvent('loadedmetadata');
  player.video.currentTime = 87;
  player.triggerVideoEvent('play');
  player.video.error = { code: 3 };
  player.triggerVideoEvent('error');
  player.advanceTime(20000);
  const failure = player.messages.find(message => message.type === 'error');
  assert.equal(failure?.data?.wasPlaying, true);
  assert.equal(player.hls()?.mediaRecoveryCalls, 1);
  assert.equal(player.hls()?.destroyCalls, 1);
});

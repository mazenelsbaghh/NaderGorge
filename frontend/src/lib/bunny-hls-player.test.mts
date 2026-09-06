import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

const routePath = new URL('../app/api/video/embed/route.ts', import.meta.url);

type PlayerMessage = {
  source?: string;
  type?: string;
  data?: { code?: number; message?: string; phase?: string; provider?: string };
};

type HlsRuntime = 'hlsjs' | 'native-apple';

async function runHlsPlayer(runtime: HlsRuntime = 'hlsjs', nativeManifestStatus = 200) {
  const routeSource = await readFile(routePath, 'utf8');
  const generatorStart = routeSource.indexOf('function generateBunnyHlsEmbedHtml');
  const scriptStart = routeSource.indexOf("(function(){\n  'use strict';", generatorStart);
  const scriptEnd = routeSource.indexOf('</script>', scriptStart);
  assert.ok(generatorStart >= 0 && scriptStart > generatorStart && scriptEnd > scriptStart);

  const playerScript = routeSource
    .slice(scriptStart, scriptEnd)
    .replace('${safeSource}', JSON.stringify('https://vz-example.b-cdn.net/signed/video/playlist.m3u8'));
  assert.doesNotMatch(playerScript, /\$\{/);

  const messages: PlayerMessage[] = [];
  const hlsListeners = new Map<string, (event: unknown, payload: unknown) => void>();
  const videoListeners = new Map<string, () => void>();
  let now = 0;
  const timers: Array<{ callback: () => void; active: boolean; due: number }> = [];
  const video = {
    currentTime: 0,
    duration: Number.NaN,
    ended: false,
    muted: false,
    paused: true,
    playbackRate: 1,
    volume: 1,
    addEventListener(eventName: string, callback: () => void) {
      videoListeners.set(eventName, callback);
    },
    canPlayType() { return runtime === 'native-apple' ? 'probably' : ''; },
    load() {},
    pause() { this.paused = true; },
    play() { this.paused = false; return Promise.resolve(); },
  };

  class FakeHls {
    static Events = { ERROR: 'error', LEVEL_SWITCHED: 'levelSwitched', MANIFEST_PARSED: 'manifestParsed' };
    static ErrorTypes = { MEDIA_ERROR: 'mediaError', NETWORK_ERROR: 'networkError' };
    static isSupported() { return true; }
    levels: unknown[] = [];
    autoLevelEnabled = true;
    currentLevel = -1;
    nextLevel = -1;
    startLoadCalls = 0;
    destroyCalls = 0;
    config: Record<string, unknown>;
    constructor(config: Record<string, unknown>) { this.config = config; }
    loadSource() {}
    attachMedia() {}
    recoverMediaError() {}
    startLoad() { this.startLoadCalls += 1; }
    destroy() { this.destroyCalls += 1; }
    on(eventName: string, callback: (event: unknown, payload: unknown) => void) {
      hlsListeners.set(eventName, callback);
    }
  }

  const parentWindow = {
    postMessage(message: PlayerMessage) { messages.push(message); },
  };
  const windowLike: {
    Hls: typeof FakeHls | undefined;
    addEventListener: () => void;
    location: { origin: string };
    parent: typeof parentWindow;
  } = {
    Hls: runtime === 'hlsjs' ? FakeHls : undefined,
    addEventListener() {},
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
    clearTimeout(timer: { active: boolean }) { timer.active = false; },
    document: {
      getElementById(id: string) {
        return id === 'video' ? video : { style: { transform: '' } };
      },
    },
    fetch() {
      if (runtime === 'hlsjs') throw new Error('Native HLS fetch must not run when Hls.js is supported.');
      return Promise.resolve({
        ok: nativeManifestStatus === 200,
        status: nativeManifestStatus,
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
    hls: () => hlsInstances[0] ?? null,
    messages,
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
  assert.match(errorMessage?.data?.message ?? '', /لم تصل بيانات الفيديو/);
});

test('2026-09-04 Apple native HTTP 403 preserves the confirmed Bunny rejection', async () => {
  const player = await runHlsPlayer('native-apple', 403);
  await new Promise<void>((resolve) => setImmediate(resolve));
  const error = player.messages.find(message => message.type === 'error');
  assert.equal(error?.data?.code, 403);
  assert.equal(error?.data?.phase, 'native_manifest_http');
  assert.match(error?.data?.message ?? '', /Token Authentication Key/);
  assert.equal(player.messages.some(message => message.type === 'ready'), false);
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

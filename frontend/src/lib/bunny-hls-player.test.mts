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

async function runHlsPlayer() {
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
  const timers: Array<{ callback: () => void; active: boolean }> = [];
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
    canPlayType() { return ''; },
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
  const windowLike = {
    Hls: FakeHls,
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
  windowLike.Hls = InstrumentedHls;

  vm.runInNewContext(playerScript, {
    URL,
    clearTimeout(timer: { active: boolean }) { timer.active = false; },
    document: {
      getElementById(id: string) {
        return id === 'video' ? video : { style: { transform: '' } };
      },
    },
    fetch() { throw new Error('Native HLS fetch must not run when Hls.js is supported.'); },
    isFinite,
    location: windowLike.location,
    Math,
    Number,
    parent: parentWindow,
    Promise,
    setInterval() { return 1; },
    setTimeout(callback: () => void) {
      const timer = { callback, active: true };
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
    hls: () => hlsInstances[0] ?? null,
    messages,
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
});

test('2026-09-03 stalled Bunny HLS exits the tablet spinner on its deadline', async () => {
  const player = await runHlsPlayer();

  player.triggerLoadDeadline();

  const errorMessage = player.messages.find((message) => message.type === 'error');
  assert.equal(errorMessage?.data?.provider, 'bunny-hls');
  assert.match(errorMessage?.data?.message ?? '', /انتهت مهلة تحميل Bunny HLS/);
  assert.equal(player.hls()?.destroyCalls, 1);
});

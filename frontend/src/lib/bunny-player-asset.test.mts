import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';

const routePath = new URL('../app/api/video/embed/route.ts', import.meta.url);
const securePlayerPath = new URL('../components/video/SecureVideoPlayer.tsx', import.meta.url);
const playerBridgePath = new URL('../../public/vendor/playerjs/player-0.1.0.min.js', import.meta.url);

type BridgeMessage = {
  source?: string;
  type?: string;
  data?: Record<string, unknown>;
};

type BridgeCallback = (value?: unknown) => void;

interface BridgeInterval {
  callback: () => void;
  delay: number;
  active: boolean;
}

interface BunnyBridgeHarness {
  callbacks: {
    duration: BridgeCallback[];
    paused: BridgeCallback[];
    playbackRate: BridgeCallback[];
    volume: BridgeCallback[];
  };
  dispatchProviderMessage: (data: unknown, source?: object, origin?: string) => void;
  dispatchParentMessage: (data: unknown) => void;
  fireProviderLoad: () => void;
  iframeState: () => { removeCalls: number; src: string };
  messages: BridgeMessage[];
  playerCount: () => number;
  providerCommands: () => Record<string, unknown>[];
  player: {
    emit: (eventName: string, value?: unknown) => void;
    rawReadyCalls: unknown[];
  } | null;
  resizeViewport: (width: number, height: number) => void;
  tickIntervals: (delay: number, times?: number) => void;
  watermarkTransform: () => string;
}

function bunnyReadyMessage() {
  return {
    context: 'player.js',
    version: '0.0.11',
    event: 'ready',
    value: {
      // Deliberately differs from the harness iframe URL, matching the URL
      // normalization mismatch that the recovery bridge exists to repair.
      src: 'https://player.mediadelivery.net/embed/library/video?autoplay=false&playsinline=true',
      events: ['ready', 'play', 'pause', 'ended', 'timeupdate', 'error'],
      methods: ['play', 'pause', 'getPaused', 'getDuration', 'getCurrentTime', 'getVolume'],
    },
  };
}

function normalizedMessages(messages: BridgeMessage[]): BridgeMessage[] {
  return JSON.parse(JSON.stringify(messages)) as BridgeMessage[];
}

async function runBunnyBridge(options: {
  constructorFails?: boolean;
  playerLibraryAvailable?: boolean;
} = {}): Promise<BunnyBridgeHarness> {
  const routeSource = await readFile(routePath, 'utf8');
  const bridgeStart = routeSource.indexOf('// Bunny Player.js → Parent PostMessage Bridge');
  const bridgeEnd = routeSource.indexOf('  </script>', bridgeStart);
  assert.ok(bridgeStart >= 0 && bridgeEnd > bridgeStart, 'Bunny bridge script must be present');

  const bridgeScript = routeSource
    .slice(bridgeStart, bridgeEnd)
    .replace('${devToolsGuard}', 'var __videoEmbedSuspended = false;')
    .replace('${safeSrc}', JSON.stringify('https://player.example/embed/library/video'));
  assert.doesNotMatch(bridgeScript, /\$\{/);

  const messages: BridgeMessage[] = [];
  const intervals: BridgeInterval[] = [];
  const windowListeners = new Map<string, BridgeCallback[]>();
  const iframeListeners = new Map<string, BridgeCallback[]>();
  const providerCommands: Record<string, unknown>[] = [];
  const iframeWindow = {
    postMessage(data: string | Record<string, unknown>) {
      providerCommands.push(typeof data === 'string' ? JSON.parse(data) : data);
    },
  };
  const callbacks = {
    duration: [] as BridgeCallback[],
    paused: [] as BridgeCallback[],
    playbackRate: [] as BridgeCallback[],
    volume: [] as BridgeCallback[],
  };
  let currentTime = 12;
  const playerInstances: FakePlayer[] = [];

  class FakePlayer {
    isReady = false;
    rawReadyCalls: unknown[] = [];
    private readonly handlers = new Map<string, BridgeCallback[]>();

    constructor() {
      if (options.constructorFails) throw new Error('constructor failed');
      playerInstances.push(this);
    }

    on(eventName: string, callback: BridgeCallback) {
      const handlers = this.handlers.get(eventName) ?? [];
      handlers.push(callback);
      this.handlers.set(eventName, handlers);
    }

    emit(eventName: string, value?: unknown) {
      for (const callback of this.handlers.get(eventName) ?? []) callback(value);
    }

    ready(message: unknown) {
      const readyValue = (message as {
        value?: { src?: unknown; events?: unknown; methods?: unknown };
      } | null)?.value;
      if (
        !readyValue
        || typeof readyValue.src !== 'string'
        || !Array.isArray(readyValue.events)
        || !Array.isArray(readyValue.methods)
      ) {
        throw new TypeError('Player.js ready payload must include src, events, and methods');
      }
      if (this.isReady) return false;
      this.rawReadyCalls.push(message);
      this.isReady = true;
      this.emit('ready');
      return true;
    }

    getDuration(callback: BridgeCallback) { callbacks.duration.push(callback); }
    getPaused(callback: BridgeCallback) { callbacks.paused.push(callback); }
    getVolume(callback: BridgeCallback) { callbacks.volume.push(callback); }
    getPlaybackRate(callback: BridgeCallback) { callbacks.playbackRate.push(callback); }
    getCurrentTime(callback: BridgeCallback) { callback(currentTime); }
    setPlaybackRate() {}
    setCurrentTime(time: number) { currentTime = time; }
    setVolume() {}
    play() {}
    pause() {}
    off(eventName: string) { this.handlers.delete(eventName); }
    supports(kind: 'event' | 'method', value: string) {
      if (kind === 'event') return ['ready', 'play', 'pause', 'ended', 'timeupdate', 'error'].includes(value);
      return ['play', 'pause', 'getPaused', 'getDuration', 'getCurrentTime', 'getVolume'].includes(value);
    }
  }

  let iframeSource = '';
  let iframeRemoveCalls = 0;
  const iframe = {
    contentWindow: iframeWindow,
    get src() { return iframeSource; },
    set src(value: string) { iframeSource = value; },
    addEventListener(eventName: string, callback: BridgeCallback) {
      const listeners = iframeListeners.get(eventName) ?? [];
      listeners.push(callback);
      iframeListeners.set(eventName, listeners);
    },
    removeAttribute() {},
    remove() { iframeRemoveCalls += 1; },
  };
  const watermark = { offsetHeight: 60, offsetWidth: 100, style: { transform: '' } };
  const parentWindow = {
    postMessage(message: BridgeMessage) {
      messages.push(message);
    },
  };
  const windowLike = {
    innerHeight: 180,
    innerWidth: 320,
    location: { origin: 'https://app.massar-academy.net' },
    parent: parentWindow,
    addEventListener(eventName: string, callback: BridgeCallback) {
      const listeners = windowListeners.get(eventName) ?? [];
      listeners.push(callback);
      windowListeners.set(eventName, listeners);
    },
    removeEventListener(eventName: string, callback: BridgeCallback) {
      windowListeners.set(
        eventName,
        (windowListeners.get(eventName) ?? []).filter((listener) => listener !== callback),
      );
    },
  };
  const setIntervalLike = (callback: () => void, delay: number) => {
    const interval = { callback, delay, active: true };
    intervals.push(interval);
    return interval;
  };
  const clearIntervalLike = (interval: BridgeInterval | null) => {
    if (interval) interval.active = false;
  };

  vm.runInNewContext(bridgeScript, {
    clearInterval: clearIntervalLike,
    document: {
      getElementById(id: string) {
        return id === 'bunny-frame' ? iframe : watermark;
      },
    },
    isFinite,
    Math,
    Number,
    playerjs: options.playerLibraryAvailable === false ? undefined : { Player: FakePlayer },
    setInterval: setIntervalLike,
    window: windowLike,
  });

  return {
    callbacks,
    dispatchProviderMessage(
      data,
      source = iframeWindow,
      origin = 'https://player.mediadelivery.net',
    ) {
      for (const listener of windowListeners.get('message') ?? []) {
        listener({
          data,
          origin,
          source,
        });
      }
    },
    dispatchParentMessage(data) {
      for (const listener of windowListeners.get('message') ?? []) {
        listener({
          data,
          origin: 'https://app.massar-academy.net',
          source: parentWindow,
        });
      }
    },
    fireProviderLoad() {
      for (const listener of iframeListeners.get('load') ?? []) listener();
    },
    iframeState: () => ({ removeCalls: iframeRemoveCalls, src: iframeSource }),
    messages,
    playerCount: () => playerInstances.length,
    providerCommands: () => providerCommands,
    player: playerInstances[0] ?? null,
    resizeViewport(width, height) {
      windowLike.innerWidth = width;
      windowLike.innerHeight = height;
      for (const listener of windowListeners.get('resize') ?? []) listener();
    },
    tickIntervals(delay, times = 1) {
      for (let iteration = 0; iteration < times; iteration += 1) {
        for (const interval of intervals) {
          if (interval.active && interval.delay === delay) interval.callback();
        }
      }
    },
    watermarkTransform: () => watermark.style.transform,
  };
}

test('Bunny playback loads its Player.js bridge locally before bridge initialization', async () => {
  const [routeSource, playerBridgeSource] = await Promise.all([
    readFile(routePath, 'utf8'),
    readFile(playerBridgePath, 'utf8'),
  ]);

  const localScriptTag = '<script src="/vendor/playerjs/player-0.1.0.min.js"></script>';
  const bridgeInitialization = '// Bunny Player.js → Parent PostMessage Bridge';

  assert.ok(routeSource.indexOf(localScriptTag) >= 0);
  assert.ok(routeSource.indexOf(localScriptTag) < routeSource.indexOf(bridgeInitialization));
  assert.doesNotMatch(routeSource, /assets\.mediadelivery\.net\/playerjs/);
  assert.ok(playerBridgeSource.length > 10_000);
  assert.match(playerBridgeSource, /playerjs/);
  assert.doesNotMatch(playerBridgeSource, /=>/);
});

test('2026-09-02 Bunny playback waits for a student gesture in Google in-app browsers', async () => {
  const routeSource = await readFile(routePath, 'utf8');
  const readyHandlerStart = routeSource.indexOf("activePlayer.on('ready'");
  const playHandlerStart = routeSource.indexOf("activePlayer.on('play'", readyHandlerStart);

  assert.match(routeSource, /autoplay=false&playsinline=true&disableIosPlayer=true/);
  assert.doesNotMatch(routeSource, /autoplay=true/);
  assert.ok(readyHandlerStart >= 0);
  assert.ok(playHandlerStart > readyHandlerStart);
  assert.doesNotMatch(routeSource.slice(readyHandlerStart, playHandlerStart), /player\.play\(\)/);
});

test('2026-09-02 Bunny retries reuse the active session instead of superseding it', async () => {
  const playerSource = await readFile(securePlayerPath, 'utf8');
  const recoveryStart = playerSource.indexOf('const scheduleBunnyPlaybackRecovery');
  const recoveryEnd = playerSource.indexOf('const loadActiveEmbed', recoveryStart);
  const recoverySource = playerSource.slice(recoveryStart, recoveryEnd);

  assert.ok(recoveryStart >= 0);
  assert.ok(recoveryEnd > recoveryStart);
  assert.match(recoverySource, /reloadActiveEmbedRef\.current\?\.\(\)/);
  assert.doesNotMatch(recoverySource, /reloadSessionRef\.current/);
});

test('2026-09-03 Bunny tablet readiness is not blocked by metadata callbacks', async () => {
  const harness = await runBunnyBridge();

  harness.fireProviderLoad();
  harness.dispatchProviderMessage(JSON.stringify(bunnyReadyMessage()));

  const messages = normalizedMessages(harness.messages);
  assert.equal(messages[0]?.type, 'providerLoaded');
  assert.deepEqual(messages[1], {
    source: 'video-embed',
    type: 'ready',
    data: {
      duration: 0,
      volume: 100,
      isMuted: false,
      provider: 'bunny',
      playbackRate: 1,
    },
  });
  assert.equal(harness.callbacks.duration.length, 1);
  assert.equal(harness.callbacks.volume.length, 1);
});

test('2026-09-03 raw Bunny Player.js ready messages recover the tablet handshake once', async () => {
  const harness = await runBunnyBridge();
  const wrongSource = {};
  const readyMessage = JSON.stringify(bunnyReadyMessage());

  harness.dispatchProviderMessage(readyMessage, wrongSource);
  harness.dispatchProviderMessage(readyMessage, undefined, 'https://attacker.example');
  harness.dispatchProviderMessage({ context: 'player.js', event: 'ready' });
  assert.equal(harness.player?.rawReadyCalls.length, 0);

  harness.dispatchProviderMessage(readyMessage);
  harness.dispatchProviderMessage(readyMessage);

  assert.equal(harness.player?.rawReadyCalls.length, 1);
  assert.equal(harness.messages.filter((message) => message.type === 'ready').length, 1);
});

test('Bunny does not become tracking-ready from a bridge without a media clock', async () => {
  const harness = await runBunnyBridge();
  const message = bunnyReadyMessage();
  message.value.events = ['ready', 'play', 'pause', 'ended', 'error'];
  message.value.methods = ['play', 'pause', 'getPaused', 'getDuration', 'getVolume'];

  harness.dispatchProviderMessage(message);

  assert.equal(harness.player?.rawReadyCalls.length, 0);
  assert.equal(harness.messages.some((candidate) => candidate.type === 'ready'), false);
});

test('2026-09-03 Bunny reconciles a native play that happened before bridge readiness', async () => {
  const harness = await runBunnyBridge();
  harness.dispatchProviderMessage(bunnyReadyMessage());

  assert.equal(harness.callbacks.paused.length, 1);
  harness.callbacks.paused[0]?.(false);

  assert.deepEqual(normalizedMessages(harness.messages).at(-1), {
    source: 'video-embed',
    type: 'stateChange',
    data: { state: 1, isPlaying: true, recoveredFromPlayer: true },
  });
});

test('2026-09-03 Bunny infers a missed play event from consecutive media-clock movement', async () => {
  const harness = await runBunnyBridge();
  harness.dispatchProviderMessage(bunnyReadyMessage());

  harness.player?.emit('timeupdate', { seconds: 1 });
  harness.player?.emit('timeupdate', { seconds: 2 });
  harness.player?.emit('timeupdate', { seconds: 3 });

  const recoveredState = normalizedMessages(harness.messages)
    .find((message) => message.type === 'stateChange' && message.data?.recoveredFromClock === true);
  assert.deepEqual(recoveredState, {
    source: 'video-embed',
    type: 'stateChange',
    data: { state: 1, isPlaying: true, recoveredFromClock: true },
  });
});

test('Bunny bridge forwards delayed metadata and the effective playback rate', async () => {
  const harness = await runBunnyBridge();
  harness.dispatchProviderMessage(bunnyReadyMessage());

  harness.callbacks.duration[0]?.(120);
  harness.callbacks.volume[0]?.(0.4);
  harness.callbacks.playbackRate[0]?.(1.5);
  harness.tickIntervals(1000);

  const timeUpdate = normalizedMessages(harness.messages)
    .find((message) => message.type === 'timeUpdate');
  assert.deepEqual(timeUpdate, {
    source: 'video-embed',
    type: 'timeUpdate',
    data: {
      currentTime: 12,
      duration: 120,
      volume: 40,
      isMuted: false,
      state: 2,
      playbackRate: 1.5,
    },
  });

  harness.player?.emit('playbackratechange', { playbackRate: 2 });
  assert.deepEqual(normalizedMessages(harness.messages).at(-1), {
    source: 'video-embed',
    type: 'playbackRateChange',
    data: { playbackRate: 2, provider: 'bunny' },
  });
});

test('all Bunny bridge failures identify the provider for bounded recovery', async (t) => {
  await t.test('provider playback error', async () => {
    const harness = await runBunnyBridge();
    harness.player?.emit('error', 'provider failed');
    assert.equal(normalizedMessages(harness.messages).at(-1)?.data?.provider, 'bunny');
  });

  await t.test('Player.js constructor error', async () => {
    const harness = await runBunnyBridge({ constructorFails: true });
    assert.equal(normalizedMessages(harness.messages).at(-1)?.data?.provider, 'bunny');
  });

  await t.test('Player.js library timeout', async () => {
    const harness = await runBunnyBridge({ playerLibraryAvailable: false });
    harness.tickIntervals(250, 101);
    assert.equal(normalizedMessages(harness.messages).at(-1)?.data?.provider, 'bunny');
  });
});

test('2026-09-03 Bunny surface load stays visual-only and bridge retry preserves the iframe', async () => {
  const harness = await runBunnyBridge();
  const initialIframeState = harness.iframeState();

  harness.fireProviderLoad();
  assert.deepEqual(normalizedMessages(harness.messages), [{
    source: 'video-embed',
    type: 'providerLoaded',
    data: { provider: 'bunny' },
  }]);

  harness.dispatchParentMessage({ type: 'retryBridge' });
  assert.equal(harness.playerCount(), 2);
  assert.deepEqual(harness.iframeState(), initialIframeState);
  assert.equal(harness.messages.some((message) => message.type === 'ready'), false);
  assert.deepEqual(harness.providerCommands().at(-1), {
    context: 'player.js',
    version: '0.0.11',
    method: 'addEventListener',
    value: 'ready',
    listener: 'massar-bunny-ready-probe-v1',
  });

  harness.dispatchProviderMessage({
    ...bunnyReadyMessage(),
    listener: 'massar-bunny-ready-probe-v1',
  });
  assert.equal(harness.messages.filter((message) => message.type === 'ready').length, 1);
  assert.equal(harness.providerCommands().at(-1)?.method, 'removeEventListener');
});

test('2026-09-03 Bunny fullscreen stays on the protected platform surface', async () => {
  const routeSource = await readFile(routePath, 'utf8');
  const bunnyFrame = routeSource.match(/<iframe id="bunny-frame"[^>]*>/)?.[0] ?? '';

  assert.ok(bunnyFrame.length > 0);
  assert.doesNotMatch(bunnyFrame, /allowfullscreen/i);
  assert.doesNotMatch(bunnyFrame, /allow="[^"]*fullscreen/);
});

test('Bunny watermark remains inside phone and rotated fullscreen bounds', async () => {
  const harness = await runBunnyBridge();
  const readPosition = () => {
    const match = harness.watermarkTransform().match(/translate3d\((\d+)px,(\d+)px,0\)/);
    assert.ok(match, 'watermark must use bounded pixel coordinates');
    return { x: Number(match[1]), y: Number(match[2]) };
  };

  let position = readPosition();
  assert.ok(position.x >= 8 && position.x <= 212);
  assert.ok(position.y >= 8 && position.y <= 112);

  harness.resizeViewport(180, 320);
  position = readPosition();
  assert.ok(position.x >= 8 && position.x <= 72);
  assert.ok(position.y >= 8 && position.y <= 252);
});

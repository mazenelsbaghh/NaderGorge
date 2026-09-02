import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';

import { createDevToolsSuspensionScript } from './video-embed-devtools-guard.ts';

interface GuardExecution {
  readonly hookCalls: number;
  locations: string[];
  messages: unknown[];
  poll: (() => void) | null;
  setViewport: (widthDifference: number, heightDifference?: number) => void;
}

function runGuard(
  widthDifference: number,
  heightDifference = 0,
  navigatorLike: {
    userAgent?: string;
    platform?: string;
    maxTouchPoints?: number;
    userAgentData?: { mobile?: boolean };
  } = {},
  fullscreen = false,
): GuardExecution {
  const locations: string[] = [];
  const messages: unknown[] = [];
  let poll: (() => void) | null = null;
  const windowLike = {
    outerWidth: 1600,
    innerWidth: 1600 - widthDifference,
    outerHeight: 1000,
    innerHeight: 1000 - heightDifference,
    addEventListener: () => undefined,
    setInterval: (callback: () => void) => {
      poll = callback;
      return 1;
    },
    setTimeout: (callback: () => void) => {
      callback();
      return 1;
    },
    location: {
      replace: (location: string) => locations.push(location),
    },
    parent: {
      postMessage: (message: unknown) => messages.push(message),
    },
    document: {
      fullscreenElement: fullscreen ? {} : null,
      webkitFullscreenElement: null,
    },
  };
  Object.assign(windowLike, { top: windowLike });

  const context = {
    Number,
    isFinite,
    navigator: navigatorLike,
    window: windowLike,
  };
  vm.runInNewContext(
    `var hookCalls = 0; function suspendPlayerForInspection() { hookCalls += 1; }\n${createDevToolsSuspensionScript('suspendPlayerForInspection')}`,
    context,
  );

  return {
    get hookCalls() {
      return (context as typeof context & { hookCalls: number }).hookCalls;
    },
    locations,
    messages,
    poll,
    setViewport: (nextWidthDifference, nextHeightDifference = 0) => {
      windowLike.innerWidth = windowLike.outerWidth - nextWidthDifference;
      windowLike.innerHeight = windowLike.outerHeight - nextHeightDifference;
    },
  };
}

test('inspection guard suspends and unloads the embed document when the viewport indicates DevTools', () => {
  const result = runGuard(320);

  assert.equal(result.hookCalls, 1);
  assert.deepEqual(result.locations, ['about:blank']);
  assert.deepEqual(JSON.parse(JSON.stringify(result.messages)), [
    {
      source: 'video-embed',
      type: 'securityViolation',
      data: { reason: 'devtools-detected' },
    },
  ]);
});

test('inspection guard ignores iPad desktop viewport differences without suspending playback', () => {
  const result = runGuard(320, 200, {
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) Version/18.6 Mobile/15E148 Safari/604.1',
    platform: 'MacIntel',
    maxTouchPoints: 5,
  });

  assert.equal(result.hookCalls, 0);
  assert.deepEqual(result.locations, []);
  assert.deepEqual(result.messages, []);
});

test('inspection guard ignores Android landscape viewport changes', () => {
  const result = runGuard(420, 260, {
    userAgent: 'Mozilla/5.0 (Linux; Android 15; Mobile) AppleWebKit/537.36 Chrome/140 Mobile Safari/537.36',
    platform: 'Linux armv8l',
    maxTouchPoints: 5,
    userAgentData: { mobile: true },
  });

  assert.equal(result.hookCalls, 0);
  assert.deepEqual(result.locations, []);
  assert.deepEqual(result.messages, []);
});

test('inspection guard ignores dimension changes while the player is fullscreen', () => {
  const result = runGuard(320, 200, {
    userAgent: 'Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 Chrome/140 Safari/537.36',
  }, true);

  assert.equal(result.hookCalls, 0);
  assert.deepEqual(result.locations, []);
  assert.deepEqual(result.messages, []);
});

test('inspection guard catches a bottom-docked panel at the threshold', () => {
  const result = runGuard(0, 160);

  assert.equal(result.hookCalls, 1);
  assert.deepEqual(result.locations, ['about:blank']);
});

test('inspection guard continues monitoring a normal viewport and suspends only once', () => {
  const result = runGuard(0, 0);

  assert.equal(result.hookCalls, 0);
  assert.equal(result.locations.length, 0);
  assert.ok(result.poll);

  result.setViewport(320);
  result.poll?.();
  result.poll?.();

  assert.equal(result.hookCalls, 1);
  assert.deepEqual(result.locations, ['about:blank']);
});

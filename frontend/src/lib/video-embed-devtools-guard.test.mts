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

function runGuard(widthDifference: number, heightDifference = 0): GuardExecution {
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
  };
  Object.assign(windowLike, { top: windowLike });

  const context = {
    Number,
    isFinite,
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

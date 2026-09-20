import assert from 'node:assert/strict';
import test from 'node:test';
import vm from 'node:vm';

import { createDevToolsSuspensionScript } from './video-embed-devtools-guard.ts';

interface KeyboardEventLike {
  key: string;
  altKey?: boolean;
  ctrlKey?: boolean;
  metaKey?: boolean;
  shiftKey?: boolean;
  preventDefault: () => void;
  stopImmediatePropagation: () => void;
}

function runGuard() {
  const locations: string[] = [];
  const messages: unknown[] = [];
  let keydown: ((event: KeyboardEventLike) => void) | null = null;
  const windowLike = {
    outerWidth: 1600,
    innerWidth: 1100,
    outerHeight: 1000,
    innerHeight: 700,
    addEventListener: (eventName: string, callback: (event: KeyboardEventLike) => void) => {
      if (eventName === 'keydown') keydown = callback;
    },
    setTimeout: (callback: () => void) => {
      callback();
      return 1;
    },
    location: {
      origin: 'https://platform.test',
      replace: (location: string) => locations.push(location),
    },
    parent: {
      postMessage: (message: unknown) => messages.push(message),
    },
  };
  const context = { window: windowLike };
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
    press: (input: Pick<KeyboardEventLike, 'key' | 'altKey' | 'ctrlKey' | 'metaKey' | 'shiftKey'>) => {
      let prevented = false;
      let stopped = false;
      keydown?.({
        ...input,
        preventDefault: () => { prevented = true; },
        stopImmediatePropagation: () => { stopped = true; },
      });
      return { prevented, stopped };
    },
  };
}

test('large viewport differences never stop playback', () => {
  const playback = runGuard();

  assert.equal(playback.hookCalls, 0);
  assert.deepEqual(playback.locations, []);
  assert.deepEqual(playback.messages, []);
});

for (const shortcut of [
  { name: 'F12', key: 'F12' },
  { name: 'Ctrl+Shift+I', key: 'i', ctrlKey: true, shiftKey: true },
  { name: 'Ctrl+Shift+J', key: 'J', ctrlKey: true, shiftKey: true },
  { name: 'Ctrl+Shift+K', key: 'k', ctrlKey: true, shiftKey: true },
  { name: 'Meta+Option+I', key: 'i', metaKey: true, altKey: true },
  { name: 'Meta+Shift+C', key: 'c', metaKey: true, shiftKey: true },
]) {
  test(`${shortcut.name} suspends the isolated player once`, () => {
    const playback = runGuard();
    const firstPress = playback.press(shortcut);
    playback.press(shortcut);

    assert.deepEqual(firstPress, { prevented: true, stopped: true });
    assert.equal(playback.hookCalls, 1);
    assert.deepEqual(playback.locations, ['about:blank']);
    assert.deepEqual(JSON.parse(JSON.stringify(playback.messages)), [
      {
        source: 'video-embed',
        type: 'securityViolation',
        data: { reason: 'devtools-shortcut' },
      },
    ]);
  });
}

test('ordinary keyboard shortcuts keep playback active', () => {
  const playback = runGuard();
  const keyPress = playback.press({ key: 'p', ctrlKey: true, shiftKey: true });

  assert.deepEqual(keyPress, { prevented: false, stopped: false });
  assert.equal(playback.hookCalls, 0);
  assert.deepEqual(playback.locations, []);
  assert.deepEqual(playback.messages, []);
});

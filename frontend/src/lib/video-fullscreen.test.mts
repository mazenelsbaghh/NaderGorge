import assert from 'node:assert/strict';
import test from 'node:test';

import {
  exitVideoFullscreen,
  getFullscreenElement,
  lockVideoToLandscape,
  requestVideoFullscreen,
  unlockVideoOrientation,
} from './video-fullscreen.ts';

test('fullscreen uses the standard browser API when available', async () => {
  let requested = 0;
  const entered = await requestVideoFullscreen({
    requestFullscreen: async () => { requested += 1; },
  } as unknown as HTMLElement);

  assert.equal(entered, true);
  assert.equal(requested, 1);
});

test('fullscreen reports rejected entry and exit APIs to their fallback caller', async () => {
  const entered = await requestVideoFullscreen({
    requestFullscreen: async () => { throw new Error('not allowed'); },
  } as unknown as HTMLElement);
  const exited = await exitVideoFullscreen({
    exitFullscreen: async () => { throw new Error('not allowed'); },
  } as unknown as Document);

  assert.equal(entered, false);
  assert.equal(exited, false);
});

test('webkit fullscreen and exit APIs remain supported', async () => {
  let requested = 0;
  let exited = 0;
  const element = { webkitRequestFullscreen: () => { requested += 1; } } as unknown as HTMLElement;
  const documentLike = {
    fullscreenElement: null,
    webkitFullscreenElement: element,
    webkitExitFullscreen: () => { exited += 1; },
  } as unknown as Document;

  assert.equal(await requestVideoFullscreen(element), true);
  assert.equal(getFullscreenElement(documentLike), element);
  assert.equal(await exitVideoFullscreen(documentLike), true);
  assert.equal(requested, 1);
  assert.equal(exited, 1);
});

test('2026-09-02 landscape lock reports rejection from an embedded browser', async () => {
  const screenLike = {
    orientation: { lock: async () => { throw new Error('unsupported in custom tab'); } },
  } as unknown as Screen;

  assert.equal(await lockVideoToLandscape(screenLike), false);
});

test('orientation unlock is safe when the embedded browser rejects it', () => {
  const screenLike = {
    orientation: { unlock: () => { throw new Error('unsupported'); } },
  } as unknown as Screen;

  assert.doesNotThrow(() => unlockVideoOrientation(screenLike));
});

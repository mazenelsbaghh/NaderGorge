import assert from 'node:assert/strict';
import test from 'node:test';

import {
  exitVideoFullscreen,
  enterNativeVideoFullscreen,
  getFullscreenElement,
  lockVideoToLandscape,
  requestVideoFullscreen,
  unlockVideoOrientation,
  waitForVideoFullscreen,
} from './video-fullscreen.ts';

test('iPhone native video entry runs inside the gesture and reports Done without replacing playback', () => {
  const video = Object.assign(new EventTarget(), {
    readyState: 1, currentTime: 123, playbackRate: 1.5,
    webkitEnterFullscreen() { entered = true; },
  });
  let entered = false;
  let exited = false;
  const cleanup = enterNativeVideoFullscreen(video as unknown as HTMLVideoElement, () => { exited = true; });
  assert.equal(entered, true);
  assert.equal(video.currentTime, 123);
  assert.equal(video.playbackRate, 1.5);
  video.dispatchEvent(new Event('webkitendfullscreen'));
  assert.equal(exited, true);
  cleanup?.();
});

test('rejected native iPhone entry leaves fallback available and removes the exit listener', () => {
  const video = Object.assign(new EventTarget(), {
    readyState: 1, webkitEnterFullscreen() { throw new Error('not ready'); },
  });
  let exited = false;
  assert.equal(enterNativeVideoFullscreen(video as unknown as HTMLVideoElement, () => { exited = true; }), null);
  video.dispatchEvent(new Event('webkitendfullscreen'));
  assert.equal(exited, false);
});

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

test('2026-09-09 iPhone API that never settles releases the caller to protected fullscreen fallback', async () => {
  const entered = await requestVideoFullscreen({ requestFullscreen: () => new Promise(() => {}) } as unknown as HTMLElement, 10);
  assert.equal(entered, false);
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

test('late WebKit fullscreenchange wins before the pseudo-fullscreen fallback', async () => {
  const documentLike = new EventTarget() as EventTarget & {
    fullscreenElement: Element | null;
    webkitFullscreenElement: Element | null;
  };
  documentLike.fullscreenElement = null;
  documentLike.webkitFullscreenElement = null;

  const waiting = waitForVideoFullscreen(documentLike as unknown as Document, 100);
  documentLike.webkitFullscreenElement = {} as Element;
  documentLike.dispatchEvent(new Event('webkitfullscreenchange'));

  assert.equal(await waiting, true);
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

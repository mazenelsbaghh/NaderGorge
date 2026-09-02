import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const routePath = new URL('../app/api/video/embed/route.ts', import.meta.url);
const securePlayerPath = new URL('../components/video/SecureVideoPlayer.tsx', import.meta.url);
const playerBridgePath = new URL('../../public/vendor/playerjs/player-0.1.0.min.js', import.meta.url);

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
  const readyHandlerStart = routeSource.indexOf("player.on('ready'");
  const playHandlerStart = routeSource.indexOf("player.on('play'", readyHandlerStart);

  assert.match(routeSource, /autoplay=false&playsinline=true&disableIosPlayer=false/);
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
  const routeSource = await readFile(routePath, 'utf8');
  const readyHandlerStart = routeSource.indexOf("player.on('ready'");
  const progressHandlerStart = routeSource.indexOf("player.on('play'", readyHandlerStart);
  const readyHandler = routeSource.slice(readyHandlerStart, progressHandlerStart);

  assert.ok(readyHandlerStart >= 0);
  assert.ok(progressHandlerStart > readyHandlerStart);
  assert.ok(readyHandler.indexOf('notifyParentReady();') >= 0);
  assert.ok(readyHandler.indexOf('notifyParentReady();') < readyHandler.indexOf('player.getDuration'));
  assert.ok(readyHandler.indexOf('notifyParentReady();') < readyHandler.indexOf('player.getVolume'));
  assert.doesNotMatch(readyHandler, /player\.getCurrentTime\([\s\S]*player\.getDuration\([\s\S]*player\.getVolume/);
});

test('2026-09-03 Bunny native surface is uncovered while the tablet bridge connects', async () => {
  const [routeSource, playerSource] = await Promise.all([
    readFile(routePath, 'utf8'),
    readFile(securePlayerPath, 'utf8'),
  ]);

  assert.match(routeSource, /postToParent\('providerLoaded', \{ provider: 'bunny' \}\)/);
  assert.match(playerSource, /case 'providerLoaded'/);
  assert.match(playerSource, /provider === 'bunny' && nativeProviderSurfaceLoaded/);
});

test('2026-09-03 Bunny fullscreen stays on the protected platform surface', async () => {
  const routeSource = await readFile(routePath, 'utf8');
  const bunnyFrame = routeSource.match(/<iframe id="bunny-frame"[^>]*>/)?.[0] ?? '';

  assert.ok(bunnyFrame.length > 0);
  assert.doesNotMatch(bunnyFrame, /allowfullscreen/i);
  assert.doesNotMatch(bunnyFrame, /allow="[^"]*fullscreen/);
});

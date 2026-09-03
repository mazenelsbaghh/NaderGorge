import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const playerPath = new URL('../components/video/SecureVideoPlayer.tsx', import.meta.url);
const controlsPath = new URL('../components/video/PlayerControls.tsx', import.meta.url);
const servicePath = new URL('../services/video-session-service.ts', import.meta.url);
const globalStylesPath = new URL('../app/globals.css', import.meta.url);

test('2026-09-03 mobile page exits use one atomic keepalive batch and clean up listeners', async () => {
  const [playerSource, serviceSource] = await Promise.all([
    readFile(playerPath, 'utf8'),
    readFile(servicePath, 'utf8'),
  ]);
  const lifecycleStart = playerSource.indexOf('const handleVisibilityChange');
  const lifecycleEnd = playerSource.indexOf('const onWatchStatusChangeRef', lifecycleStart);
  const lifecycle = playerSource.slice(lifecycleStart, lifecycleEnd);

  assert.ok(lifecycleStart >= 0 && lifecycleEnd > lifecycleStart);
  assert.match(lifecycle, /flushProgressForPageExit\(\)/);
  assert.match(lifecycle, /window\.addEventListener\('pagehide', handlePageExit\)/);
  assert.match(lifecycle, /window\.addEventListener\('pageshow', handlePageShow\)/);
  assert.match(lifecycle, /window\.removeEventListener\('pagehide', handlePageExit\)/);
  assert.match(lifecycle, /window\.removeEventListener\('pageshow', handlePageShow\)/);
  assert.match(lifecycle, /document\.removeEventListener\('visibilitychange', handleVisibilityChange\)/);
  assert.match(serviceSource, /function trackProgressBatchWithKeepalive/);
  assert.match(serviceSource, /return fetch\(/);
  assert.match(serviceSource, /credentials: 'include'/);
  assert.match(serviceSource, /keepalive: true/);
  assert.match(serviceSource, /progressSegments: request\.progressSegments/);
  assert.match(serviceSource, /if \(options\?\.keepalive\) return trackProgressBatchWithKeepalive\(request\)/);
  assert.match(playerSource, /const existingDrain = progressDrainPromiseRef\.current/);
  assert.match(playerSource, /TRACKING_BATCH_MAX_SEGMENTS = 30/);
  assert.match(playerSource, /pageExitProgressPromiseRef\.current = batchPromise/);
  assert.match(playerSource, /progressSegmentsRef\.current\.length > 0/);
  assert.match(playerSource, /Failed to sync page-exit progress batch/);
  assert.doesNotMatch(playerSource, /Failed to replay progress during page exit/);
});

test('2026-09-03 a view is announced only after the progress API confirms it', async () => {
  const playerSource = await readFile(playerPath, 'utf8');
  const responseStart = playerSource.indexOf('const applyProgressResponse');
  const responseEnd = playerSource.indexOf('const flushTrackedProgress', responseStart);
  const responseHandler = playerSource.slice(responseStart, responseEnd);
  const trackingStart = playerSource.indexOf('trackingInterval.current = setInterval');
  const trackingEnd = playerSource.indexOf('}, [accrueTrackedPlayback, flushTrackedProgress, status]', trackingStart);
  const trackingLoop = playerSource.slice(trackingStart, trackingEnd);

  assert.ok(responseStart >= 0 && responseEnd > responseStart);
  assert.ok(trackingStart >= 0 && trackingEnd > trackingStart);
  assert.match(responseHandler, /sessionHasRegisteredView/);
  assert.match(responseHandler, /viewTrackedRef\.current = true/);
  assert.match(responseHandler, /setViewTracked\(true\)/);
  assert.match(trackingLoop, /void flushTrackedProgress\(\)/);
  assert.doesNotMatch(trackingLoop, /viewTrackedRef\.current = true/);
  assert.doesNotMatch(trackingLoop, /setViewTracked\(true\)/);
  assert.match(playerSource, /\{ keepalive: true, drain: true \}/);
});

test('portrait pseudo-fullscreen gives the rotated video child the complete surface', async () => {
  const globalStyles = await readFile(globalStylesPath, 'utf8');
  const selector = '.secure-video-pseudo-fullscreen.secure-video-force-landscape .secure-video-fullscreen-surface';
  const ruleStart = globalStyles.indexOf(selector);
  const ruleEnd = globalStyles.indexOf('\n  }', ruleStart);
  const rule = globalStyles.slice(ruleStart, ruleEnd);

  assert.ok(ruleStart >= 0 && ruleEnd > ruleStart);
  assert.match(rule, /width: 100% !important/);
  assert.match(rule, /height: 100% !important/);
  assert.match(rule, /min-height: 0 !important/);
});

test('video seek controls keep a full touch target and cancel without committing', async () => {
  const controlsSource = await readFile(controlsPath, 'utf8');
  const cancelStart = controlsSource.indexOf('const handlePointerCancel');
  const cancelEnd = controlsSource.indexOf('const commitValue', cancelStart);
  const cancelHandler = controlsSource.slice(cancelStart, cancelEnd);

  assert.ok(cancelStart >= 0 && cancelEnd > cancelStart);
  assert.match(controlsSource, /relative flex h-11 w-full cursor-pointer/);
  assert.match(cancelHandler, /setLocalValue\(value\)/);
  assert.doesNotMatch(cancelHandler, /onChange\(/);
  assert.match(controlsSource, /onPointerCancel=\{handlePointerCancel\}/);
});

test('2026-09-03 Bunny playback menu keeps a wide click-through area between seek zones', async () => {
  const playerSource = await readFile(playerPath, 'utf8');
  const narrowSeekZones = playerSource.match(/w-\[12\.5%\] min-w-11 max-w-16/g) ?? [];

  assert.equal(narrowSeekZones.length, 2);
  assert.doesNotMatch(playerSource, /pointer-events-auto h-full w-\[38%\]/);
});

test('2026-09-03 Bunny browser error documents stay covered until the media bridge is ready', async () => {
  const playerSource = await readFile(playerPath, 'utf8');
  const surfaceStart = playerSource.indexOf("case 'providerLoaded':");
  const surfaceEnd = playerSource.indexOf("case 'ready':", surfaceStart);
  const surfaceHandler = playerSource.slice(surfaceStart, surfaceEnd);
  const readyStart = surfaceEnd;
  const readyEnd = playerSource.indexOf("case 'stateChange':", readyStart);
  const readyHandler = playerSource.slice(readyStart, readyEnd);

  assert.ok(surfaceStart >= 0 && surfaceEnd > surfaceStart && readyEnd > readyStart);
  assert.match(surfaceHandler, /markSurfaceLoaded\(\)/);
  assert.doesNotMatch(surfaceHandler, /setNativeProviderSurfaceLoaded\(true\)/);
  assert.doesNotMatch(surfaceHandler, /setIsBuffering\(false\)/);
  assert.match(readyHandler, /setNativeProviderSurfaceLoaded\(embedProvider === 'bunny'\)/);
  assert.match(readyHandler, /setIsBuffering\(false\)/);
});

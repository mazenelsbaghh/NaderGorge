import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const routePath = new URL('../app/api/video/embed/route.ts', import.meta.url);
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

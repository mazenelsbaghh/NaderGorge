import assert from 'node:assert/strict';
import test from 'node:test';
import { bunnyPlaybackSelection } from './bunny-playback-mode.ts';

test('2026-09-07 API enum strings preserve the selected HLS player when editing', () => {
  for (const mode of [1, 'PlatformHls'] as const) assert.equal(bunnyPlaybackSelection(mode), 1);
  for (const mode of [0, 'BunnyPlayer', undefined] as const) assert.equal(bunnyPlaybackSelection(mode), 0);
});

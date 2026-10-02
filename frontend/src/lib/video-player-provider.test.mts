import assert from 'node:assert/strict';
import test from 'node:test';

import { usesNativeProviderControls, usesRenewableHlsSource } from './video-player-provider.ts';

test('Bunny videos use the provider player without platform chrome', () => {
  assert.equal(usesNativeProviderControls('bunny'), true);
  assert.equal(usesNativeProviderControls('Bunny'), true);
  assert.equal(usesNativeProviderControls('youtube'), false);
  assert.equal(usesNativeProviderControls('youtube-hls'), false);
  assert.equal(usesNativeProviderControls('bunny-hls'), false);
});

test('only custom HLS providers request protected source renewals', () => {
  for (const provider of ['bunny-hls', 'youtube-hls', 'vcdn']) assert.equal(usesRenewableHlsSource(provider), true);
  for (const provider of ['bunny', 'youtube', 'vk']) assert.equal(usesRenewableHlsSource(provider), false);
});

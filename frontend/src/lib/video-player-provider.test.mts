import assert from 'node:assert/strict';
import test from 'node:test';

import { usesNativeProviderControls } from './video-player-provider.ts';

test('Bunny videos use the provider player without platform chrome', () => {
  assert.equal(usesNativeProviderControls('bunny'), true);
  assert.equal(usesNativeProviderControls('Bunny'), true);
  assert.equal(usesNativeProviderControls('youtube'), false);
});

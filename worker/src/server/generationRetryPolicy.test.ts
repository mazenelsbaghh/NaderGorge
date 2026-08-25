import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  DIRECT_GENERATION_RETRY_DISABLED,
  directGenerationRetryDenied,
} from './generationRetryPolicy.js';

test('direct worker retry fails closed without changing the logical generation id', () => {
  const logicalJobId = 'video-stable-id_mindmaps';
  const response = directGenerationRetryDenied(logicalJobId);

  assert.equal(response.statusCode, 409);
  assert.equal(response.body.success, false);
  assert.equal(response.body.code, DIRECT_GENERATION_RETRY_DISABLED);
  assert.equal(response.body.id, logicalJobId);
});

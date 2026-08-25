import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseAiOutputLanguage, parseGenerationRunId, resolveGenerationRun } from './aiGenerationContract.js';

test('AI generation contract accepts only normalized worker language codes', () => {
  assert.equal(parseAiOutputLanguage('auto'), 'auto');
  assert.equal(parseAiOutputLanguage('ar'), 'ar');
  assert.equal(parseAiOutputLanguage('en'), 'en');
  assert.throws(() => parseAiOutputLanguage('Arabic'), /invalid outputLanguage/);
  assert.equal(parseAiOutputLanguage(undefined), 'auto');
});

test('legacy generation jobs derive a stable artifact fence without emitting a callback fence', () => {
  const first = resolveGenerationRun(undefined, 'legacy-job', 1_700_000_000_000);
  const repeated = resolveGenerationRun(undefined, 'legacy-job', 1_700_000_000_000);
  const laterJob = resolveGenerationRun(undefined, 'legacy-job', 1_700_000_000_001);

  assert.deepEqual(first, repeated);
  assert.match(first.artifactRunId, /^legacy-[0-9a-f]{32}$/);
  assert.equal(first.callbackRunId, undefined);
  assert.notEqual(laterJob.artifactRunId, first.artifactRunId);
});

test('AI generation contract requires a UUID generation run id', () => {
  assert.equal(
    parseGenerationRunId('AAAAAAAA-AAAA-4AAA-8AAA-AAAAAAAAAAAA'),
    'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  );
  assert.throws(() => parseGenerationRunId('../old-run'), /invalid generationRunId/);
  assert.throws(() => parseGenerationRunId(''), /invalid generationRunId/);
});

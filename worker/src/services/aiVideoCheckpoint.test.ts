import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createVideoAnalysisCheckpoint } from './aiVideoCheckpoint.js';
import type { VideoChapter } from './geminiService.js';

test('video analysis checkpoints preserve completed stages across retries', () => {
  const checkpoint = createVideoAnalysisCheckpoint('checkpoint-test-video', 'https://example.test/video');
  checkpoint.clear();
  assert.equal(checkpoint.transcription(), undefined);
  assert.equal(checkpoint.chapters(), undefined);

  checkpoint.saveTranscription('1\n00:00:00,000 --> 00:00:01,000\nاختبار');
  checkpoint.saveChapters([{ title: 'فصل', startTime: 0, endTime: 1, summaryText: 'ملخص', order: 1 }]);

  const resumed = createVideoAnalysisCheckpoint('checkpoint-test-video', 'https://example.test/video');
  assert.match(resumed.transcription() || '', /اختبار/);
  assert.equal(resumed.chapters()?.[0]?.title, 'فصل');
  resumed.clear();
  assert.equal(resumed.transcription(), undefined);
  assert.equal(resumed.chapters(), undefined);
});

test('video analysis checkpoints do not cross source URLs', () => {
  const firstSource = createVideoAnalysisCheckpoint('checkpoint-source-test', 'https://example.test/first');
  const secondSource = createVideoAnalysisCheckpoint('checkpoint-source-test', 'https://example.test/second');
  firstSource.clear();
  secondSource.clear();
  firstSource.saveTranscription('first source');

  assert.equal(secondSource.transcription(), undefined);
  firstSource.clear();
  secondSource.clear();
});

test('invalid chapter checkpoints are discarded instead of being resumed', () => {
  const checkpoint = createVideoAnalysisCheckpoint('checkpoint-invalid-chapters', 'https://example.test/invalid');
  checkpoint.clear();
  checkpoint.saveChapters([{ title: 'incomplete' }] as unknown as VideoChapter[]);

  assert.equal(checkpoint.chapters(), undefined);
  checkpoint.clear();
});

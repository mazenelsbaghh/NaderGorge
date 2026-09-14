import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { sharedAiVideoCheckpointsRoot } from '../config/storage.js';
import { createVideoAnalysisCheckpoint, sweepExpiredVideoAnalysisCheckpoints } from './aiVideoCheckpoint.js';
import type { VideoChapter } from './geminiService.js';

const RUN_ONE = '11111111-1111-4111-8111-111111111111';
const RUN_TWO = '22222222-2222-4222-8222-222222222222';

test('video analysis checkpoints preserve completed stages across retries', () => {
  const checkpoint = createVideoAnalysisCheckpoint('checkpoint-test-video', 'https://example.test/video', 'ar', RUN_ONE);
  checkpoint.clear();
  assert.equal(checkpoint.transcription(), undefined);
  assert.equal(checkpoint.chapters(), undefined);

  checkpoint.saveTranscription('1\n00:00:00,000 --> 00:00:01,000\nاختبار');
  checkpoint.saveChapters([{ title: 'فصل', startTime: 0, endTime: 1, summaryText: 'ملخص', order: 1 }]);

  const resumed = createVideoAnalysisCheckpoint('checkpoint-test-video', 'https://example.test/video', 'ar', RUN_ONE);
  assert.match(resumed.transcription() || '', /اختبار/);
  assert.equal(resumed.chapters()?.[0]?.title, 'فصل');
  resumed.clear();
  assert.equal(resumed.transcription(), undefined);
  assert.equal(resumed.chapters(), undefined);
});

test('video analysis checkpoints do not cross source URLs', () => {
  const firstSource = createVideoAnalysisCheckpoint('checkpoint-source-test', 'https://example.test/first', 'auto', RUN_ONE);
  const secondSource = createVideoAnalysisCheckpoint('checkpoint-source-test', 'https://example.test/second', 'auto', RUN_ONE);
  firstSource.clear();
  secondSource.clear();
  firstSource.saveTranscription('first source');

  assert.equal(secondSource.transcription(), undefined);
  firstSource.clear();
  secondSource.clear();
});

test('invalid chapter checkpoints are discarded instead of being resumed', () => {
  const checkpoint = createVideoAnalysisCheckpoint('checkpoint-invalid-chapters', 'https://example.test/invalid', 'en', RUN_ONE);
  checkpoint.clear();
  checkpoint.saveChapters([{ title: 'incomplete' }] as unknown as VideoChapter[]);

  assert.equal(checkpoint.chapters(), undefined);
  checkpoint.clear();
});

test('video analysis checkpoints never cross output languages or generation runs', () => {
  const arabicRun = createVideoAnalysisCheckpoint('checkpoint-language-test', 'https://example.test/video', 'ar', RUN_ONE);
  const englishRun = createVideoAnalysisCheckpoint('checkpoint-language-test', 'https://example.test/video', 'en', RUN_ONE);
  const nextArabicRun = createVideoAnalysisCheckpoint('checkpoint-language-test', 'https://example.test/video', 'ar', RUN_TWO);
  arabicRun.clear();
  englishRun.clear();
  nextArabicRun.clear();
  arabicRun.saveTranscription('arabic run');

  assert.equal(englishRun.transcription(), undefined);
  assert.equal(nextArabicRun.transcription(), undefined);

  arabicRun.clear();
  englishRun.clear();
  nextArabicRun.clear();
});

test('checkpoint sweep removes only expired crash leftovers', () => {
  const marker = 'expired-checkpoint-marker';
  const checkpoint = createVideoAnalysisCheckpoint(
    'checkpoint-expiry-test',
    'https://example.test/expired',
    'auto',
    RUN_ONE,
  );
  checkpoint.clear();
  checkpoint.saveTranscription(marker);
  const transcriptionPath = (fs.readdirSync(sharedAiVideoCheckpointsRoot, { recursive: true }) as string[])
    .map(relativePath => path.join(sharedAiVideoCheckpointsRoot, relativePath))
    .find(candidate => candidate.endsWith('transcription.srt')
      && fs.readFileSync(candidate, 'utf8') === marker);
  assert.ok(transcriptionPath);
  fs.utimesSync(path.dirname(transcriptionPath), new Date(0), new Date(0));

  assert.ok(sweepExpiredVideoAnalysisCheckpoints(1_000, 2_000) >= 1);
  assert.equal(checkpoint.transcription(), undefined);
  checkpoint.clear();
});

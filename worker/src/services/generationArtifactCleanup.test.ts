import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

import {
  cleanupCurrentMindmapArtifacts,
  readCallbackAcceptance,
  readCallbackResponseAcceptance,
  reconcileAnalysisArtifacts,
  reconcileMindmapArtifacts,
} from './generationArtifactCleanup.js';

const CURRENT_RUN = '11111111-1111-4111-8111-111111111111';
const OLD_RUN = '22222222-2222-4222-8222-222222222222';
const OTHER_RUN = '33333333-3333-4333-8333-333333333333';

function temporaryDirectory(testContext: { after(callback: () => void): void }) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'massar-ai-artifacts-'));
  testContext.after(() => {
    fs.rmSync(directory, { recursive: true, force: true });
  });
  return directory;
}

test('callback acceptance is read only from the typed data contract', () => {
  assert.equal(readCallbackAcceptance({ data: { accepted: true } }), true);
  assert.equal(readCallbackAcceptance({ data: { accepted: false } }), false);
  assert.equal(readCallbackAcceptance({ accepted: true }), undefined);
  assert.equal(readCallbackAcceptance({ data: { accepted: 'true' } }), undefined);
});

test('fenced callbacks reject a 2xx response without a typed receipt as retryable', async () => {
  await assert.rejects(
    readCallbackResponseAcceptance(new Response('{}'), CURRENT_RUN),
    (error: unknown) => error instanceof Error
      && error.name === 'WorkerExternalError'
      && (error as { retryable?: unknown }).retryable === true,
  );
});

test('legacy callbacks remain compatible with an empty 2xx response', async () => {
  assert.equal(
    await readCallbackResponseAcceptance(new Response(''), undefined),
    undefined,
  );
});

test('accepted analysis keeps the retained SRT and removes older regular artifacts without following symlinks', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'analysis-artifact-video';
  const current = `${videoId}_run_${CURRENT_RUN}.srt`;
  const old = `${videoId}_run_${OLD_RUN}.srt`;
  const symlink = `${videoId}_run_${OTHER_RUN}.srt`;
  const externalTarget = path.join(directory, 'outside.txt');

  fs.writeFileSync(path.join(directory, current), 'current');
  fs.writeFileSync(path.join(directory, old), 'old');
  fs.writeFileSync(externalTarget, 'must survive');
  fs.symlinkSync(externalTarget, path.join(directory, symlink));

  await reconcileAnalysisArtifacts(directory, videoId, CURRENT_RUN, true);

  assert.equal(fs.existsSync(path.join(directory, current)), true);
  assert.equal(fs.existsSync(path.join(directory, old)), false);
  assert.equal(fs.lstatSync(path.join(directory, symlink)).isSymbolicLink(), true);
  assert.equal(fs.readFileSync(externalTarget, 'utf8'), 'must survive');
});

test('stale analysis deletes only this run SRT', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'stale-analysis-video';
  const current = `${videoId}_run_${CURRENT_RUN}.srt`;
  const retained = `${videoId}_run_${OLD_RUN}.srt`;
  fs.writeFileSync(path.join(directory, current), 'stale');
  fs.writeFileSync(path.join(directory, retained), 'retained');

  await reconcileAnalysisArtifacts(directory, videoId, CURRENT_RUN, false);

  assert.equal(fs.existsSync(path.join(directory, current)), false);
  assert.equal(fs.existsSync(path.join(directory, retained)), true);
});

test('accepted batch keeps current mindmaps and removes other runs for the same video', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'batch-artifact-video';
  const current = `${videoId}_run_${CURRENT_RUN}_chapter_1_200.webp`;
  const oldFirst = `${videoId}_run_${OLD_RUN}_chapter_1_100.webp`;
  const oldSecond = `${videoId}_run_${OLD_RUN}_chapter_2_101.png`;
  const unrelated = `another-video_run_${OLD_RUN}_chapter_1_100.webp`;
  for (const fileName of [current, oldFirst, oldSecond, unrelated]) {
    fs.writeFileSync(path.join(directory, fileName), fileName);
  }

  await reconcileMindmapArtifacts(directory, videoId, CURRENT_RUN, true);

  assert.equal(fs.existsSync(path.join(directory, current)), true);
  assert.equal(fs.existsSync(path.join(directory, oldFirst)), false);
  assert.equal(fs.existsSync(path.join(directory, oldSecond)), false);
  assert.equal(fs.existsSync(path.join(directory, unrelated)), true);
});

test('accepted single chapter removes older versions only for that chapter', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'single-artifact-video';
  const current = `${videoId}_run_${CURRENT_RUN}_chapter_2_200.webp`;
  const oldSameChapter = `${videoId}_run_${OLD_RUN}_chapter_2_100.webp`;
  const oldOtherChapter = `${videoId}_run_${OLD_RUN}_chapter_3_100.webp`;
  for (const fileName of [current, oldSameChapter, oldOtherChapter]) {
    fs.writeFileSync(path.join(directory, fileName), fileName);
  }

  await reconcileMindmapArtifacts(directory, videoId, CURRENT_RUN, true, 2);

  assert.equal(fs.existsSync(path.join(directory, current)), true);
  assert.equal(fs.existsSync(path.join(directory, oldSameChapter)), false);
  assert.equal(fs.existsSync(path.join(directory, oldOtherChapter)), true);
});

test('missing callback acceptance preserves retryable artifacts while stale acceptance removes only this run', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'retryable-artifact-video';
  const current = `${videoId}_run_${CURRENT_RUN}_chapter_1_200.webp`;
  const retained = `${videoId}_run_${OLD_RUN}_chapter_1_100.webp`;
  fs.writeFileSync(path.join(directory, current), 'current');
  fs.writeFileSync(path.join(directory, retained), 'retained');

  await reconcileMindmapArtifacts(directory, videoId, CURRENT_RUN, undefined);
  assert.equal(fs.existsSync(path.join(directory, current)), true);
  assert.equal(fs.existsSync(path.join(directory, retained)), true);

  await reconcileMindmapArtifacts(directory, videoId, CURRENT_RUN, false);
  assert.equal(fs.existsSync(path.join(directory, current)), false);
  assert.equal(fs.existsSync(path.join(directory, retained)), true);
});

test('terminal cleanup removes current run final and partial mindmaps but never follows a matching symlink', async (testContext) => {
  const directory = temporaryDirectory(testContext);
  const videoId = 'terminal-artifact-video';
  const finalArtifact = `${videoId}_run_${CURRENT_RUN}_chapter_1_200.webp`;
  const atomicPartial = `.${videoId}_run_${CURRENT_RUN}_chapter_2_temp_201.png.12.34.tmp`;
  const transcodePartial = `.${videoId}_run_${CURRENT_RUN}_chapter_3_202.webp.12.tmp.webp`;
  const symlinkArtifact = `${videoId}_run_${CURRENT_RUN}_chapter_4_203.webp`;
  const externalTarget = path.join(directory, 'outside-target.webp');
  const oldArtifact = `${videoId}_run_${OLD_RUN}_chapter_1_100.webp`;
  for (const fileName of [finalArtifact, atomicPartial, transcodePartial, oldArtifact]) {
    fs.writeFileSync(path.join(directory, fileName), fileName);
  }
  fs.writeFileSync(externalTarget, 'must survive');
  fs.symlinkSync(externalTarget, path.join(directory, symlinkArtifact));

  await cleanupCurrentMindmapArtifacts(directory, videoId, CURRENT_RUN);

  assert.equal(fs.existsSync(path.join(directory, finalArtifact)), false);
  assert.equal(fs.existsSync(path.join(directory, atomicPartial)), false);
  assert.equal(fs.existsSync(path.join(directory, transcodePartial)), false);
  assert.equal(fs.existsSync(path.join(directory, oldArtifact)), true);
  assert.equal(fs.lstatSync(path.join(directory, symlinkArtifact)).isSymbolicLink(), true);
  assert.equal(fs.readFileSync(externalTarget, 'utf8'), 'must survive');
});

import test from 'node:test';
import assert from 'node:assert/strict';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { GradingRubricStore } from './gradingRubricStore.js';

const source = { questionText: 'اذكر النتائج', expectedAnswer: 'النتيجة الأولى والثانية والثالثة' };
const candidate = (weight: number) => JSON.stringify({ criteria: [{ meaning: 'النتيجة الأولى', weight }] });

test('concurrent rubric publishers and a restarted worker all use the first immutable rubric', async context => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'grading-rubric-race-'));
  context.after(() => fs.rm(root, { recursive: true, force: true }));
  const versions = await Promise.all([1, 2, 3].map(weight => new GradingRubricStore(root).resolve(source, async () => candidate(weight))));
  assert.deepEqual(versions[0], versions[1]);
  assert.deepEqual(versions[1], versions[2]);
  const reloaded = await new GradingRubricStore(root).resolve(source, async () => { throw new Error('provider unavailable after restart'); });
  assert.deepEqual(reloaded, versions[0]);
  assert.equal((await fs.readdir(root)).length, 1);
});

test('changed teacher references receive a new rubric while a damaged stored rubric cannot be silently replaced', async context => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'grading-rubric-integrity-'));
  context.after(() => fs.rm(root, { recursive: true, force: true }));
  const store = new GradingRubricStore(root);
  const first = await store.resolve(source, async () => candidate(1));
  const changed = await store.resolve({ ...source, expectedAnswer: 'نموذج مختلف' }, async () => candidate(2));
  assert.notEqual(first.fingerprint, changed.fingerprint);
  await fs.writeFile(path.join(root, `${first.fingerprint}.json`), JSON.stringify({ ...changed, version: 999 }));
  await assert.rejects(store.resolve(source, async () => candidate(3)), /identity mismatch/);
});

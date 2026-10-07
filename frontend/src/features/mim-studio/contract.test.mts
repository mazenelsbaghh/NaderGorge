import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { generationPrompt, preparedSourceMatches, type MimDocument, type MimSource } from './contract.ts';

const episode: MimDocument = JSON.parse(readFileSync(new URL('./prepared-episode.json', import.meta.url), 'utf8'));

test('script edits replace old spoken lines in the generation prompt', () => {
  const updated = structuredClone(episode);
  const oldDialogue = updated.scenes[0].shots[0].dialogue;
  updated.scenes[0].shots[0].dialogue = 'ميم: «بابا، ساعدني أفهم الحكاية!»';
  const prompt = generationPrompt(updated, 0);
  assert.ok(prompt.includes(updated.scenes[0].shots[0].dialogue));
  assert.ok(!prompt.includes(oldDialogue));
  assert.ok(prompt.includes('REFERENCE IMAGE 1'));
  assert.ok(prompt.includes('REFERENCE IMAGE 2'));
});

test('the prepared episode only attaches when every referenced chapter belongs to that video', () => {
  const source: MimSource = { id: 'source', title: 'شرح الحصة', sourceRevision: 1,
    chapters: episode.scenes.flatMap(scene => scene.sourceChapterIds).map(id => ({ id, title: 'فصل', summary: 'ملخص', startTime: 0, endTime: 5 })) };
  assert.equal(preparedSourceMatches(episode, source), true);
  assert.equal(preparedSourceMatches(episode, { ...source, chapters: source.chapters.slice(1) }), false);
  assert.equal(preparedSourceMatches(episode, { ...source, chapters: [] }), false);
});

test('the complete approved episode has four continuous thirty-second timelines', () => {
  assert.equal(episode.scenes.length, 4);
  for (const scene of episode.scenes) {
    assert.equal(scene.shots.length, 10);
    let end = 0;
    for (const shot of scene.shots) {
      assert.equal(shot.start, end);
      assert.ok(shot.end > shot.start);
      end = shot.end;
    }
    assert.equal(end, 30);
  }
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createLessonMimGameProcessor } from './generateLessonMimGame.js';

const videoId = '11111111-1111-4111-8111-111111111111';
const chapterId = '22222222-2222-4222-8222-222222222222';
const sourcePack = { lessonId: '33333333-3333-4333-8333-333333333333', lessonTitle: 'Lesson', outputLanguage: 'en' as const, videos: [{ id: videoId, sourceRevision: 1, title: 'Video', chapters: [{ id: chapterId, title: 'Chapter', summary: 'Summary', startTime: 0, endTime: 10 }] }] };
const mission = { title: 'M', instruction: 'I', hint: 'H', reward: 'R', icon: 'book', sourceRefs: [{ videoId, chapterId, startTime: 0, endTime: 10 }], choices: ['A', 'B'], tasks: Array.from({ length: 3 }, () => ({ label: 'T', icon: 'target', correctChoiceIndex: 0, explanation: 'E' })) };
const content = { schemaVersion: 1 as const, title: 'G', intro: 'I', sourceLabel: 'S', missions: [mission, mission, mission] };
const data = { gameId: '44444444-4444-4444-8444-444444444444', lessonId: sourcePack.lessonId, runId: '55555555-5555-4555-8555-555555555555', sourceFingerprint: 'a'.repeat(64), schemaVersion: 1, sourcePack };

test('processor persists generated JSON before exact completion callback', async () => {
  const events: string[] = [];
  const complete: any[] = [];
  const job: any = { data, updateData: async (next: any) => { job.data = next; events.push('saved'); } };
  await createLessonMimGameProcessor({ generate: async () => content, callbacks: { complete: async p => { events.push('callback'); complete.push(p); }, fail: async () => {} } })(job);
  assert.deepEqual(events, ['saved', 'callback']);
  assert.equal(complete[0].contentJson, JSON.stringify(content));
});

test('processor retries callback from saved content without a second inference', async () => {
  let generated = 0;
  const job: any = { data: { ...data, generatedContentJson: JSON.stringify(content) }, updateData: async () => assert.fail('must not rewrite') };
  await createLessonMimGameProcessor({ generate: async () => { generated++; return content; }, callbacks: { complete: async () => {}, fail: async () => {} } })(job);
  assert.equal(generated, 0);
});

import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

import {
  canPublishMimGameDraft,
  canShowStudentMimGame,
  mimGameContentDigest,
  mimGameProgressKey,
  mimGameProgressKeyForContent,
  parseMimGameContent,
  pollMimGameWhileGenerating,
} from './mim-game-contract.ts';

const content = {
  schemaVersion: 1,
  title: 'مراجعة الحصة',
  intro: 'راجع أهم أفكار الحصة عبر ثلاث مهمات قصيرة.',
  sourceLabel: 'ملخصات الفيديو والفصول',
  missions: Array.from({ length: 3 }, (_, missionIndex) => ({
    title: `المهمة ${missionIndex + 1}`,
    instruction: 'اختر الإجابة الصحيحة ثم أكمل المسار.',
    hint: 'راجع الفكرة الأساسية في الفصل.',
    reward: `ختم ${missionIndex + 1}`,
    icon: 'book',
    sourceRefs: [
      {
        videoId: '11111111-1111-1111-1111-111111111111',
        chapterId: `22222222-2222-2222-2222-22222222222${missionIndex}`,
        startTime: 10,
        endTime: 40,
      },
    ],
    choices: ['الإجابة الأولى', 'الإجابة الثانية'],
    tasks: Array.from({ length: 3 }, (_, taskIndex) => ({
      label: `سؤال ${taskIndex + 1}`,
      icon: 'target',
      correctChoiceIndex: taskIndex % 2,
      explanation: 'تفسير مرتبط بملخص الفصل.',
    })),
  })),
} as const;

test('accepts the backend schema and rejects markup or malformed answers', () => {
  assert.deepEqual(parseMimGameContent(JSON.stringify(content)), content);
  assert.equal(
    parseMimGameContent(JSON.stringify({ ...content, title: '<img src=x>' })),
    null
  );
  const invalid = structuredClone(content) as unknown as {
    missions: Array<{ tasks: Array<{ correctChoiceIndex: number }> }>;
  };
  invalid.missions[0].tasks[0].correctChoiceIndex = 9;
  assert.equal(parseMimGameContent(JSON.stringify(invalid)), null);
});

test('student game is omitted for locked, video-only, missing, or invalid lessons', () => {
  const mimGame = {
    contentJson: JSON.stringify(content),
    fingerprint: 'fingerprint-a',
    schemaVersion: 1,
  };
  assert.equal(canShowStudentMimGame({ mimGame }), true);
  assert.equal(canShowStudentMimGame({ isLocked: true, mimGame }), false);
  assert.equal(
    canShowStudentMimGame({ isVideoOnlyAccess: true, mimGame }),
    false
  );
  assert.equal(canShowStudentMimGame({}), false);
  assert.equal(
    canShowStudentMimGame({ mimGame: { ...mimGame, contentJson: '{}' } }),
    false
  );
});

test('progress keys isolate users, lessons, fingerprints, schemas, and preview mode', () => {
  const base = {
    userId: 'user-a',
    lessonId: 'lesson-a',
    fingerprint: 'fp-a',
    schemaVersion: 1,
    mode: 'student' as const,
  };
  const key = mimGameProgressKey(base);
  assert.notEqual(key, mimGameProgressKey({ ...base, userId: 'user-b' }));
  assert.notEqual(key, mimGameProgressKey({ ...base, lessonId: 'lesson-b' }));
  assert.notEqual(key, mimGameProgressKey({ ...base, fingerprint: 'fp-b' }));
  assert.notEqual(key, mimGameProgressKey({ ...base, schemaVersion: 2 }));
  assert.notEqual(key, mimGameProgressKey({ ...base, mode: 'preview' }));
});

test('content digest stays stable for the same game and changes for regenerated questions', async () => {
  const parsed = parseMimGameContent(JSON.stringify(content));
  assert.ok(parsed);
  const regenerated = structuredClone(parsed);
  regenerated.missions[0].tasks[0].label = 'سؤال جديد من نفس المصدر';
  assert.equal(
    await mimGameContentDigest(parsed),
    await mimGameContentDigest(parsed)
  );
  assert.notEqual(
    await mimGameContentDigest(parsed),
    await mimGameContentDigest(regenerated)
  );
  const baseKey = mimGameProgressKey({
    userId: 'user-a',
    lessonId: 'lesson-a',
    fingerprint: 'same-source',
    schemaVersion: 1,
    mode: 'student',
  });
  assert.equal(
    await mimGameProgressKeyForContent(baseKey, parsed),
    await mimGameProgressKeyForContent(baseKey, parsed)
  );
  assert.notEqual(
    await mimGameProgressKeyForContent(baseKey, parsed),
    await mimGameProgressKeyForContent(baseKey, regenerated)
  );
});

test('regenerated ready draft remains publishable when its source fingerprint is unchanged', () => {
  const firstDraft = parseMimGameContent(JSON.stringify(content));
  assert.ok(firstDraft);
  const regeneratedDraft = structuredClone(firstDraft);
  regeneratedDraft.missions[0].tasks[0].label = 'سؤال جديد';
  const state = {
    id: 'game-a',
    status: 'Ready',
    isEnabled: true,
    draftFingerprint: 'same-source',
    publishedFingerprint: 'same-source',
  } as const;
  assert.equal(canPublishMimGameDraft(state, firstDraft), true);
  assert.equal(canPublishMimGameDraft(state, regeneratedDraft), true);
});

test('generating poll continues through unchanged states until ready', async (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });
  const states = ['Generating', 'Generating', 'Ready'] as const;
  let calls = 0;
  const polling = pollMimGameWhileGenerating({
    load: async () => ({ status: states[calls++] }) as never,
    signal: new AbortController().signal,
    intervalMs: 100,
    maxAttempts: 5,
  });
  for (let index = 0; index < states.length; index += 1) {
    context.mock.timers.tick(100);
    await Promise.resolve();
    await Promise.resolve();
  }
  assert.deepEqual(await polling, { attempts: 3, timedOut: false });
  assert.equal(calls, 3);
});

test('generating poll cancels on unmount or lesson change and respects its maximum', async (context) => {
  context.mock.timers.enable({ apis: ['setTimeout'] });
  const controller = new AbortController();
  let cancelledCalls = 0;
  const cancelled = pollMimGameWhileGenerating({
    load: async () => {
      cancelledCalls += 1;
      return { status: 'Generating' } as never;
    },
    signal: controller.signal,
    intervalMs: 100,
    maxAttempts: 2,
  });
  controller.abort();
  context.mock.timers.tick(500);
  assert.deepEqual(await cancelled, { attempts: 0, timedOut: false });
  assert.equal(cancelledCalls, 0);

  const limited = pollMimGameWhileGenerating({
    load: async () => ({ status: 'Generating' }) as never,
    signal: new AbortController().signal,
    intervalMs: 100,
    maxAttempts: 2,
  });
  for (let index = 0; index < 2; index += 1) {
    context.mock.timers.tick(100);
    await Promise.resolve();
    await Promise.resolve();
  }
  assert.deepEqual(await limited, { attempts: 2, timedOut: true });
});

test('iframe bridge verifies origin and source and keeps publication outside the game shell', async () => {
  const frame = await readFile(
    new URL('../components/mim-game/LessonMimGameFrame.tsx', import.meta.url),
    'utf8'
  );
  const game = await readFile(
    new URL('../../public/mim-game/game.mjs', import.meta.url),
    'utf8'
  );
  assert.match(frame, /event\.origin !== window\.location\.origin/);
  assert.match(frame, /event\.source !== iframeRef\.current\?\.contentWindow/);
  assert.match(
    game,
    /event\.origin\s*!==\s*location\.origin\s*\|\|\s*event\.source\s*!==\s*parent/
  );
  assert.doesNotMatch(game, /publish-enable|\/api\/|Authorization|accessToken/);
});

import assert from 'node:assert/strict';
import test from 'node:test';
import { learningSummary, lessonProgressPercent, videoProgressPercent } from './student-learning-progress.ts';
import type { MyLessonDto } from '../services/student-service.ts';

test('video completion tolerates clock rounding but not a quota view', () => {
  assert.equal(videoProgressPercent({ durationSeconds: 100, learningWatchedSeconds: 30 }), 30);
  assert.equal(videoProgressPercent({ durationSeconds: 100, learningWatchedSeconds: 99.999 }), 100);
  assert.equal(videoProgressPercent({ durationSeconds: 100, learningWatchedSeconds: 100 }), 100);
  assert.equal(videoProgressPercent({ durationSeconds: 100, learningWatchedSeconds: 200 }), 100);
});

test('unknown duration is not misrepresented as zero or complete', () => {
  for (const durationSeconds of [undefined, null, 0, -1, NaN, Infinity]) {
    assert.equal(videoProgressPercent({ durationSeconds, learningWatchedSeconds: 300 }), null);
  }
  assert.equal(lessonProgressPercent([]), null);
  assert.equal(lessonProgressPercent([{ durationSeconds: 60, learningWatchedSeconds: 60 }, {}]), null);
});

test('lesson percentage is duration weighted and excess replay cannot cover another video', () => {
  assert.equal(lessonProgressPercent([
    { durationSeconds: 300, learningWatchedSeconds: 600 },
    { durationSeconds: 900, learningWatchedSeconds: 0 },
  ]), 25);
  assert.equal(lessonProgressPercent([
    { durationSeconds: 300, learningWatchedSeconds: 300 },
    { durationSeconds: 900, learningWatchedSeconds: 450 },
  ]), 62);
  assert.equal(lessonProgressPercent([
    { durationSeconds: 300, learningWatchedSeconds: 300 },
    { durationSeconds: 900, learningWatchedSeconds: 900 },
  ]), 100);
});

function lesson(overrides: Partial<MyLessonDto>): MyLessonDto {
  return {
    id: 'lesson', title: 'Lesson', order: 1, packageId: 'package', packageName: 'Course',
    termTitle: 'Term', sectionTitle: 'Section', teacherName: 'Teacher', imageUrl: null,
    isCompleted: false, videoCount: 1, watchedVideoCount: 0,
    ...overrides,
  };
}

test('home summary counts completed parts independently and weights time across lessons', () => {
  assert.deepEqual(learningSummary([
    lesson({ isCompleted: true, watchedVideoCount: 1, recordedWatchSeconds: 60, totalVideoSeconds: 60 }),
    lesson({ videoCount: 3, watchedVideoCount: 1, recordedWatchSeconds: 120, totalVideoSeconds: 540 }),
  ]), { percent: 30, completedLessons: 1, completedVideos: 2, totalVideos: 4 });
});

test('empty and partially unknown home data do not invent progress', () => {
  assert.deepEqual(learningSummary([]), { percent: null, completedLessons: 0, completedVideos: 0, totalVideos: 0 });
  assert.equal(learningSummary([
    lesson({ recordedWatchSeconds: 60, totalVideoSeconds: 60 }),
    lesson({ totalVideoSeconds: null }),
  ]).percent, null);
});

// 2026-09-15: rounding tolerance is per part and must stay consistent with the backend.
test('completion bounds missing time and makes every completed part display 100 percent', () => {
  for (const [durationSeconds, learningWatchedSeconds, percent] of [
    [100, 99, 100], [100, 98.9, 98], [3600, 3598, 100], [3600, 3597.9, 99], [1, 0, 0],
  ]) assert.equal(videoProgressPercent({ durationSeconds, learningWatchedSeconds }), percent);
  assert.equal(lessonProgressPercent([
    { durationSeconds: 100, learningWatchedSeconds: 99.999 },
    { durationSeconds: 3600, learningWatchedSeconds: 3598 },
  ]), 100);
  assert.equal(learningSummary([
    lesson({ isCompleted: true, recordedWatchSeconds: 3598, totalVideoSeconds: 3600 }),
  ]).percent, 100);
});

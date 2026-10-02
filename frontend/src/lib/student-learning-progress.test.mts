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

// Exact percentages such as 29% used to lose a point through floating-point division.
for (const percent of [0, 25, 29, 50, 57, 58, 75, 100]) {
  test(`partial ${percent}% stays consistent in video, lesson and course progress`, () => {
    const video = { durationSeconds: 100, learningWatchedSeconds: percent };
    assert.equal(videoProgressPercent(video), percent);
    assert.equal(lessonProgressPercent([video]), percent);
    assert.equal(learningSummary([lesson({ recordedWatchSeconds: percent, totalVideoSeconds: 100, isCompleted: percent === 100 })]).percent, percent);
  });
}

test('fractional playback preserves exact percentage boundaries without rounding genuinely partial time up', () => {
  for (const [durationSeconds, learningWatchedSeconds, expected] of [[7, 2.03, 29], [100, 28.999, 28], [600, 299.5, 49], [600, 300, 50]]) {
    const video = { durationSeconds, learningWatchedSeconds };
    assert.equal(videoProgressPercent(video), expected);
    assert.equal(lessonProgressPercent([video]), expected);
  }
});

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

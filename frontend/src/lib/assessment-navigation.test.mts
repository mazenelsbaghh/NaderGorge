import assert from 'node:assert/strict';
import test from 'node:test';
import { assessmentContentPath } from './assessment-navigation.ts';

test('staff assessment profiles and editors stay in staff instead of linking to admin (2026-09-10)', () => {
  for (const route of [
    '/assistant/content/lessons/lesson-id',
    '/assistant/content/exams/exam-id',
    '/assistant/content/exams/exam-id/dashboard',
    '/assistant/content/homework/homework-id/add-question',
  ]) {
    assert.equal(assessmentContentPath(route), '/assistant/content', route);
  }
});

test('admin and teacher assessment navigation retain their workspace', () => {
  assert.equal(assessmentContentPath('/admin/content/exams/exam-id'), '/admin/content');
  assert.equal(assessmentContentPath('/teacher/packages/exams/exam-id', 'teacher'), '/teacher/packages');
  assert.equal(assessmentContentPath('/assistant/content-other'), '/admin/content');
});

import assert from 'node:assert/strict';
import test from 'node:test';

import {
  filterStudentQuickAccessItems,
  getStudentQuickAccessKind,
} from './student-quick-access.ts';
import type { QuickAccessItemDto } from '../services/student-service.ts';

test('numeric and named API access types resolve to the same student sections', () => {
  assert.equal(getStudentQuickAccessKind(1), 'term');
  assert.equal(getStudentQuickAccessKind('Term'), 'term');
  assert.equal(getStudentQuickAccessKind(2), 'section');
  assert.equal(getStudentQuickAccessKind('Month'), 'section');
  assert.equal(getStudentQuickAccessKind(3), 'lesson');
  assert.equal(getStudentQuickAccessKind('Lesson'), 'lesson');
  assert.equal(getStudentQuickAccessKind(4), 'video');
  assert.equal(getStudentQuickAccessKind('Video'), 'video');
});

test('term subscription remains visible when the student has no full package', () => {
  const term: QuickAccessItemDto = {
    title: 'الترم الأول',
    pathBreadcrumb: 'الصف الأول > الترم الأول',
    url: '/student/packages/package-1/terms/term-1',
    accessType: 'Term',
  };

  assert.deepEqual(filterStudentQuickAccessItems([term], 'term'), [term]);
  assert.deepEqual(filterStudentQuickAccessItems([term], 'section'), []);
});

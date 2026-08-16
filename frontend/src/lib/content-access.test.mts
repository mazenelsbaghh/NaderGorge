import assert from 'node:assert/strict';
import test from 'node:test';

import { hasStudentTermAccess, type StudentPackageAccess, type StudentTermAccess } from './content-access.ts';

function packageFixture(overrides: Partial<StudentPackageAccess> = {}): StudentPackageAccess {
  return {
    id: 'package-1',
    ...overrides,
  };
}

const term: StudentTermAccess = {
  id: 'root-term-1',
  isPurchased: false,
};

test('a purchased flexible package root term cannot be offered for sale again', () => {
  const pkg = packageFixture({
    contentMode: 'SectionWithLessons',
    rootTermId: term.id,
    hasDirectPackageAccess: false,
    hasRootContentAccess: true,
  });

  assert.equal(hasStudentTermAccess(pkg, term), true);
});

test('root access does not unlock an unrelated term', () => {
  const pkg = packageFixture({
    contentMode: 'SectionWithLessons',
    rootTermId: 'another-term',
    hasRootContentAccess: true,
  });

  assert.equal(hasStudentTermAccess(pkg, term), false);
});

test('direct package and direct term grants still unlock the term', () => {
  assert.equal(hasStudentTermAccess(packageFixture({ hasDirectPackageAccess: true }), term), true);
  assert.equal(hasStudentTermAccess(packageFixture(), { ...term, isPurchased: true }), true);
});

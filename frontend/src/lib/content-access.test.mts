import assert from 'node:assert/strict';
import test from 'node:test';

import {
  hasStudentTermAccess,
  isFullPackagePurchaseDisabled,
  type StudentPackageAccess,
  type StudentTermAccess,
} from './content-access.ts';

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

test('only an explicitly disabled full-year package blocks root purchase', () => {
  const scenarios = [
    { name: 'legacy omitted flag', pkg: packageFixture(), expected: false },
    { name: 'explicitly enabled full-year package', pkg: packageFixture({ allowFullPackagePurchase: true }), expected: false },
    { name: 'explicitly disabled full-year package', pkg: packageFixture({ allowFullPackagePurchase: false }), expected: true },
    {
      name: 'a non-year root content mode',
      pkg: packageFixture({ contentMode: 'SectionWithLessons', allowFullPackagePurchase: false }),
      expected: false,
    },
  ];

  for (const scenario of scenarios) {
    assert.equal(isFullPackagePurchaseDisabled(scenario.pkg), scenario.expected, scenario.name);
  }
});

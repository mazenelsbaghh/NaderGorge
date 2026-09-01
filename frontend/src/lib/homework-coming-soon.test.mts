import assert from 'node:assert/strict';
import test from 'node:test';

import {
  getDefaultHomeworkComingSoonDate,
  getHomeworkComingSoonLabel,
} from './homework-coming-soon.ts';

test('default date follows Cairo midnight in summer and winter', () => {
  const scenarios = [
    ['summer before midnight', '2026-09-01T20:59:59Z', '2026-09-02'],
    ['summer after midnight', '2026-09-01T21:00:00Z', '2026-09-03'],
    ['winter before midnight', '2026-12-31T21:59:59Z', '2027-01-01'],
    ['winter after midnight', '2026-12-31T22:00:00Z', '2027-01-02'],
  ] as const;

  for (const [scenario, instant, expectedDate] of scenarios) {
    assert.equal(
      getDefaultHomeworkComingSoonDate(new Date(instant)),
      expectedDate,
      scenario
    );
  }
});

test('empty values do not render a coming-soon label', () => {
  assert.equal(getHomeworkComingSoonLabel(undefined), null);
  assert.equal(getHomeworkComingSoonLabel(null), null);
  assert.equal(getHomeworkComingSoonLabel(''), null);
  assert.equal(getHomeworkComingSoonLabel('   '), null);
});

test('invalid and past dates use the safe fallback label', () => {
  const now = new Date('2026-09-01T12:00:00Z');
  const scenarios = ['not-a-date', '2026-02-30', '2026-08-31'];

  for (const expectedOn of scenarios) {
    assert.equal(getHomeworkComingSoonLabel(expectedOn, now), 'سيظهر قريبًا');
  }
});

test('today and tomorrow labels follow Cairo midnight', () => {
  const scenarios = [
    ['2026-09-01', '2026-09-01T20:59:59Z', 'سيظهر اليوم'],
    ['2026-09-02', '2026-09-01T20:59:59Z', 'سيظهر غدًا'],
    ['2026-09-02', '2026-09-01T21:00:00Z', 'سيظهر اليوم'],
    ['2026-09-03', '2026-09-01T21:00:00Z', 'سيظهر غدًا'],
    ['2027-01-01', '2026-12-31T22:00:00Z', 'سيظهر اليوم'],
    ['2027-01-02', '2026-12-31T22:00:00Z', 'سيظهر غدًا'],
  ] as const;

  for (const [expectedOn, instant, expectedLabel] of scenarios) {
    assert.equal(
      getHomeworkComingSoonLabel(expectedOn, new Date(instant)),
      expectedLabel,
      `${expectedOn} at ${instant}`
    );
  }
});

test('farther future dates keep their calendar day in Arabic formatting', () => {
  const now = new Date('2026-09-01T20:59:59Z');

  assert.equal(
    getHomeworkComingSoonLabel('2026-09-04', now),
    'سيظهر يوم 4 سبتمبر 2026'
  );
});

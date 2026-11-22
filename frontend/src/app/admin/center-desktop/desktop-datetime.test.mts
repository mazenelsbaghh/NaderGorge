import assert from 'node:assert/strict';
import { test } from 'node:test';
import { desktopClock } from './desktop-datetime.ts';

// Regression: local Flutter timestamps must not gain a second Cairo offset.
for (const [stored, expected] of [
  ['2026-11-01T12:13:00.318556', '12:13'],
  ['2026-07-01T12:13:00', '12:13'],
  ['2026-11-01T10:13:00Z', '12:13'],
  ['2026-07-01T09:13:00Z', '12:13'],
  ['2026-11-01T12:13:00+02:00', '12:13'],
  ['', 'الوقت غير متوفر'],
]) {
  test(`attendance time ${stored || '(missing)'} displays ${expected}`, () => {
    assert.equal(desktopClock(stored), expected);
  });
}

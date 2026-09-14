import assert from 'node:assert/strict';
import test from 'node:test';
import { isValidSupportScheduleWindow } from './live-support-schedule.ts';

test('accepts a support window that crosses midnight', () => {
  assert.equal(isValidSupportScheduleWindow({
    dayOfWeek: 1,
    startLocalTime: '22:00:00',
    endLocalTime: '02:00:00',
  }), true);
});

test('rejects an empty 24-hour-looking window with matching endpoints', () => {
  assert.equal(isValidSupportScheduleWindow({
    dayOfWeek: 1,
    startLocalTime: '22:00',
    endLocalTime: '22:00:00',
  }), false);
});

test('rejects invalid days and malformed times', () => {
  assert.equal(isValidSupportScheduleWindow({
    dayOfWeek: 7,
    startLocalTime: '09:00:00',
    endLocalTime: '17:00:00',
  }), false);
  assert.equal(isValidSupportScheduleWindow({
    dayOfWeek: 1,
    startLocalTime: '25:00:00',
    endLocalTime: '02:00:00',
  }), false);
});

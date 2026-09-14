import assert from 'node:assert/strict';

import { cairoDateTimeLocalToIso, formatCairoDateTimeLocal } from './admin-utils.ts';

function verifyCairoConversions() {
  assert.equal(
    formatCairoDateTimeLocal('2026-07-22T12:00:00'),
    '2026-07-22T15:00',
    'zone-less API timestamps must be interpreted as UTC before applying Cairo summer time',
  );
  assert.equal(
    formatCairoDateTimeLocal('2026-12-22T12:00:00'),
    '2026-12-22T14:00',
    'zone-less API timestamps must be interpreted as UTC before applying Cairo winter time',
  );
  assert.equal(cairoDateTimeLocalToIso('2026-07-22T15:00'), '2026-07-22T12:00:00.000Z');
  assert.equal(cairoDateTimeLocalToIso('2026-12-22T14:00'), '2026-12-22T12:00:00.000Z');
  assert.equal(
    formatCairoDateTimeLocal(cairoDateTimeLocalToIso('2026-07-22T15:00')),
    '2026-07-22T15:00',
    'saving and reloading a Cairo expiry must not shift its displayed value',
  );
}

process.env.TZ = 'UTC';
verifyCairoConversions();

process.env.TZ = 'America/New_York';
verifyCairoConversions();

console.log('Admin Cairo time contracts passed for UTC and non-Cairo device time zones.');

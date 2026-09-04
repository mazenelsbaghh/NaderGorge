const LTR_ISOLATE = '\u2066';
const POP_DIRECTIONAL_ISOLATE = '\u2069';

function shortTime(value: string): string {
  return value.slice(0, 5);
}

function isolatedTime(value: string): string {
  return `${LTR_ISOLATE}${shortTime(value)}${POP_DIRECTIONAL_ISOLATE}`;
}

export function isOvernightShift(startsAt: string, endsAt: string): boolean {
  return endsAt <= startsAt;
}

export function formatShiftTimeRange(startsAt: string, endsAt: string): string {
  const nextDay = isOvernightShift(startsAt, endsAt) ? ' (اليوم التالي)' : '';
  return `من ${isolatedTime(startsAt)} إلى ${isolatedTime(endsAt)}${nextDay}`;
}

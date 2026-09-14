export function delayUntilNextCairoMidnight(now = new Date()): number {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Cairo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(now);
  const value = (type: Intl.DateTimeFormatPartTypes) =>
    Number(parts.find((part) => part.type === type)?.value);
  const approximateNextMidnight = new Date(Date.UTC(
    value('year'),
    value('month') - 1,
    value('day') + 1,
  ));
  const offsetName = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Africa/Cairo',
    timeZoneName: 'longOffset',
  }).formatToParts(approximateNextMidnight)
    .find((part) => part.type === 'timeZoneName')?.value ?? 'GMT+00:00';
  const offset = /GMT([+-])(\d{2}):(\d{2})/.exec(offsetName);
  const offsetMilliseconds = offset
    ? (Number(offset[2]) * 60 + Number(offset[3])) * 60_000
      * (offset[1] === '+' ? 1 : -1)
    : 0;
  return Math.max(
    1_000,
    approximateNextMidnight.getTime() - offsetMilliseconds - now.getTime(),
  );
}

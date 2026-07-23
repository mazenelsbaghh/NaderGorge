export function formatCompactNumber(value: number) {
  return new Intl.NumberFormat('en-US').format(value);
}

function parseApiUtcDate(value: string | Date) {
  if (value instanceof Date) return value;

  const hasExplicitZone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value);
  return new Date(hasExplicitZone ? value : `${value}Z`);
}

export function formatDate(value: string | Date, options?: Intl.DateTimeFormatOptions) {
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeZone: 'Africa/Cairo',
    ...options
  }).format(parseApiUtcDate(value));
}

/** Formats a UTC timestamp for a datetime-local input using Cairo civil time. */
export function formatCairoDateTimeLocal(value: string | Date) {
  const date = parseApiUtcDate(value);
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Africa/Cairo',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find((item) => item.type === type)?.value ?? '';

  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`;
}

/** Converts a Cairo datetime-local value to the UTC instant sent to the API. */
export function cairoDateTimeLocalToIso(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!match) return '';

  const [, year, month, day, hour, minute] = match;
  const localTimestamp = Date.UTC(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
  const offsetAt = (instant: number) => {
    const zone = new Intl.DateTimeFormat('en-US', {
      timeZone: 'Africa/Cairo',
      timeZoneName: 'longOffset',
    }).formatToParts(new Date(instant)).find((item) => item.type === 'timeZoneName')?.value ?? 'GMT+00:00';
    const offset = /GMT([+-])(\d{2}):(\d{2})/.exec(zone);
    if (!offset) return 0;
    return (Number(offset[2]) * 60 + Number(offset[3])) * 60_000 * (offset[1] === '+' ? 1 : -1);
  };

  // Re-evaluate once because Egypt observes daylight-saving time.
  let utcTimestamp = localTimestamp - offsetAt(localTimestamp);
  utcTimestamp = localTimestamp - offsetAt(utcTimestamp);
  return new Date(utcTimestamp).toISOString();
}

export function formatRelativeDate(value: string | Date) {
  const date = parseApiUtcDate(value);
  const diffMs = Date.now() - date.getTime();
  const minutes = Math.max(1, Math.floor(diffMs / 60000));

  if (minutes < 60) return `منذ ${formatCompactNumber(minutes)} دقيقة`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `منذ ${formatCompactNumber(hours)} ساعة`;

  const days = Math.floor(hours / 24);
  if (days === 1) return 'أمس';
  if (days < 30) return `منذ ${formatCompactNumber(days)} يوم`;

  return formatDate(date);
}

export function getInitials(name: string) {
  if (!name) return '';
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('');
}

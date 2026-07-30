const cairoDateFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Africa/Cairo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

const cairoDateTimeFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: 'Africa/Cairo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

function cairoDateParts(date = new Date()) {
  const parts = cairoDateFormatter.formatToParts(date);
  const value = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value;
  return { year: Number(value('year')), month: Number(value('month')), day: Number(value('day')) };
}

export function cairoCurrentDate(date = new Date()) {
  const { year, month, day } = cairoDateParts(date);
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

function parseUtcDateTime(dateTime: string | Date) {
  if (dateTime instanceof Date) return dateTime;
  return new Date(/(?:Z|[+-]\d{2}:\d{2})$/i.test(dateTime) ? dateTime : `${dateTime}Z`);
}

export function cairoCurrentMonthPeriod(date = new Date()) {
  const { year, month } = cairoDateParts(date);
  const first = `${year}-${String(month).padStart(2, '0')}-01`;
  const last = `${year}-${String(month).padStart(2, '0')}-${String(new Date(Date.UTC(year, month, 0)).getUTCDate()).padStart(2, '0')}`;
  return { first, last };
}

export function cairoDateAfterDays(days: number, date = new Date()) {
  const { year, month, day } = cairoDateParts(date);
  const target = new Date(Date.UTC(year, month - 1, day + days));
  return `${target.getUTCFullYear()}-${String(target.getUTCMonth() + 1).padStart(2, '0')}-${String(target.getUTCDate()).padStart(2, '0')}`;
}

export function formatCairoDateTime(dateTime: string | Date, options: Intl.DateTimeFormatOptions = {}) {
  return new Intl.DateTimeFormat('ar-EG', {
    ...options,
    timeZone: 'Africa/Cairo',
  }).format(parseUtcDateTime(dateTime));
}

export function cairoDateTimeLocalToUtcISOString(localDateTime: string) {
  const dateTimeMatch = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(localDateTime);
  if (!dateTimeMatch) throw new Error('CAIRO_DATETIME_INVALID');

  const [, year, month, day, hour, minute] = dateTimeMatch.map(Number);
  const intendedUtc = Date.UTC(year, month - 1, day, hour, minute);
  const formattedParts = cairoDateTimeFormatter.formatToParts(new Date(intendedUtc));
  const numberForPart = (type: Intl.DateTimeFormatPartTypes) => Number(formattedParts.find((formatPart) => formatPart.type === type)?.value);
  const renderedUtc = Date.UTC(numberForPart('year'), numberForPart('month') - 1, numberForPart('day'), numberForPart('hour'), numberForPart('minute'));
  return new Date(intendedUtc - (renderedUtc - intendedUtc)).toISOString();
}

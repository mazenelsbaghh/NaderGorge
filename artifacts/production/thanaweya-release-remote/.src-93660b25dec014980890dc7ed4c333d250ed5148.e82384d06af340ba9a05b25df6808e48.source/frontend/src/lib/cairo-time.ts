const cairoDateFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'Africa/Cairo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

function cairoDateParts(date = new Date()) {
  const parts = cairoDateFormatter.formatToParts(date);
  const value = (type: Intl.DateTimeFormatPartTypes) => parts.find((part) => part.type === type)?.value;
  return { year: Number(value('year')), month: Number(value('month')), day: Number(value('day')) };
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
